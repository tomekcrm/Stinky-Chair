using System;
using System.Collections.Generic;
using StenchMod.Systems;

namespace StenchMod.Config
{
    /// <summary>
    /// All configurable parameters for the Stench mod.
    /// Loaded from / saved to stench.json via api.LoadModConfig / api.StoreModConfig.
    /// </summary>
    public class StenchConfig
    {
        public int ConfigSchemaVersion = 14;

        // -------------------------------------------------------------------------
        // Stench gain
        // -------------------------------------------------------------------------

        /// <summary>Passive stench gain per second, regardless of activity.</summary>
        public float BaseGainPerSecond = 0.005f;

        /// <summary>Multiplier applied to stamina drain delta when Vigor is active.</summary>
        public float StaminaDrainGainMultiplier = 0.002f;

        /// <summary>Minimum movement speed (units/tick) to trigger speed-based gain (fallback without Vigor).</summary>
        public float SpeedGainThreshold = 0.01f;

        /// <summary>Gain multiplier applied to movement speed when Vigor is not active.</summary>
        public float SpeedGainMultiplier = 0.15f;

        /// <summary>Gain multiplier applied when the player is fully exhausted (stamina = 0).</summary>
        public float ExhaustedGainMultiplier = 1.15f;

        /// <summary>Hard safety cap for total stench gain per second after all activity modifiers.</summary>
        public float MaxGainPerSecond = 0.06f;

        // -------------------------------------------------------------------------
        // Clothing multipliers
        // -------------------------------------------------------------------------

        /// <summary>Stench gain multiplier when no clothing is worn.</summary>
        public float ClothingNoneMultiplier = 0.7f;

        /// <summary>Stench gain multiplier for light (cloth/fabric) clothing.</summary>
        public float ClothingLightMultiplier = 1.0f;

        /// <summary>Stench gain multiplier for medium (leather) clothing.</summary>
        public float ClothingMediumMultiplier = 1.2f;

        /// <summary>Stench gain multiplier for heavy (metal) armor.</summary>
        public float ClothingHeavyMultiplier = 1.5f;

        /// <summary>Item code substrings that classify armor as heavy.</summary>
        public List<string> HeavyArmorCodes = new List<string> { "plate", "mail", "iron", "steel", "bronze" };

        /// <summary>Item code substrings that classify armor as medium.</summary>
        public List<string> MediumArmorCodes = new List<string> { "leather" };

        // -------------------------------------------------------------------------
        // Stench reduction
        // -------------------------------------------------------------------------

        /// <summary>Stench reduction per second while swimming.</summary>
        public float SwimmingReductionPerSecond = 8.0f;

        /// <summary>Stench reduction per second while standing in liquid (feet only).</summary>
        public float WaterStandReductionPerSecond = 0.8f;

        /// <summary>Stench reduction per second while it is raining and the player is exposed to sky.</summary>
        public float RainReductionPerSecond = 1.5f;

        /// <summary>
        /// Seconds the player must be in water before cleaning begins.
        /// Prevents instant reduction from briefly touching water.
        /// </summary>
        public float WaterEntryDelaySeconds = 1.5f;

        // -------------------------------------------------------------------------
        // Level thresholds and names
        // -------------------------------------------------------------------------

        /// <summary>
        /// Lower bounds for each of the 5 stench levels (indices 0–4 map to levels 1–5).
        /// Value range is 0–100. Must be 5 entries in ascending order.
        /// </summary>
        public float[] LevelThresholds = { 0f, 10f, 30f, 70f, 90f };

        /// <summary>Display names for each of the 5 stench levels.</summary>
        public string[] LevelNames = { "Clean", "Slightly Dirty", "Smelly", "Reeking", "Repulsive" };

        // -------------------------------------------------------------------------
        // Temporal stability
        // -------------------------------------------------------------------------

        /// <summary>Master toggle for stench-driven temporal stability effects.</summary>
        public bool EnableStabilityDrain = true;

        /// <summary>
        /// Stability implementation mode. "temporalaura" hooks the vanilla
        /// temporal stability system and is the default. "direct" keeps the
        /// old direct OwnStability drain as a fallback.
        /// </summary>
        public string StabilityMode = "temporalaura";

        /// <summary>
        /// Radius of the temporal aura emitted by a level-5 dirty player.
        /// Nearby players inside this radius are affected as well.
        /// </summary>
        public float TemporalAuraRadius = 2.0f;

        /// <summary>
        /// Maximum penalty applied to vanilla temporal stability at the emitter
        /// center before radial falloff. This is tuned to be about 2x the old
        /// stock direct-drain profile while still feeling local and rift-like.
        /// </summary>
        public float TemporalAuraLevel5Penalty = 0.2668f;

        /// <summary>
        /// Minimum final local temporal stability forced by the aura at level 5.
        /// If the place is too stable, the aura will dynamically cancel positive
        /// stability down to at most this value so vanilla gear/feedback reacts.
        /// </summary>
        public float TemporalAuraMinFinalStabilityLevel5 = 0.95f;

        /// <summary>Reserved for backwards compatibility. Level 4 no longer drains sanity by default.</summary>
        public float StabilityDrainLevel4PerMin = 0f;

        /// <summary>
        /// Legacy direct-drain value used only when StabilityMode == "direct".
        /// </summary>
        public float StabilityDrainLevel5PerMin = 0.0667f;

        // -------------------------------------------------------------------------
        // Drifter AI
        // -------------------------------------------------------------------------

        /// <summary>Whether the drifter AI modification is enabled.</summary>
        public bool EnableDrifterAI = true;

        /// <summary>Entity code substrings of drifters affected by stench.</summary>
        public List<string> AffectedDrifters = new List<string>
        {
            "drifter",
            "drifter-corrupt",
            "drifter-deep",
            "drifter-surface",
            "drifter-tainted"
        };

        /// <summary>
        /// Detection range multipliers per stench level (indices 0–4 = levels 1–5).
        /// 1.0 = full range, 0.0 = no detection.
        /// </summary>
        public float[] DrifterRangeMultipliers = { 1f, 1f, 0.5f, 0.25f, 0f };

        /// <summary>
        /// Probability (0–1) that a drifter ignores the player even within detection range, per level.
        /// </summary>
        public float[] DrifterIgnoreChances = { 0f, 0f, 0.5f, 0.85f, 1f };

        // -------------------------------------------------------------------------
        // Animal AI
        // -------------------------------------------------------------------------

        /// <summary>
        /// Whether the player should drive the built-in animal detection stat
        /// animalSeekingRange. This is the preferred cross-species path because
        /// it affects any task inheriting from AiTaskBaseTargetable.
        /// </summary>
        public bool EnableAnimalSeekingRangeModifier = true;

        /// <summary>
        /// Effective animal detection multipliers by stench level (indices 0–4 =
        /// levels 1–5). Values are applied around the vanilla baseline of 1.0.
        /// The intended curve is a sweet spot at level 3, with levels 1 and 5
        /// being easier for animals to notice than default.
        /// </summary>
        public float[] AnimalSeekingRangeMultipliers = { 1.15f, 0.80f, 0.35f, 0.90f, 1.20f };

        /// <summary>
        /// Legacy animal AI wrapper. Kept only as a fallback for task-level
        /// exceptions; disabled by default once the global player stat path is on.
        /// </summary>
        public bool EnableAnimalAI = false;

        /// <summary>Entity code substrings of animals affected by stench.</summary>
        public List<string> AffectedAnimals = new List<string>
        {
            "wolf",
            "bear",
            "pig",
            "hyena"
        };

        /// <summary>Detection range multipliers per stench level for animals.</summary>
        public float[] AnimalRangeMultipliers = { 1f, 1f, 0.5f, 0.25f, 0f };

        /// <summary>Ignore chance per stench level for animals.</summary>
        public float[] AnimalIgnoreChances = { 0f, 0f, 0.5f, 0.85f, 1f };

        // -------------------------------------------------------------------------
        // Shared visual tuning
        // -------------------------------------------------------------------------

        /// <summary>ARGB colors (as int) for each of the 5 stench levels.</summary>
        public int[] BarColors = new int[]
        {
            unchecked((int)0xFF44AA44),  // Level 1 — green
            unchecked((int)0xFF88AA22),  // Level 2 — yellow-green
            unchecked((int)0xFFBBAA00),  // Level 3 — yellow
            unchecked((int)0xFFCC6600),  // Level 4 — orange
            unchecked((int)0xFF663300)   // Level 5 — brown
        };

        /// <summary>Overlay opacity at stench level 4 (0.0–1.0).</summary>
        public float OverlayOpacityLevel4 = 0.30f;

        /// <summary>Overlay opacity at stench level 5 (0.0–1.0).</summary>
        public float OverlayOpacityLevel5 = 0.65f;

        /// <summary>Seconds over which the overlay fades in or out.</summary>
        public float OverlayFadeSeconds = 2.0f;

        /// <summary>Whether stench particles are shown.</summary>
        public bool ShowParticles = true;

        /// <summary>Whether ambient fly buzz sounds are emitted at high stench levels.</summary>
        public bool EnableFlyBuzzSounds = true;

        /// <summary>Minimum seconds between fly buzz one-shots at stench level 4.</summary>
        public float FlyBuzzLevel4MinSeconds = 60f;

        /// <summary>Maximum seconds between fly buzz one-shots at stench level 4.</summary>
        public float FlyBuzzLevel4MaxSeconds = 135f;

        /// <summary>Minimum seconds between fly buzz one-shots at stench level 5.</summary>
        public float FlyBuzzLevel5MinSeconds = 20f;

        /// <summary>Maximum seconds between fly buzz one-shots at stench level 5.</summary>
        public float FlyBuzzLevel5MaxSeconds = 45f;

        /// <summary>Audible range of the fly buzz sounds.</summary>
        public float FlyBuzzRange = 24f;

        /// <summary>Base volume of the fly buzz sounds at stench level 4.</summary>
        public float FlyBuzzLevel4Volume = 1.0f;

        /// <summary>Base volume of the fly buzz sounds at stench level 5.</summary>
        public float FlyBuzzLevel5Volume = 1.25f;

        // -------------------------------------------------------------------------
        // Debug
        // -------------------------------------------------------------------------

        /// <summary>
        /// Enables extra server-side WatchedAttributes used by the debug overlay.
        /// The overlay visibility itself is now controlled by the client config.
        /// </summary>
        public bool DebugMode = false;

        public bool NormalizeAndMigrate()
        {
            bool changed = false;

            if (ConfigSchemaVersion < 2)
            {
                // Reset known too-aggressive defaults from earlier 1.1/1.2 builds.
                StaminaDrainGainMultiplier = 0.002f;
                ExhaustedGainMultiplier = 1.15f;
                MaxGainPerSecond = 0.06f;
                StabilityDrainLevel4PerMin = 0f;
                StabilityDrainLevel5PerMin = 0.0333f;
                ConfigSchemaVersion = 2;
                changed = true;
            }

            if (ConfigSchemaVersion < 3)
            {
                // Keep existing user choice for DebugMode, but advance schema so
                // later builds can rely on the lighter logging/sync policy.
                ConfigSchemaVersion = 3;
                changed = true;
            }

            if (ConfigSchemaVersion < 4)
            {
                StabilityDrainLevel4PerMin = 0f;
                ConfigSchemaVersion = 4;
                changed = true;
            }

            if (ConfigSchemaVersion < 5)
            {
                if (AffectedDrifters.RemoveAll(code =>
                        code.Equals("bowtorn", StringComparison.OrdinalIgnoreCase) ||
                        code.Equals("shiver", StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    changed = true;
                }

                ConfigSchemaVersion = 5;
                changed = true;
            }

            if (ConfigSchemaVersion < 6)
            {
                if (MatchesThresholdProfile(LevelThresholds, 0f, 20f, 40f, 60f, 80f))
                {
                    LevelThresholds = new[] { 0f, 10f, 30f, 70f, 90f };
                    changed = true;
                }

                ConfigSchemaVersion = 6;
                changed = true;
            }

            if (ConfigSchemaVersion < 7)
            {
                if (EnableAnimalAI)
                {
                    EnableAnimalAI = false;
                    changed = true;
                }

                ConfigSchemaVersion = 7;
                changed = true;
            }

            if (ConfigSchemaVersion < 8)
            {
                changed |= MigrateLegacyFloat(ref FlyBuzzRange, 18f, 24f);
                changed |= MigrateLegacyFloat(ref FlyBuzzLevel4Volume, 0.55f, 1.0f);
                changed |= MigrateLegacyFloat(ref FlyBuzzLevel5Volume, 0.75f, 1.25f);

                ConfigSchemaVersion = 8;
                changed = true;
            }

            if (ConfigSchemaVersion < 9)
            {
                if (string.IsNullOrWhiteSpace(StabilityMode))
                {
                    StabilityMode = "temporalaura";
                    changed = true;
                }

                changed |= MigrateLegacyFloat(ref StabilityDrainLevel5PerMin, 0.0333f, 0.0667f);

                ConfigSchemaVersion = 9;
                changed = true;
            }

            if (ConfigSchemaVersion < 10)
            {
                changed |= MigrateLegacyFloat(ref TemporalAuraLevel5Penalty, 0.0667f, 0.1334f);

                ConfigSchemaVersion = 10;
                changed = true;
            }

            if (ConfigSchemaVersion < 11)
            {
                changed |= MigrateLegacyFloat(ref TemporalAuraLevel5Penalty, 0.1334f, 0.2668f);

                ConfigSchemaVersion = 11;
                changed = true;
            }

            if (ConfigSchemaVersion < 12)
            {
                changed |= MigrateLegacyFloat(ref TemporalAuraLevel5Penalty, 0.2668f, 0.5336f);

                ConfigSchemaVersion = 12;
                changed = true;
            }

            if (ConfigSchemaVersion < 13)
            {
                changed |= MigrateLegacyFloat(ref TemporalAuraLevel5Penalty, 0.5336f, 0.2668f);

                if (TemporalAuraMinFinalStabilityLevel5 <= 0f)
                {
                    TemporalAuraMinFinalStabilityLevel5 = 0.95f;
                    changed = true;
                }

                ConfigSchemaVersion = 13;
                changed = true;
            }

            if (ConfigSchemaVersion < 14)
            {
                changed |= MigrateLegacyFloat(ref TemporalAuraLevel5Penalty, 0.5336f, 0.2668f);

                if (TemporalAuraMinFinalStabilityLevel5 <= 0f)
                {
                    TemporalAuraMinFinalStabilityLevel5 = 0.95f;
                    changed = true;
                }

                ConfigSchemaVersion = 14;
                changed = true;
            }

            changed |= MigrateLegacyFloat(ref BaseGainPerSecond, 0.5f, 0.005f);
            changed |= MigrateLegacyFloat(ref StaminaDrainGainMultiplier, 2.0f, 0.002f);
            changed |= MigrateLegacyFloat(ref StaminaDrainGainMultiplier, 0.01f, 0.002f);
            changed |= MigrateLegacyFloat(ref StaminaDrainGainMultiplier, 0.003f, 0.002f);
            changed |= MigrateLegacyFloat(ref SpeedGainMultiplier, 1.5f, 0.15f);
            changed |= MigrateLegacyFloat(ref ExhaustedGainMultiplier, 2.5f, 1.15f);
            changed |= MigrateLegacyFloat(ref ExhaustedGainMultiplier, 1.25f, 1.15f);
            changed |= MigrateLegacyFloat(ref MaxGainPerSecond, 0.10f, 0.06f);
            changed |= MigrateLegacyFloat(ref StabilityDrainLevel4PerMin, 0.5f, 0f);
            changed |= MigrateLegacyFloat(ref StabilityDrainLevel4PerMin, 0.10f, 0f);
            changed |= MigrateLegacyFloat(ref StabilityDrainLevel5PerMin, 2.0f, 0.0667f);
            changed |= MigrateLegacyFloat(ref StabilityDrainLevel5PerMin, 0.40f, 0.0667f);

            if (StaminaDrainGainMultiplier > 0.01f)
            {
                StaminaDrainGainMultiplier = 0.002f;
                changed = true;
            }

            if (ExhaustedGainMultiplier > 1.5f)
            {
                ExhaustedGainMultiplier = 1.15f;
                changed = true;
            }

            if (MaxGainPerSecond > 0.06f)
            {
                MaxGainPerSecond = 0.06f;
                changed = true;
            }

            if (StabilityDrainLevel4PerMin != 0f)
            {
                StabilityDrainLevel4PerMin = 0f;
                changed = true;
            }

            StabilityMode = StenchTemporalAuraSystem.NormalizeMode(StabilityMode);

            if (TemporalAuraRadius <= 0f)
            {
                TemporalAuraRadius = 2.0f;
                changed = true;
            }

            if (TemporalAuraLevel5Penalty <= 0f)
            {
                TemporalAuraLevel5Penalty = 0.2668f;
                changed = true;
            }

            if (TemporalAuraMinFinalStabilityLevel5 <= 0f)
            {
                TemporalAuraMinFinalStabilityLevel5 = 0.95f;
                changed = true;
            }

            if (TemporalAuraRadius > 4f)
            {
                TemporalAuraRadius = 4f;
                changed = true;
            }

            if (TemporalAuraLevel5Penalty > 0.60f)
            {
                TemporalAuraLevel5Penalty = 0.60f;
                changed = true;
            }

            if (TemporalAuraMinFinalStabilityLevel5 < 0.50f)
            {
                TemporalAuraMinFinalStabilityLevel5 = 0.50f;
                changed = true;
            }

            if (TemporalAuraMinFinalStabilityLevel5 > 0.99f)
            {
                TemporalAuraMinFinalStabilityLevel5 = 0.99f;
                changed = true;
            }

            if (StabilityDrainLevel5PerMin > 0.0667f)
            {
                StabilityDrainLevel5PerMin = 0.0667f;
                changed = true;
            }

            if (FlyBuzzLevel4MinSeconds <= 0f)
            {
                FlyBuzzLevel4MinSeconds = 60f;
                changed = true;
            }

            if (FlyBuzzLevel4MaxSeconds < FlyBuzzLevel4MinSeconds)
            {
                FlyBuzzLevel4MaxSeconds = Math.Max(FlyBuzzLevel4MinSeconds, 135f);
                changed = true;
            }

            if (FlyBuzzLevel5MinSeconds <= 0f)
            {
                FlyBuzzLevel5MinSeconds = 20f;
                changed = true;
            }

            if (FlyBuzzLevel5MaxSeconds < FlyBuzzLevel5MinSeconds)
            {
                FlyBuzzLevel5MaxSeconds = Math.Max(FlyBuzzLevel5MinSeconds, 45f);
                changed = true;
            }

            if (FlyBuzzRange <= 0f)
            {
                FlyBuzzRange = 24f;
                changed = true;
            }

            if (FlyBuzzLevel4Volume <= 0f)
            {
                FlyBuzzLevel4Volume = 1.0f;
                changed = true;
            }

            if (FlyBuzzLevel5Volume <= 0f)
            {
                FlyBuzzLevel5Volume = 1.25f;
                changed = true;
            }

            if (MaxGainPerSecond <= 0f)
            {
                MaxGainPerSecond = 0.06f;
                changed = true;
            }

            changed |= EnsureAscendingThresholds();
            changed |= EnsureLevelNames();
            changed |= EnsureBarColors();
            changed |= EnsureAnimalSeekingMultipliers();

            changed |= DeduplicateList(ref HeavyArmorCodes, "plate", "mail", "iron", "steel", "bronze");
            changed |= DeduplicateList(ref MediumArmorCodes, "leather");
            changed |= DeduplicateList(ref AffectedDrifters, "drifter", "drifter-corrupt", "drifter-deep", "drifter-surface", "drifter-tainted");
            changed |= DeduplicateList(ref AffectedAnimals, "wolf", "bear", "pig", "hyena");

            return changed;
        }

        private bool EnsureAscendingThresholds()
        {
            if (LevelThresholds == null || LevelThresholds.Length != 5)
            {
                LevelThresholds = new[] { 0f, 10f, 30f, 70f, 90f };
                return true;
            }

            bool changed = false;
            LevelThresholds[0] = 0f;
            for (int i = 1; i < LevelThresholds.Length; i++)
            {
                float clamped = Math.Clamp(LevelThresholds[i], LevelThresholds[i - 1], 100f);
                if (!NearlyEqual(LevelThresholds[i], clamped))
                {
                    LevelThresholds[i] = clamped;
                    changed = true;
                }
            }

            return changed;
        }

        private bool EnsureLevelNames()
        {
            if (LevelNames != null && LevelNames.Length == 5)
            {
                return false;
            }

            LevelNames = new[] { "Clean", "Slightly Dirty", "Smelly", "Reeking", "Repulsive" };
            return true;
        }

        private bool EnsureBarColors()
        {
            if (BarColors != null && BarColors.Length == 5)
            {
                return false;
            }

            BarColors = new[]
            {
                unchecked((int)0xFF44AA44),
                unchecked((int)0xFF88AA22),
                unchecked((int)0xFFBBAA00),
                unchecked((int)0xFFCC6600),
                unchecked((int)0xFF663300)
            };
            return true;
        }

        private bool EnsureAnimalSeekingMultipliers()
        {
            if (AnimalSeekingRangeMultipliers == null || AnimalSeekingRangeMultipliers.Length != 5)
            {
                AnimalSeekingRangeMultipliers = new[] { 1.15f, 0.80f, 0.35f, 0.90f, 1.20f };
                return true;
            }

            bool changed = false;
            float[] defaults = { 1.15f, 0.80f, 0.35f, 0.90f, 1.20f };

            for (int i = 0; i < AnimalSeekingRangeMultipliers.Length; i++)
            {
                float clamped = Math.Clamp(AnimalSeekingRangeMultipliers[i], 0.05f, 3f);
                if (!NearlyEqual(AnimalSeekingRangeMultipliers[i], clamped))
                {
                    AnimalSeekingRangeMultipliers[i] = clamped;
                    changed = true;
                }
            }

            if (MatchesThresholdProfile(AnimalSeekingRangeMultipliers, 1f, 1f, 0.5f, 0.25f, 0f))
            {
                AnimalSeekingRangeMultipliers = defaults;
                changed = true;
            }

            return changed;
        }

        private static bool DeduplicateList(ref List<string> values, params string[] fallback)
        {
            List<string> original = values ?? new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduped = new List<string>();

            foreach (string raw in original)
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                string trimmed = raw.Trim();
                if (seen.Add(trimmed))
                {
                    deduped.Add(trimmed);
                }
            }

            if (deduped.Count == 0)
            {
                deduped.AddRange(fallback);
            }

            bool changed = deduped.Count != original.Count;
            if (!changed)
            {
                for (int i = 0; i < deduped.Count; i++)
                {
                    if (!string.Equals(deduped[i], original[i], StringComparison.Ordinal))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                values = deduped;
            }

            return changed;
        }

        private static bool MigrateLegacyFloat(ref float value, float legacyValue, float newValue)
        {
            if (!NearlyEqual(value, legacyValue))
            {
                return false;
            }

            value = newValue;
            return true;
        }

        private static bool MatchesThresholdProfile(float[]? values, params float[] expected)
        {
            if (values == null || expected == null || values.Length != expected.Length)
            {
                return false;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!NearlyEqual(values[i], expected[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool NearlyEqual(float left, float right)
        {
            return Math.Abs(left - right) < 0.0001f;
        }
    }
}
