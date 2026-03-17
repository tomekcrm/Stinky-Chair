using System;
using StenchMod.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace StenchMod.Client
{
    /// <summary>
    /// HUD element that renders a coloured stench bar above the health bar.
    /// Inherits from <see cref="HudElement"/> which extends <see cref="GuiDialog"/>,
    /// giving it an always-visible, non-interactive overlay.
    /// </summary>
    public class StenchHudRenderer : HudElement
    {
        private const double StatsBarParentWidth = 850.0;
        private const double StatsBarParentHeight = 10.0;
        private const double BarWidth  = StatsBarParentWidth * 0.41;
        private const double BarHeight = 10.0;
        private const double BaseBarOffsetX = -2.0;
        private const double BaseBarOffsetY = 186.0;
        private const double HudSlotSpacingY = 22.0;

        private int   currentLevel = 1;
        private float currentValue = 0f;
        private Entity? boundPlayer;
        private bool composerBuilt;
        private GuiElementStatbar? continuousStatbar;
        private GuiElementStatbar? pivotStatbar;

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
        /// Creates the stench HUD bar and immediately opens it.
        /// Registers a listener on the local player's WatchedAttributes so the
        /// bar redraws whenever the server sends a new stench level.
        /// </summary>
        public StenchHudRenderer(ICoreClientAPI capi) : base(capi)
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

        private void OnStenchChanged()
        {
            int previousLevel = currentLevel;
            if (!RefreshFromPlayer())
                return;

            EnsureOpen();
            if (ClientConfig.ShowBar && !composerBuilt)
            {
                RebuildComposer();
                return;
            }

            if (ClientConfig.ShowBar && previousLevel != currentLevel && !ClientConfig.SegmentedBar)
            {
                RebuildComposer();
                return;
            }

            if (ClientConfig.ShowBar && ClientConfig.SegmentedBar && PivotNeedsRebuild(previousLevel))
            {
                RebuildComposer();
                return;
            }

            RefreshStatbar();
        }

        private bool TryBindPlayer()
        {
            Entity? player = capi.World?.Player?.Entity;
            if (player == null)
                return false;

            if (ReferenceEquals(boundPlayer, player))
                return true;

            boundPlayer = player;
            boundPlayer.WatchedAttributes.RegisterModifiedListener("stench:level", OnStenchChanged);
            boundPlayer.WatchedAttributes.RegisterModifiedListener("stench:value", OnStenchChanged);
            RefreshFromPlayer();
            return true;
        }

        private bool RefreshFromPlayer()
        {
            if (boundPlayer == null && !TryBindPlayer())
                return false;

            if (boundPlayer == null)
                return false;

            currentLevel = boundPlayer.WatchedAttributes.GetInt("stench:level", 1);
            currentValue = boundPlayer.WatchedAttributes.GetFloat("stench:value", 0f);
            return true;
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
                continuousStatbar = null;
                pivotStatbar = null;
            }

            if (!ClientConfig.ShowBar)
            {
                return;
            }

            ElementBounds statsBarBounds = new ElementBounds()
            {
                Alignment = EnumDialogArea.CenterBottom,
                BothSizing = ElementSizing.Fixed,
                fixedWidth = StatsBarParentWidth,
                fixedHeight = StatsBarParentHeight
            };

            ElementBounds dialogBounds = statsBarBounds.FlatCopy()
                .FixedGrow(0.0, BarHeight)
                .WithFixedOffset(0.0, -(GetDefaultBarOffsetY() + ClientConfig.BarOffsetYManual))
                .WithFixedAlignmentOffset(GetDefaultBarOffsetX() + ClientConfig.BarOffsetXManual, 0.0);

            double barStartX = StatsBarParentWidth - BarWidth;
            GuiComposer composer = capi.Gui
                .CreateCompo("stench:hudbar", dialogBounds)
                .BeginChildElements(statsBarBounds);

            if (ClientConfig.SegmentedBar)
            {
                ElementBounds pivotBounds = ElementBounds.Fixed(barStartX, 0.0, BarWidth, BarHeight);
                pivotStatbar = new GuiElementStatbar(capi, pivotBounds, GetPivotBarColor(), true, false);
                composer = composer.AddInteractiveElement(pivotStatbar, "stenchbar-pivot");
            }
            else
            {
                ElementBounds barBounds = ElementBounds.Fixed(barStartX, 0.0, BarWidth, BarHeight);
                continuousStatbar = new GuiElementStatbar(capi, barBounds, GetCurrentBarColor(), true, false);
                composer = composer.AddInteractiveElement(continuousStatbar, "stenchbar");
            }

            SingleComposer = composer
                .EndChildElements()
                .Compose();
            composerBuilt = true;
            if (continuousStatbar != null)
            {
                continuousStatbar.HideWhenFull = false;
                continuousStatbar.ShouldFlash = false;
            }

            if (pivotStatbar != null)
            {
                pivotStatbar.HideWhenFull = false;
                pivotStatbar.ShouldFlash = false;
            }

            RefreshStatbar();
        }

        public void ReloadFromConfig()
        {
            EnsureOpen();

            if (!ClientConfig.ShowBar)
            {
                if (composerBuilt)
                {
                    SingleComposer?.Dispose();
                    composerBuilt = false;
                }

                return;
            }

            RefreshFromPlayer();
            RebuildComposer();
        }

        private void EnsureOpen()
        {
            if (ClientConfig.ShowBar)
            {
                TryOpen();
            }
            else
            {
                TryClose();
            }
        }

        private double[] GetBarColor(int levelIndex)
        {
            int levelIdx = Math.Clamp(levelIndex, 0, Config.BarColors.Length - 1);
            return ColorUtil.ToRGBADoubles(Config.BarColors[levelIdx]);
        }

        private double[] GetCurrentBarColor()
        {
            return GetBarColor(currentLevel - 1);
        }

        private bool PivotNeedsRebuild(int previousLevel)
        {
            return previousLevel != currentLevel;
        }

        private int GetPivotColorIndex(int level)
        {
            float pivot = GetThresholds()[2];
            if (currentValue <= pivot)
            {
                return Math.Clamp(level <= 1 ? 0 : 1, 0, Config.BarColors.Length - 1);
            }

            return Math.Clamp(level - 1, 2, Config.BarColors.Length - 1);
        }

        private double[] GetPivotBarColor()
        {
            return GetBarColor(GetPivotColorIndex(currentLevel));
        }

        private double GetDefaultBarOffsetX()
        {
            return BaseBarOffsetX;
        }

        private double GetDefaultBarOffsetY()
        {
            double offsetY = BaseBarOffsetY;

            if (capi.ModLoader.IsModEnabled("hydrateordiedrate"))
            {
                offsetY -= HudSlotSpacingY;
            }

            if (capi.ModLoader.IsModEnabled("vigor"))
            {
                offsetY -= HudSlotSpacingY;
            }

            return offsetY;
        }

        private float[] GetThresholds()
        {
            float[] thresholds = Config.LevelThresholds;
            if (thresholds == null || thresholds.Length != 5)
            {
                return new[] { 0f, 10f, 30f, 70f, 90f };
            }

            return thresholds;
        }

        private void RefreshStatbar()
        {
            if (continuousStatbar == null && pivotStatbar == null)
            {
                return;
            }

            if (pivotStatbar != null)
            {
                float[] thresholds = GetThresholds();
                float pivot = thresholds[2];

                pivotStatbar.HideWhenFull = false;
                pivotStatbar.ShouldFlash = false;

                if (currentValue <= pivot)
                {
                    pivotStatbar.SetValues(pivot - currentValue, 0f, pivot);
                }
                else
                {
                    pivotStatbar.SetValues(currentValue - pivot, 0f, 100f - pivot);
                }
            }

            if (continuousStatbar != null)
            {
                continuousStatbar.HideWhenFull = false;
                continuousStatbar.ShouldFlash = false;
                continuousStatbar.SetValues(currentValue, 0f, 100f);
            }
        }
    }
}
