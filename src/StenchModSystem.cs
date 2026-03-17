using System;
using System.IO;
using System.Text.Json;
using StenchMod.AI;
using StenchMod.Behaviors;
using StenchMod.Client;
using StenchMod.Config;
using StenchMod.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace StenchMod
{
    /// <summary>
    /// Entry point for the Stench mod.  Handles:
    /// <list type="bullet">
    ///   <item>Config loading and saving</item>
    ///   <item>Entity behavior registration</item>
    ///   <item>Player-join fallback behavior attachment</item>
    ///   <item>Client-side HUD element creation</item>
    /// </list>
    /// </summary>
    public class StenchModSystem : ModSystem
    {
        private const string ServerConfigFileName = "stench.json";
        private const string ClientConfigFileName = "stench-client.json";

        // -------------------------------------------------------------------------
        // Public state (read by other classes)
        // -------------------------------------------------------------------------

        /// <summary>Active configuration loaded from stench.json.</summary>
        public static StenchConfig Config { get; private set; } = new StenchConfig();

        /// <summary>Client-only UI configuration loaded from stench-client.json.</summary>
        public static StenchClientConfig ClientConfig { get; private set; } = new StenchClientConfig();
        public static StenchTemporalAuraSystem? TemporalAuraSystem { get; private set; }

        private StenchHudRenderer? hudRenderer;
        private StenchOverlayRenderer? overlayRenderer;
        private StenchDebugOverlay? debugOverlay;
        private ICoreClientAPI? clientApi;
        private string? clientConfigPath;
        private DateTime clientConfigLastWriteUtc = DateTime.MinValue;

        // -------------------------------------------------------------------------
        // ModSystem overrides — both sides
        // -------------------------------------------------------------------------

        /// <inheritdoc/>
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            // Load or create config
            Config = api.LoadModConfig<StenchConfig>(ServerConfigFileName) ?? new StenchConfig();
            if (Config.NormalizeAndMigrate())
            {
                api.Logger.Notification("[StenchMod] Normalized legacy stench config values.");
            }
            api.StoreModConfig(Config, ServerConfigFileName);

            // Register entity behaviors used by JSON patches.
            api.RegisterEntityBehaviorClass("stench.stench", typeof(EntityBehaviorStench));
            api.RegisterEntityBehaviorClass("stench.sensitivity", typeof(EntityBehaviorStenchSensitivity));
            api.RegisterEntityBehaviorClass("stench.drifterai", typeof(EntityBehaviorDrifterStenchAI));

            AiTaskRegistry.Register<AiTaskStenchFreezeNearPlayer>("stenchfreezenearplayer");
            AiTaskRegistry.Register<AiTaskStenchSeekEntity>("stenchseekentity");
            AiTaskRegistry.Register<AiTaskStenchThrowAtEntity>("stenchthrowatentity");
            AiTaskRegistry.Register<AiTaskStenchMeleeAttackTargetingEntity>("stenchmeleeattack");

            Mod.Logger.Notification("[StenchMod] Entity behaviors registered.");
        }

        // -------------------------------------------------------------------------
        // ModSystem overrides — server side
        // -------------------------------------------------------------------------

        /// <inheritdoc/>
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            TemporalAuraSystem ??= new StenchTemporalAuraSystem();
            TemporalAuraSystem.InitializeServer(api);

            RegisterAdminCommands(api);

            // Emit a warning if player patching failed.
            api.Event.PlayerJoin += OnPlayerJoin;
        }

        // -------------------------------------------------------------------------
        // ModSystem overrides — client side
        // -------------------------------------------------------------------------

        /// <inheritdoc/>
        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);

            clientApi = capi;
            TemporalAuraSystem ??= new StenchTemporalAuraSystem();
            TemporalAuraSystem.InitializeClient(capi);
            ClientConfig = LoadClientConfig(capi);
            clientConfigPath = Path.Combine(capi.GetOrCreateDataPath("ModConfig"), ClientConfigFileName);
            clientConfigLastWriteUtc = GetClientConfigLastWriteUtc();

            hudRenderer = CreateClientElement(() => new StenchHudRenderer(capi), "HUD bar");
            overlayRenderer = CreateClientElement(() => new StenchOverlayRenderer(capi), "overlay");
            debugOverlay = CreateClientElement(() => new StenchDebugOverlay(capi), "debug overlay");
            capi.Event.RegisterGameTickListener(_ => CheckForClientConfigReload(), 500);

            Mod.Logger.Notification("[StenchMod] Client HUD elements created.");
        }

        public override void Dispose()
        {
            TemporalAuraSystem?.Dispose();
            TemporalAuraSystem = null;
            base.Dispose();
        }

        // -------------------------------------------------------------------------
        // Event handlers
        // -------------------------------------------------------------------------

        private void OnPlayerJoin(IServerPlayer player)
        {
            if (player.Entity.GetBehavior<EntityBehaviorStench>() == null)
            {
                Mod.Logger.Warning(
                    $"[StenchMod] Player {player.PlayerName} joined without EntityBehaviorStench. Check player entity patching.");
            }
        }

        private T? CreateClientElement<T>(Func<T> factory, string name) where T : class
        {
            try
            {
                T element = factory();
                Mod.Logger.Notification("[StenchMod] Initialized client {0}.", name);
                return element;
            }
            catch (Exception ex)
            {
                Mod.Logger.Error("[StenchMod] Failed to initialize client {0}: {1}", name, ex);
                return null;
            }
        }

        private void RegisterAdminCommands(ICoreServerAPI api)
        {
            api.ChatCommands
                .Create("smrod")
                .WithDescription("Sets a player's exact stench value.")
                .WithAdditionalInformation("Use a value between 0 and 100. This command is server-side and requires admin permissions.")
                .WithExamples("/smrod SomePlayer 65", "/stench SomePlayer 0", "/stench SomePlayer 100")
                .WithAlias("stench")
                .RequiresPrivilege(Privilege.controlserver)
                .WithArgs(
                    api.ChatCommands.Parsers.OnlinePlayer("player"),
                    api.ChatCommands.Parsers.IntRange("value", 0, 100)
                )
                .HandleWith(HandleSetPlayerStench);
        }

        private static TextCommandResult HandleSetPlayerStench(TextCommandCallingArgs args)
        {
            IPlayer target = (IPlayer)args[0];
            int value = (int)args[1];

            if (target.Entity == null)
            {
                return TextCommandResult.Error("Target player entity is not available.", "noentity");
            }

            EntityBehaviorStench? behavior = target.Entity.GetBehavior<EntityBehaviorStench>();
            if (behavior == null)
            {
                return TextCommandResult.Error("Target player has no stench behavior.", "nobehavior");
            }

            behavior.SetCurrentValue(value);
            return TextCommandResult.Success(
                $"Set {target.PlayerName} stench to {value}/100.",
                null
            );
        }

        private void CheckForClientConfigReload()
        {
            if (clientApi == null || string.IsNullOrEmpty(clientConfigPath))
            {
                return;
            }

            DateTime lastWriteUtc = GetClientConfigLastWriteUtc();
            if (lastWriteUtc == DateTime.MinValue || lastWriteUtc <= clientConfigLastWriteUtc)
            {
                return;
            }

            clientConfigLastWriteUtc = lastWriteUtc;
            ClientConfig = LoadClientConfig(clientApi);
            clientConfigLastWriteUtc = GetClientConfigLastWriteUtc();

            hudRenderer?.ReloadFromConfig();
            overlayRenderer?.ReloadFromConfig();
            debugOverlay?.ReloadFromConfig();
            Mod.Logger.Notification("[StenchMod] Reloaded stench-client.json and refreshed client UI.");
        }

        private static StenchClientConfig LoadClientConfig(ICoreAPI api)
        {
            JsonObject? rawClientConfig = api.LoadModConfig(ClientConfigFileName);
            bool hadClientConfig = rawClientConfig?.Exists == true;
            string rawClientJson = rawClientConfig?.ToString() ?? string.Empty;

            StenchClientConfig config = api.LoadModConfig<StenchClientConfig>(ClientConfigFileName) ?? new StenchClientConfig();
            bool changed = config.Normalize();

            if (hadClientConfig && ApplySchemaLessClientConfigMigration(config, rawClientJson))
            {
                changed = true;
            }

            if (!hadClientConfig)
            {
                JsonObject? rawLegacyConfig = api.LoadModConfig(ServerConfigFileName);
                if (rawLegacyConfig?.Exists == true && ApplyLegacyClientSettings(config, rawLegacyConfig.ToString()))
                {
                    changed = true;
                    api.Logger.Notification("[StenchMod] Migrated client UI settings from stench.json to stench-client.json.");
                }
            }

            if (changed || !hadClientConfig)
            {
                api.StoreModConfig(config, ClientConfigFileName);
            }

            return config;
        }

        private DateTime GetClientConfigLastWriteUtc()
        {
            if (string.IsNullOrEmpty(clientConfigPath) || !File.Exists(clientConfigPath))
            {
                return DateTime.MinValue;
            }

            return File.GetLastWriteTimeUtc(clientConfigPath);
        }

        private static bool ApplySchemaLessClientConfigMigration(StenchClientConfig config, string rawClientJson)
        {
            if (string.IsNullOrWhiteSpace(rawClientJson))
            {
                return false;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(rawClientJson);
                JsonElement root = document.RootElement;

                if (root.TryGetProperty(nameof(StenchClientConfig.UiConfigSchemaVersion), out _))
                {
                    return false;
                }

                bool changed = true;

                // Pre-1.3.10 client configs stored the working HUD slot in a +90
                // manual offset. The slot is now baked into layout code, so keep
                // 0 as-is but translate non-zero custom placements back into the
                // new coordinate system.
                if (config.BarOffsetYManual != 0)
                {
                    config.BarOffsetYManual -= 90;
                    changed = true;
                }

                return changed;
            }
            catch
            {
                return false;
            }
        }

        private static bool ApplyLegacyClientSettings(StenchClientConfig config, string rawLegacyJson)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(rawLegacyJson);
                JsonElement root = document.RootElement;
                bool changed = false;

                changed |= TryAssignBool(root, "ShowBar", value => config.ShowBar = value);
                changed |= TryAssignBool(root, "SegmentedBar", value => config.SegmentedBar = value);
                changed |= TryAssignBool(root, "ShowOverlay", value => config.ShowOverlay = value);
                changed |= TryAssignBool(root, "DebugMode", value => config.ShowDebugOverlay = value);
                changed |= TryAssignInt(root, "BarOffsetXManual", value => config.BarOffsetXManual = value);
                changed |= TryAssignInt(root, "BarOffsetYManual", value => config.BarOffsetYManual = value);
                changed |= TryAssignInt(root, "DebugOffsetXManual", value => config.DebugOffsetXManual = value);
                changed |= TryAssignInt(root, "DebugOffsetYManual", value => config.DebugOffsetYManual = value);

                if (changed)
                {
                    config.Normalize();
                }

                return changed;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryAssignBool(JsonElement root, string propertyName, Action<bool> assign)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement element) || element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False)
            {
                return false;
            }

            assign(element.GetBoolean());
            return true;
        }

        private static bool TryAssignInt(JsonElement root, string propertyName, Action<int> assign)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement element) || element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
            {
                return false;
            }

            assign(value);
            return true;
        }
    }
}
