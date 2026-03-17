using System;
using System.Text;
using StenchMod.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace StenchMod.BlockEntities
{
    /// <summary>
    /// Block entity for the barrel shower. Stores liquid (water) and tracks
    /// how much remains for stench washing. Liquid is stored as an ItemStack
    /// using VS's water-tight containable system, matching vanilla barrel conventions.
    /// </summary>
    public class BEBarrelShower : BlockEntityContainer, IRenderer
    {
        private const string AttrLiquidCode = "liquidCode";
        private const string AttrCurrentLitres = "currentLitres";
        private const string AttrLiquidAttributes = "liquidAttribs";
        private const string AttrIsActive = "isActive";

        private InventoryGeneric inventory = null!;
        private bool inventoryHasApi;
        private string? inventoryId;
        private ILoadedSound? showerSound;

        // -----------------------------------------------------------------------
        // IRenderer — water level visual
        // -----------------------------------------------------------------------
        private MeshRef? liquidMeshRef;
        private int liquidAtlasTextureId;
        private readonly Matrixf modelMat = new Matrixf();

        public double RenderOrder => 0.5;
        public int RenderRange => 24;

        /// <summary>Current liquid fill in litres.</summary>
        public float CurrentLitres { get; private set; }

        /// <summary>Maximum capacity in litres, read from block attributes.</summary>
        public float CapacityLitres => Block?.Attributes?["capacityLitres"].AsFloat(50f) ?? 50f;

        /// <summary>Fills as a 0–1 fraction.</summary>
        public float FillRatio => CapacityLitres > 0f ? CurrentLitres / CapacityLitres : 0f;

        /// <summary>The liquid's AssetLocation code (e.g. "game:waterportion"), or null when empty.</summary>
        public AssetLocation? LiquidCode { get; private set; }

        /// <summary>Whether the shower is currently running (valve turned on).</summary>
        public bool IsActive { get; private set; }

        /// <summary>Toggles the shower on/off. Returns the new state.</summary>
        public bool ToggleActive()
        {
            IsActive = !IsActive;
            MarkDirty(true);
            return IsActive;
        }

        // -----------------------------------------------------------------------
        // BlockEntityContainer / BlockEntity overrides
        // -----------------------------------------------------------------------

        public override InventoryBase Inventory
        {
            get
            {
                EnsureInventory(Api);
                return inventory;
            }
        }
        public override string InventoryClassName => "barrelshower";

        public override void Initialize(ICoreAPI api)
        {
            EnsureInventory(api);
            base.Initialize(api);
            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                UpdateSound();
                BuildLiquidMesh(capi);
                capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "barrelshower-liquid");
            }
            else
            {
                RegisterGameTickListener(OnShowerParticleTick, 200);
            }
        }

        private void OnShowerParticleTick(float dt)
        {
            if (!IsActive || CurrentLitres <= 0f) return;
            StenchWashParticleSystem.SpawnShower(Api.World, Pos);
            TakeLiquid(0.0833f); // ~0.42 L/s — 50 L lasts ~2 min
        }

        private void OnSlotModified(int slotId)
        {
            MarkDirty(true);
        }

        private void UpdateSound()
        {
            if (Api is not ICoreClientAPI capi) return;

            if (IsActive && showerSound == null)
            {
                showerSound = capi.World.LoadSound(new SoundParams
                {
                    Location        = new AssetLocation("stench:sounds/block/shower-running"),
                    Position        = new Vec3f(Pos.X + 0.5f, Pos.Y + 1.5f, Pos.Z + 0.5f),
                    ShouldLoop      = true,
                    DisposeOnFinish = false,
                    Volume          = 0.8f,
                    Range           = 12f,
                });
                showerSound?.Start();
            }
            else if (!IsActive && showerSound != null)
            {
                showerSound.Stop();
                showerSound.Dispose();
                showerSound = null;
            }
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (stage != EnumRenderStage.Opaque || liquidMeshRef == null || CurrentLitres <= 0f || Api == null) return;

            ICoreClientAPI capi = (ICoreClientAPI)Api;
            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            // Mesh vertices are at Y = 0. Translate directly to the barrel interior:
            //   Empty  → ~2.3 blocks above base block (just above barrel floor)
            //   Full   → ~2.85 blocks above base block (near barrel top)
            double yWorld = Pos.Y - camPos.Y + 2.3 + FillRatio * 0.55;

            rpi.GlDisableCullFace();

            IStandardShaderProgram prog = rpi.PreparedStandardShader(Pos.X, Pos.Y + 2, Pos.Z);
            prog.Use();
            prog.RgbaGlowIn = new Vec4f(0, 0, 0, 0);
            prog.NormalShaded = 0;
            prog.ExtraGlow = 0;
            prog.AlphaTest = 0f;
            prog.Tex2D = liquidAtlasTextureId;
            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn   = new Vec4f(1f, 1f, 1f, 1f);
            prog.ModelMatrix = modelMat
                .Identity()
                .Translate(Pos.X - camPos.X, yWorld, Pos.Z - camPos.Z)
                .Values;
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.RenderMesh(liquidMeshRef);
            prog.Stop();
            rpi.GlEnableCullFace();
        }

        public void Dispose()
        {
            liquidMeshRef?.Dispose();
            liquidMeshRef = null;
        }

        private void BuildLiquidMesh(ICoreClientAPI capi)
        {
            // Look up the water texture UV directly from the block texture atlas.
            // ShapeTextureSource fails for animated liquid textures; the indexer works.
            TextureAtlasPosition? waterPos =
                capi.BlockTextureAtlas[new AssetLocation("stench:block/liquid/barrelwater")];

            float u1, u2, v1, v2;
            int atlasTextureId;
            if (waterPos != null)
            {
                u1 = waterPos.x1; u2 = waterPos.x2;
                v1 = waterPos.y1; v2 = waterPos.y2;
                atlasTextureId = capi.BlockTextureAtlas.AtlasTextures[waterPos.atlasNumber].TextureId;
            }
            else
            {
                // Fallback: vanilla water-still — always present in the block atlas
                TextureAtlasPosition? fallbackPos =
                    capi.BlockTextureAtlas[new AssetLocation("game:block/liquid/water-still")];
                u1 = fallbackPos?.x1 ?? 0f; u2 = fallbackPos?.x2 ?? 0.0625f;
                v1 = fallbackPos?.y1 ?? 0f; v2 = fallbackPos?.y2 ?? 0.0625f;
                int fallbackAtlas = fallbackPos?.atlasNumber ?? 0;
                atlasTextureId = capi.BlockTextureAtlas.AtlasTextures[fallbackAtlas].TextureId;
            }

            // Flat quad covering barrel interior opening (2/16 to 14/16 on X and Z).
            // Y = 0 here; model matrix in OnRenderFrame positions it at the right height.
            const float x1 = 2f / 16f, x2 = 14f / 16f;
            const float z1 = 2f / 16f, z2 = 14f / 16f;

            const int liquidColor = unchecked((int)0xFFFFFFFF);
            const int liquidFlags = 0;

            // Standard/world shaders expect the usual block vertex payload. When rgba/flags
            // are omitted, animated atlas textures can render as black in custom meshes.
            MeshData mesh = new MeshData(4);
            mesh.AddVertexWithFlags(x1, 0f, z1, u1, v1, liquidColor, liquidFlags);
            mesh.AddVertexWithFlags(x2, 0f, z1, u2, v1, liquidColor, liquidFlags);
            mesh.AddVertexWithFlags(x2, 0f, z2, u2, v2, liquidColor, liquidFlags);
            mesh.AddVertexWithFlags(x1, 0f, z2, u1, v2, liquidColor, liquidFlags);
            mesh.AddIndices(0, 1, 2, 0, 2, 3);

            liquidMeshRef = capi.Render.UploadMesh(mesh);
            liquidAtlasTextureId = atlasTextureId;
        }

        public override void OnBlockRemoved()
        {
            if (Api?.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)Api;
                capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
                liquidMeshRef?.Dispose();
                liquidMeshRef = null;
            }
            showerSound?.Stop();
            showerSound?.Dispose();
            showerSound = null;
            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            if (Api?.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)Api;
                capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
                liquidMeshRef?.Dispose();
                liquidMeshRef = null;
            }
            showerSound?.Stop();
            showerSound?.Dispose();
            showerSound = null;
            base.OnBlockUnloaded();
        }

        // -----------------------------------------------------------------------
        // Liquid API — called by BlockBarrelShower
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns true if this shower can accept any more liquid.
        /// </summary>
        public bool CanAcceptLiquid => CurrentLitres < CapacityLitres - 0.001f;

        /// <summary>
        /// Attempts to add liquid from a container item.  Validates that it is
        /// water (or matches existing liquid) before accepting it.
        /// </summary>
        /// <param name="liquidStack">ItemStack representing the liquid portion.</param>
        /// <param name="desiredLitres">Amount the caller wants to transfer.</param>
        /// <returns>Actual litres accepted (≤ desiredLitres).</returns>
        public float TryAddLiquid(ItemStack liquidStack, float desiredLitres)
        {
            if (liquidStack == null || desiredLitres <= 0f) return 0f;

            AssetLocation incomingCode = liquidStack.Collectible.Code;

            // Allow only water (game:waterportion), or matching liquid if already filled.
            if (LiquidCode != null && !LiquidCode.Equals(incomingCode)) return 0f;
            if (LiquidCode == null && !IsWater(incomingCode)) return 0f;

            float space = CapacityLitres - CurrentLitres;
            float accepted = Math.Min(desiredLitres, space);
            if (accepted <= 0f) return 0f;

            LiquidCode = incomingCode;
            CurrentLitres += accepted;
            MarkDirty(true);
            return accepted;
        }

        /// <summary>
        /// Removes up to <paramref name="litres"/> from the shower.
        /// </summary>
        /// <returns>Actual litres removed.</returns>
        public float TakeLiquid(float litres)
        {
            float taken = Math.Min(CurrentLitres, litres);
            CurrentLitres -= taken;
            if (CurrentLitres <= 0.001f)
            {
                CurrentLitres = 0f;
                LiquidCode = null;
                if (IsActive)
                {
                    IsActive = false;
                    int idx = Api.World.Rand.Next(3);
                    Api?.World.PlaySoundAt(
                        new AssetLocation($"stench:sounds/block/shower-empty{idx + 1}"),
                        Pos.X + 0.5, Pos.Y + 1.5, Pos.Z + 0.5,
                        null, randomizePitch: true, range: 16f, volume: 1f);
                }
            }
            MarkDirty(true);
            return taken;
        }

        /// <summary>
        /// Consumes a small amount of water when washing occurs.
        /// Harmlessly clamps at zero.
        /// </summary>
        public void ConsumeWater(float litres)
        {
            TakeLiquid(litres);
        }

        // -----------------------------------------------------------------------
        // Serialisation
        // -----------------------------------------------------------------------

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat(AttrCurrentLitres, CurrentLitres);
            tree.SetString(AttrLiquidCode, LiquidCode?.ToString() ?? string.Empty);
            tree.SetBool(AttrIsActive, IsActive);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            EnsureInventory(worldAccessForResolve as ICoreAPI);
            base.FromTreeAttributes(tree, worldAccessForResolve);
            CurrentLitres = tree.GetFloat(AttrCurrentLitres, 0f);
            string codeStr = tree.GetString(AttrLiquidCode, string.Empty);
            LiquidCode = string.IsNullOrEmpty(codeStr) ? null : new AssetLocation(codeStr);
            IsActive = tree.GetBool(AttrIsActive, false);
            if (Api?.Side == EnumAppSide.Client)
                UpdateSound();
        }

        private void EnsureInventory(ICoreAPI? api)
        {
            ICoreAPI? actualApi = api ?? Api;
            if (inventory != null && (inventoryHasApi || actualApi == null))
            {
                return;
            }

            if (inventory != null)
            {
                inventory.SlotModified -= OnSlotModified;
            }

            inventory = new InventoryGeneric(1, InventoryId, actualApi);
            inventory.SlotModified += OnSlotModified;
            inventoryHasApi = actualApi != null;
        }

        private string InventoryId => inventoryId ??= $"barrelshower-{Guid.NewGuid():N}";

        // -----------------------------------------------------------------------
        // Info overlay
        // -----------------------------------------------------------------------

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            AppendInfo(dsc);
        }

        /// <summary>
        /// Builds the hover tooltip for this shower. Called from the block entity and
        /// also from part blocks that need to display the same info.
        /// </summary>
        public void AppendInfo(StringBuilder dsc)
        {
            if (CurrentLitres <= 0f)
            {
                dsc.AppendLine(Lang.Get("stench:barrelshower-empty"));
            }
            else
            {
                dsc.AppendLine(Lang.Get("stench:barrelshower-fill",
                    CurrentLitres.ToString("F1"),
                    CapacityLitres.ToString("F0")));
                string statusKey = IsActive ? "stench:barrelshower-status-on" : "stench:barrelshower-status-off";
                dsc.AppendLine(Lang.Get(statusKey));
            }

            dsc.AppendLine(Lang.Get("stench:barrelshower-hint-fill"));
            dsc.AppendLine(Lang.Get("stench:barrelshower-hint-toggle"));
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static bool IsWater(AssetLocation code)
        {
            // VS water portion code is "game:waterportion"
            return code.Domain == "game" && code.Path == "waterportion";
        }
    }
}
