using System;
using StenchMod.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace StenchMod.Client
{
    /// <summary>
    /// Full-screen dirt/grime texture overlay rendered at stench levels 4 and 5.
    /// Fades in and out smoothly based on the configured
    /// <see cref="StenchConfig.OverlayFadeSeconds"/>.
    /// </summary>
    public class StenchOverlayRenderer : HudElement
    {
        private int   textureIdLv4 = -1;
        private int   textureIdLv5 = -1;

        private float currentOpacity = 0f;
        private float targetOpacity  = 0f;

        private int   currentLevel   = 1;
        private Entity? boundPlayer;

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

        /// <summary>
        /// Creates the overlay, loads both dirt textures, and subscribes to
        /// stench level changes from the player's WatchedAttributes.
        /// </summary>
        public StenchOverlayRenderer(ICoreClientAPI capi) : base(capi)
        {
            LoadTextures();

            EnsureOpen();
        }

        public override void OnOwnPlayerDataReceived()
        {
            base.OnOwnPlayerDataReceived();
            TryBindPlayer();
            EnsureOpen();
        }

        public override void OnLevelFinalize()
        {
            base.OnLevelFinalize();
            TryBindPlayer();
            EnsureOpen();
        }

        // -------------------------------------------------------------------------
        // Texture loading
        // -------------------------------------------------------------------------

        private void LoadTextures()
        {
            try
            {
                textureIdLv4 = capi.Render.GetOrLoadTexture(
                    new AssetLocation("stench", "textures/gui/dirt_overlay_lv4.png"));
            }
            catch (Exception ex)
            {
                capi.Logger.Warning("[StenchMod] Could not load dirt_overlay_lv4.png: " + ex.Message);
            }

            try
            {
                textureIdLv5 = capi.Render.GetOrLoadTexture(
                    new AssetLocation("stench", "textures/gui/dirt_overlay_lv5.png"));
            }
            catch (Exception ex)
            {
                capi.Logger.Warning("[StenchMod] Could not load dirt_overlay_lv5.png: " + ex.Message);
            }
        }

        // -------------------------------------------------------------------------
        // Event handler
        // -------------------------------------------------------------------------

        private void OnStenchLevelChanged()
        {
            if (boundPlayer == null && !TryBindPlayer())
                return;

            if (boundPlayer == null)
                return;

            currentLevel = boundPlayer.WatchedAttributes.GetInt("stench:level", 1);

            if (!ClientConfig.ShowOverlay)
            {
                targetOpacity = 0f;
                return;
            }

            targetOpacity = currentLevel >= 5
                ? Config.OverlayOpacityLevel5
                : currentLevel >= 4
                    ? Config.OverlayOpacityLevel4
                    : 0f;
        }

        private bool TryBindPlayer()
        {
            Entity? player = capi.World?.Player?.Entity;
            if (player == null)
                return false;

            if (ReferenceEquals(boundPlayer, player))
                return true;

            boundPlayer = player;
            boundPlayer.WatchedAttributes.RegisterModifiedListener("stench:level", OnStenchLevelChanged);
            OnStenchLevelChanged();
            return true;
        }

        // -------------------------------------------------------------------------
        // Rendering
        // -------------------------------------------------------------------------

        /// <inheritdoc/>
        public override void OnRenderGUI(float deltaTime)
        {
            if (!IsOpened()) return;
            if (boundPlayer == null)
                TryBindPlayer();

            // Lerp current opacity towards target
            float fadeSpeed = deltaTime / Math.Max(Config.OverlayFadeSeconds, 0.01f);
            currentOpacity  = Lerp(currentOpacity, targetOpacity, Math.Clamp(fadeSpeed, 0f, 1f));

            if (currentOpacity < 0.005f) return;

            int texId = currentLevel >= 5
                ? (textureIdLv5 >= 0 ? textureIdLv5 : textureIdLv4)
                : textureIdLv4;

            if (texId < 0) return;

            int screenW = capi.Render.FrameWidth;
            int screenH = capi.Render.FrameHeight;

            // Render full-screen dirt texture with current opacity modulated as alpha.
            // Render2DTexture(texId, x, y, w, h, z, color) — color is RGBA where A controls opacity.
            capi.Render.Render2DTexture(
                texId,
                0f, 0f,
                (float)screenW, (float)screenH,
                50f,
                ColorUtil.ToRGBAVec4f(ColorUtil.ColorFromRgba(255, 255, 255, (int)(currentOpacity * 255)))
            );
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static float Lerp(float from, float to, float t)
            => from + (to - from) * t;

        private void EnsureOpen()
        {
            if (ClientConfig.ShowOverlay)
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
            EnsureOpen();
            OnStenchLevelChanged();
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            base.Dispose();
            // Texture handles are managed by the render system; no manual dispose needed.
        }
    }
}
