using System;

namespace StenchMod.Config
{
    /// <summary>
    /// Client-only UI settings for the Stench mod.
    /// Stored separately from gameplay config so Mods Settings can expose
    /// visibility and position controls without mixing them with server logic.
    /// </summary>
    public class StenchClientConfig
    {
        public int UiConfigSchemaVersion = 6;

        /// <summary>Whether the stench HUD bar is shown.</summary>
        public bool ShowBar = true;

        /// <summary>
        /// Whether the HUD bar uses the clean/dirty pivot mode.
        /// Internal field name kept for backwards compatibility with older client configs.
        /// </summary>
        public bool SegmentedBar = true;

        /// <summary>Whether the full-screen grime overlay is rendered.</summary>
        public bool ShowOverlay = true;

        /// <summary>Whether the debug overlay should be visible on this client.</summary>
        public bool ShowDebugOverlay = true;

        /// <summary>
        /// Horizontal offset relative to the default HydrateOrDiedrate-style anchor.
        /// Positive values move the bar further right.
        /// </summary>
        public int BarOffsetXManual = 0;

        /// <summary>
        /// Vertical offset relative to the default HydrateOrDiedrate-style slot.
        /// Negative values move the bar upward, positive values move it downward.
        /// </summary>
        public int BarOffsetYManual = 0;

        /// <summary>
        /// Horizontal offset relative to the default top-right debug anchor.
        /// Positive values move the panel to the right.
        /// </summary>
        public int DebugOffsetXManual = 0;

        /// <summary>
        /// Vertical offset relative to the default top-right debug anchor.
        /// Positive values move the panel downward.
        /// </summary>
        public int DebugOffsetYManual = 0;

        public bool Normalize()
        {
            bool changed = false;

            if (UiConfigSchemaVersion < 2)
            {
                if (BarOffsetYManual != 0)
                {
                    BarOffsetYManual -= 72;
                }

                UiConfigSchemaVersion = 2;
                changed = true;
            }

            if (UiConfigSchemaVersion < 3)
            {
                if (BarOffsetXManual != 0 || BarOffsetYManual != 0)
                {
                    BarOffsetXManual = 0;
                    BarOffsetYManual = 0;
                }

                UiConfigSchemaVersion = 3;
                changed = true;
            }

            if (UiConfigSchemaVersion < 4)
            {
                if (BarOffsetYManual == 0)
                {
                    BarOffsetYManual = 90;
                    changed = true;
                }

                UiConfigSchemaVersion = 4;
                changed = true;
            }

            if (UiConfigSchemaVersion < 5)
            {
                if (BarOffsetYManual == 0)
                {
                    BarOffsetYManual = 90;
                    changed = true;
                }

                UiConfigSchemaVersion = 5;
                changed = true;
            }

            if (UiConfigSchemaVersion < 6)
            {
                // The default HUD slot is now baked into the layout code instead
                // of living in a magic +90 client offset. Preserve existing custom
                // placements, but leave 0 untouched because that value may come
                // from a broken ConfigLib "Default" reset and should now be valid.
                if (BarOffsetYManual != 0)
                {
                    BarOffsetYManual -= 90;
                    changed = true;
                }

                UiConfigSchemaVersion = 6;
                changed = true;
            }

            changed |= Clamp(ref BarOffsetXManual, -800, 800);
            changed |= Clamp(ref BarOffsetYManual, -400, 400);
            changed |= Clamp(ref DebugOffsetXManual, -1200, 1200);
            changed |= Clamp(ref DebugOffsetYManual, -400, 1200);

            return changed;
        }

        private static bool Clamp(ref int value, int min, int max)
        {
            int clamped = Math.Clamp(value, min, max);
            if (clamped == value)
            {
                return false;
            }

            value = clamped;
            return true;
        }
    }
}
