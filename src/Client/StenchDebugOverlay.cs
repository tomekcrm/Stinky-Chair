using System;
using StenchMod.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;

namespace StenchMod.Client
{
    /// <summary>
    /// Debug HUD overlay displayed in the top-right corner when the client UI
    /// config enables it. Detailed rate fields are still sourced from
    /// server-side WatchedAttributes.
    /// </summary>
    public class StenchDebugOverlay : HudElement
    {
        private const double PanelWidth  = 290.0;
        private const double PanelHeight = 760.0;
        private const double Margin      = 10.0;

        private double updateAccum  = 0.0;
        private const double UpdateInterval = 0.5; // seconds between panel rebuilds
        private Entity? boundPlayer;
        private bool composerBuilt;

        private StenchConfig Config => StenchModSystem.Config;
        private StenchClientConfig ClientConfig => StenchModSystem.ClientConfig;

        /// <inheritdoc/>
        public override string ToggleKeyCombinationCode => null!;

        /// <inheritdoc/>
        public override bool ShouldReceiveKeyboardEvents() => false;

        /// <inheritdoc/>
        public override bool ShouldReceiveMouseEvents() => false;

        /// <inheritdoc/>
        public override bool Focusable => false;

        public StenchDebugOverlay(ICoreClientAPI capi) : base(capi)
        {
            EnsureOpen();
        }

        public override void OnOwnPlayerDataReceived()
        {
            base.OnOwnPlayerDataReceived();
            EnsureOpen();
            if (TryBindPlayer())
            {
                RebuildComposer();
            }
        }

        public override void OnLevelFinalize()
        {
            base.OnLevelFinalize();
            EnsureOpen();
            if (TryBindPlayer())
            {
                RebuildComposer();
            }
        }

        // -------------------------------------------------------------------------
        // Update
        // -------------------------------------------------------------------------

        /// <inheritdoc/>
        public override void OnRenderGUI(float deltaTime)
        {
            if (!IsOpened()) return;
            if (!TryBindPlayer()) return;

            updateAccum += deltaTime;
            if (updateAccum >= UpdateInterval)
            {
                updateAccum -= UpdateInterval;
                RebuildComposer();
            }

            base.OnRenderGUI(deltaTime);
        }

        // -------------------------------------------------------------------------
        // Layout
        // -------------------------------------------------------------------------

        private void RebuildComposer()
        {
            if (!IsOpened())
            {
                return;
            }

            if (composerBuilt)
            {
                SingleComposer?.Dispose();
                composerBuilt = false;
            }

            if (boundPlayer == null && !TryBindPlayer())
                return;

            if (boundPlayer == null)
                return;

            // Gather all debug data from WatchedAttributes
            var attrs = boundPlayer.WatchedAttributes;

            float  value         = attrs.GetFloat("stench:value",        0f);
            int    level         = attrs.GetInt  ("stench:level",         1);
            float  gainRate      = attrs.GetFloat("stench:gainrate",      0f);
            float  reductionRate = attrs.GetFloat("stench:reductionrate", 0f);
            float  clothingMult  = attrs.GetFloat("stench:clothingmult",  1f);
            bool   isRaining     = attrs.GetInt  ("stench:israining",     0) != 0;
            float  buzzEta       = attrs.GetFloat("stench:buzzeta",      -1f);
            int    buzzLevel     = attrs.GetInt  ("stench:buzzlevel",     0);
            string buzzLast      = attrs.GetString("stench:buzzlast",     "-");
            float  animalSeekTarget = attrs.GetFloat("stench:animalseektarget", 1f);
            float  animalSeekDelta  = attrs.GetFloat("stench:animalseekdelta",  0f);
            float  animalSeekFinal  = attrs.GetFloat("stench:animalseekfinal",  1f);
            int    tempEnabled   = attrs.GetInt  ("stench:tempenabled",   Config.EnableStabilityDrain ? 1 : 0);
            string tempMode      = attrs.GetString("stench:tempmode",      Config.StabilityMode);
            float  tempAuraRadius = attrs.GetFloat("stench:tempauraradius", Config.TemporalAuraRadius);
            float  tempAuraStrength = attrs.GetFloat("stench:tempaurastrength", Config.TemporalAuraLevel5Penalty);
            float  tempAuraFloor = attrs.GetFloat("stench:tempaurafloor", Config.TemporalAuraMinFinalStabilityLevel5);
            bool   tempAuraActive = attrs.GetInt("stench:tempauraactive", 0) != 0;
            float  tempAuraPenalty = attrs.GetFloat("stench:tempaurapenalty", 0f);
            int    tempAuraNearby = attrs.GetInt("stench:tempauranearby", 0);
            float  tempAuraBase = attrs.GetFloat("stench:tempaurabase", 0f);
            float  tempAuraFinal = attrs.GetFloat("stench:tempaurafinal", 0f);
            float  ownStability = attrs.GetFloat("stench:ownstability", 0f);
            float  tempTopupRate = attrs.GetFloat("stench:temptopuprate", 0f);
            float  tempTotalDrainRate = attrs.GetFloat("stench:temptotaldrainrate", 0f);

            float  netRate       = gainRate - reductionRate;

            bool swimming        = boundPlayer.Swimming;
            bool inLiquid        = boundPlayer.FeetInLiquid;

            bool vigorActive     = capi.ModLoader.IsModEnabled("vigor");
            bool hodActive       = capi.ModLoader.IsModEnabled("hydrateordiedrate");

            string clothingCat   = clothingMult >= Config.ClothingHeavyMultiplier  - 0.01f ? "Heavy"
                                 : clothingMult >= Config.ClothingMediumMultiplier - 0.01f ? "Medium"
                                 : clothingMult >= Config.ClothingLightMultiplier  - 0.01f ? "Light"
                                 : "None";

            string levelName     = (level >= 1 && level <= (Config.LevelNames?.Length ?? 0))
                                   ? Config.LevelNames![level - 1]
                                   : "?";
            string thresholdProfile = Config.LevelThresholds != null && Config.LevelThresholds.Length == 5
                ? $"{Config.LevelThresholds[0]:F0}/{Config.LevelThresholds[1]:F0}/{Config.LevelThresholds[2]:F0}/{Config.LevelThresholds[3]:F0}/{Config.LevelThresholds[4]:F0}"
                : "0/10/30/70/90";

            string normalizedTempMode = string.Equals(tempMode, "direct", StringComparison.OrdinalIgnoreCase)
                ? "DIRECT"
                : "TEMPORAL AURA";
            float legacyDirectDrain = level >= 5
                ? Config.StabilityDrainLevel5PerMin / 60f
                : 0f;

            string buzzStatus = !Config.EnableFlyBuzzSounds
                ? "DISABLED"
                : buzzLevel < 4
                    ? "IDLE (< L4)"
                    : $"WAITING (L{buzzLevel})";
            string buzzEtaText = buzzEta < 0f ? "-" : $"{buzzEta:F1}s";
            string buzzLastText = string.IsNullOrWhiteSpace(buzzLast) ? "-" : buzzLast;
            int probeActive = attrs.GetInt("stench:probeactive", 0);
            string probePhase = attrs.GetString("stench:probephase", "-");
            float probeSeconds = attrs.GetFloat("stench:probeseconds", 0f);
            string probeEntitySel = attrs.GetString("stench:probeentitysel", "-");
            string probeEntityPos = attrs.GetString("stench:probeentitypos", "-");
            string probeBlockSel = attrs.GetString("stench:probeblocksel", "-");
            string probeRayEntity = attrs.GetString("stench:proberayentity", "-");
            string probeRayBlock = attrs.GetString("stench:proberayblock", "-");
            string probeRayDistance = attrs.GetString("stench:proberaydistance", "-");
            string probeWashTarget = attrs.GetString("stench:probewashtarget", "-");
            string probeWashValue = attrs.GetString("stench:probewashvalue", "-");
            string probeWashLevel = attrs.GetString("stench:probewashlevel", "-");
            string probeWashApplied = attrs.GetString("stench:probewashapplied", "-");
            string probeWashFlow = attrs.GetString("stench:probewashflow", "-");

            string text = "[STENCH DEBUG]";

            if (ClientConfig.ShowDebugCore)
            {
                text +=
                    $"\n─ Core ───────────────────\n" +
                    $"Value:         {value:F1} / 100\n" +
                    $"Level:         {level} / 5  ({levelName})\n" +
                    $"Gain/s:        +{gainRate:F3}\n" +
                    $"Reduction/s:   -{reductionRate:F3}\n" +
                    $"Net/s:         {(netRate >= 0 ? "+" : "")}{netRate:F3}\n" +
                    $"Thresholds:    {thresholdProfile}";
            }

            if (ClientConfig.ShowDebugSources)
            {
                text +=
                    $"\n─ Sources ─────────────────\n" +
                    $"Clothing mult: ×{clothingMult:F2}  ({clothingCat})\n" +
                    $"Swimming:      {(swimming ? "YES" : "NO")}\n" +
                    $"In water:      {(inLiquid  ? "YES" : "NO")}\n" +
                    $"Raining:       {(isRaining ? "YES" : "NO")}\n" +
                    $"Base gain/s:   {Config.BaseGainPerSecond:F3}\n" +
                    $"Max gain/s:    {Config.MaxGainPerSecond:F3}\n" +
                    $"Animal mode:   {(Config.EnableAnimalSeekingRangeModifier ? "GLOBAL animalSeekingRange" : "LEGACY per-animal AI")}\n" +
                    $"Animal target: ×{animalSeekTarget:F2}\n" +
                    $"Animal delta:  {(animalSeekDelta >= 0 ? "+" : "")}{animalSeekDelta:F2}\n" +
                    $"Animal final:  ×{animalSeekFinal:F2}\n" +
                    $"─ Integrations ─────────────\n" +
                    $"Vigor active:  {(vigorActive ? "YES" : "NO")}\n" +
                    $"HoD active:    {(hodActive   ? "YES" : "NO")}";
            }

            if (ClientConfig.ShowDebugEffects)
            {
                text +=
                    $"\n─ Effects ──────────────────\n" +
                    $"Temp enabled:  {(tempEnabled != 0 ? "YES" : "NO")}\n" +
                    $"Temp mode:     {normalizedTempMode}\n" +
                    $"Aura active:   {(tempAuraActive ? "YES" : "NO")}\n" +
                    $"Aura radius:   {tempAuraRadius:F1}\n" +
                    $"Aura strength: {tempAuraStrength:F4}\n" +
                    $"Aura floor:    {tempAuraFloor:F4}\n" +
                    $"Aura penalty:  -{tempAuraPenalty:F4}\n" +
                    $"Aura nearby:   {tempAuraNearby}\n" +
                    $"Aura base:     {tempAuraBase:F4}\n" +
                    $"Aura final:    {tempAuraFinal:F4}\n" +
                    $"Own stability: {ownStability:F4}\n" +
                    $"Top-up/s:      {tempTopupRate:F4}\n" +
                    $"Total drain/s: {tempTotalDrainRate:F4}\n" +
                    $"Direct L5/s:   {legacyDirectDrain:F4}\n" +
                    $"Direct L5/min: {Config.StabilityDrainLevel5PerMin:F2}\n" +
                    $"Show bar:      {(ClientConfig.ShowBar ? "YES" : "NO")}\n" +
                    $"Show overlay:  {(ClientConfig.ShowOverlay ? "YES" : "NO")}\n" +
                    $"Particles:     {(Config.ShowParticles && level >= 3 ? $"YES (L{level})" : "NO")}\n" +
                    $"Buzz audio:    {(Config.EnableFlyBuzzSounds ? $"YES (first: L4 20-45s, L5 4-8s; loop: L4 {Config.FlyBuzzLevel4MinSeconds:F0}-{Config.FlyBuzzLevel4MaxSeconds:F0}s, L5 {Config.FlyBuzzLevel5MinSeconds:F0}-{Config.FlyBuzzLevel5MaxSeconds:F0}s)" : "NO")}\n" +
                    $"Buzz state:    {buzzStatus}\n" +
                    $"Buzz ETA:      {buzzEtaText}\n" +
                    $"Buzz last:     {buzzLastText}";
            }

            if (ClientConfig.ShowDebugWateringCanProbe)
            {
                text +=
                    $"\n─ Watering Can Probe ─────\n" +
                    $"Probe active:  {(probeActive != 0 ? "YES" : "NO")}\n" +
                    $"Probe phase:   {probePhase}\n" +
                    $"Probe secs:    {probeSeconds:F2}\n" +
                    $"entitySel:     {probeEntitySel}\n" +
                    $"entitySel pos: {probeEntityPos}\n" +
                    $"blockSel:      {probeBlockSel}\n" +
                    $"Ray entity:    {probeRayEntity}\n" +
                    $"Ray block:     {probeRayBlock}\n" +
                    $"Ray dist:      {probeRayDistance}\n" +
                    $"Wash flow:     {probeWashFlow}\n" +
                    $"Wash target:   {probeWashTarget}\n" +
                    $"Target value:  {probeWashValue}\n" +
                    $"Target level:  {probeWashLevel}\n" +
                    $"Wash applied:  {probeWashApplied}";
            }

            // Bounds: top-right corner, anchored to right-top alignment
            ElementBounds dialogBounds = ElementStdBounds
                .AutosizedMainDialog
                .WithAlignment(EnumDialogArea.RightTop)
                .WithFixedOffset(-Margin + ClientConfig.DebugOffsetXManual, Margin + ClientConfig.DebugOffsetYManual);

            ElementBounds textBounds = ElementBounds.Fixed(0, 0, PanelWidth, PanelHeight);

            SingleComposer = capi.Gui
                .CreateCompo("stench:debug", dialogBounds)
                .AddDynamicText(text,
                    CairoFont.WhiteSmallText().WithFontSize(10f),
                    textBounds,
                    "debugtext")
                .Compose();
            composerBuilt = true;
        }

        private void EnsureOpen()
        {
            if (ClientConfig.ShowDebugOverlay)
            {
                TryOpen();
            }
            else
            {
                TryClose();
            }
        }

        public void ReloadFromConfig()
        {
            updateAccum = 0.0;
            EnsureOpen();

            if (!ClientConfig.ShowDebugOverlay)
            {
                if (composerBuilt)
                {
                    SingleComposer?.Dispose();
                    composerBuilt = false;
                }

                return;
            }

            if (TryBindPlayer())
            {
                RebuildComposer();
            }
        }

        private bool TryBindPlayer()
        {
            Entity? player = capi.World?.Player?.Entity;
            if (player == null)
                return false;

            if (ReferenceEquals(boundPlayer, player))
                return true;

            boundPlayer = player;
            return true;
        }
    }
}
