using System;
using StenchMod.Config;
using StenchMod.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace StenchMod.Behaviors
{
    /// <summary>
    /// Server-side entity behavior attached to the player entity.
    /// Manages the stench value (0–100) and level (1–5), writing them to
    /// <see cref="ITreeAttribute"/> WatchedAttributes so they are automatically
    /// synced to all nearby clients.
    /// </summary>
    public class EntityBehaviorStench : EntityBehavior
    {
        // -------------------------------------------------------------------------
        // Constants
        // -------------------------------------------------------------------------

        private const string AttrValue         = "stench:value";
        private const string AttrLevel         = "stench:level";
        private const string AttrGainRate      = "stench:gainrate";
        private const string AttrReductionRate = "stench:reductionrate";
        private const string AttrIsRaining     = "stench:israining";
        private const string AttrClothingMult  = "stench:clothingmult";
        private const string AttrBuzzEta       = "stench:buzzeta";
        private const string AttrBuzzLevel     = "stench:buzzlevel";
        private const string AttrBuzzLast      = "stench:buzzlast";
        private const string AttrAnimalSeekTarget = "stench:animalseektarget";
        private const string AttrAnimalSeekDelta  = "stench:animalseekdelta";
        private const string AttrAnimalSeekFinal  = "stench:animalseekfinal";
        private const string AttrTempEnabled      = "stench:tempenabled";
        private const string AttrTempMode         = "stench:tempmode";
        private const string AttrTempAuraRadius   = "stench:tempauraradius";
        private const string AttrTempAuraStrength = "stench:tempaurastrength";
        private const string AttrTempAuraFloor    = "stench:tempaurafloor";
        private const string AttrTempAuraActive   = "stench:tempauraactive";
        private const string AttrTempAuraPenalty  = "stench:tempaurapenalty";
        private const string AttrTempAuraNearby   = "stench:tempauranearby";
        private const string AttrTempAuraBase    = "stench:tempaurabase";
        private const string AttrTempAuraFinal   = "stench:tempaurafinal";
        private const string AttrOwnStability    = "stench:ownstability";
        private const string AttrTempTopupRate   = "stench:temptopuprate";
        private const string AttrTempTotalDrainRate = "stench:temptotaldrainrate";
        private const string StatAnimalSeekingRange = "animalSeekingRange";
        private const string StatSourceStench = "stench";
        private const float ValueSyncThreshold = 0.02f;
        private const float DebugRateSyncStep = 0.005f;
        private const float DebugClothingSyncStep = 0.10f;
        private const float DebugBuzzEtaSyncStep = 0.5f;
        private const float DebugAnimalSeekSyncStep = 0.01f;
        private const float DebugTempAuraSyncStep = 0.005f;
        private const float DebugSyncIntervalSeconds = 1.25f;

        // -------------------------------------------------------------------------
        // State
        // -------------------------------------------------------------------------

        private float stenchValue    = 0f;
        private int   stenchLevel    = 1;

        /// <summary>Accumulated time the player has been in water (for entry delay).</summary>
        private float waterEntryTimer = 0f;

        /// <summary>Stamina reading from the previous tick, used to compute drain delta.</summary>
        private float lastStamina = -1f;

        /// <summary>True when Vigor mod is active and its API was retrieved successfully.</summary>
        private bool usingVigor = false;

        /// <summary>Vigor API object stored as dynamic to avoid a hard DLL reference.</summary>
        private object? vigorApiObj = null;

        /// <summary>Cached delegate to GetCurrentStamina(EntityPlayer) on the Vigor API object.</summary>
        private System.Func<EntityPlayer, float>? vigorGetStamina = null;

        /// <summary>Cached delegate to IsExhausted(EntityPlayer) on the Vigor API object.</summary>
        private System.Func<EntityPlayer, bool>? vigorIsExhausted = null;

        /// <summary>Accumulator for particle spawn timing.</summary>
        private float particleAccum = 0f;
        private float flyBuzzAccum = 0f;
        private float nextFlyBuzzDelay = -1f;
        private int lastFlyBuzzLevel = 0;
        private string lastPlayedBuzzSound = "-";
        private float debugSyncAccum = 0f;
        private float lastSyncedValue = float.NaN;
        private int lastSyncedLevel = int.MinValue;
        private float lastSyncedGainRate = float.NaN;
        private float lastSyncedReductionRate = float.NaN;
        private int lastSyncedIsRaining = int.MinValue;
        private float lastSyncedClothingMult = float.NaN;
        private float lastSyncedBuzzEta = float.NaN;
        private int lastSyncedBuzzLevel = int.MinValue;
        private string lastSyncedBuzzLast = string.Empty;
        private float lastAppliedAnimalSeekDelta = float.NaN;
        private float lastSyncedAnimalSeekTarget = float.NaN;
        private float lastSyncedAnimalSeekDelta = float.NaN;
        private float lastSyncedAnimalSeekFinal = float.NaN;
        private int lastSyncedTempEnabled = int.MinValue;
        private string lastSyncedTempMode = string.Empty;
        private float lastSyncedTempAuraRadius = float.NaN;
        private float lastSyncedTempAuraStrength = float.NaN;
        private float lastSyncedTempAuraFloor = float.NaN;
        private int lastSyncedTempAuraActive = int.MinValue;
        private float lastSyncedTempAuraPenalty = float.NaN;
        private int lastSyncedTempAuraNearby = int.MinValue;
        private float lastSyncedTempAuraBase = float.NaN;
        private float lastSyncedTempAuraFinal = float.NaN;
        private float lastSyncedOwnStability = float.NaN;
        private float lastSyncedTempTopupRate = float.NaN;
        private float lastSyncedTempTotalDrainRate = float.NaN;
        private double lastObservedOwnStability = double.NaN;
        private float lastAppliedTempTopupRate = 0f;
        private float lastTotalTempDrainRate = 0f;

        // -------------------------------------------------------------------------
        // References
        // -------------------------------------------------------------------------

        private StenchConfig Config => StenchModSystem.Config;

        // -------------------------------------------------------------------------
        // EntityBehavior overrides
        // -------------------------------------------------------------------------

        public EntityBehaviorStench(Entity entity) : base(entity) { }

        /// <inheritdoc/>
        public override string PropertyName() => "stench.stench";

        /// <inheritdoc/>
        public override void Initialize(EntityProperties properties, JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);

            // Restore persisted values (survive server restarts)
            stenchValue = entity.WatchedAttributes.GetFloat(AttrValue, 0f);
            stenchLevel = entity.WatchedAttributes.GetInt(AttrLevel,   1);

            // Initialise particle definitions once globally
            StenchParticleSystem.Initialize();

            // Set up Vigor integration if available
            if (entity.Api.Side == EnumAppSide.Server)
                TryInitVigor(entity.Api);
        }

        /// <inheritdoc/>
        public override void OnGameTick(float dt)
        {
            // This behavior runs its full logic only on the server.
            if (entity.Api.Side != EnumAppSide.Server) return;

            if (entity is not EntityPlayer player) return;

            // --- Gain -------------------------------------------------------
            float baseGain      = Config.BaseGainPerSecond * dt;
            float activityGain  = GetActivityGain(player, dt);
            float clothingMult  = StenchClothingSystem.GetMultiplier(player, Config);
            float totalGain     = (baseGain + activityGain) * clothingMult;
            float maxGain       = Math.Max(0f, Config.MaxGainPerSecond) * dt;
            if (maxGain > 0f)
            {
                totalGain = Math.Min(totalGain, maxGain);
            }

            // --- Reduction --------------------------------------------------
            float waterReduction = GetWaterReduction(dt);
            float rainReduction  = GetRainReduction(dt, out bool isRainingOut);
            float totalReduction = waterReduction + rainReduction;

            // --- Update value -----------------------------------------------
            stenchValue = Math.Clamp(stenchValue + totalGain - totalReduction, 0f, 100f);

            // --- Update level -----------------------------------------------
            stenchLevel = CalculateLevel(stenchValue);

            // --- Animal detection profile ----------------------------------
            ApplyAnimalSeekingRangeModifier(player, out float animalSeekTarget, out float animalSeekDelta, out float animalSeekFinal);

            // --- Temporal stability -----------------------------------------
            ApplyTemporalStabilityEffect(player, dt);
            StenchTemporalAuraSystem.DebugSnapshot auraSnapshot = GetTemporalAuraDebugSnapshot(player);

            float gainRate = totalGain / Math.Max(dt, 0.001f);
            float reductionRate = totalReduction / Math.Max(dt, 0.001f);

            // --- Sync to clients via WatchedAttributes ----------------------
            // Keep these writes sparse. Repeated player-entity watched updates can
            // interact badly with player resync mods and cause full playerdata
            // refreshes, which in turn reset held-item animations client-side.
            bool syncedStateThisTick = SyncWatchedAttributesIfNeeded();

            // --- Debug attributes -------------------------------------------
            float buzzEta = GetBuzzEtaSeconds();
            int buzzLevel = Config.EnableFlyBuzzSounds && stenchLevel >= 4 ? stenchLevel : 0;
            EntityBehaviorTemporalStabilityAffected? temporalBehavior =
                entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();
            float ownStability = temporalBehavior != null ? (float)temporalBehavior.OwnStability : 0f;

            MaybeSyncDebugAttributes(
                gainRate,
                reductionRate,
                isRainingOut,
                clothingMult,
                buzzEta,
                buzzLevel,
                lastPlayedBuzzSound,
                animalSeekTarget,
                animalSeekDelta,
                animalSeekFinal,
                auraSnapshot,
                ownStability,
                dt,
                syncedStateThisTick);

            // --- Particles --------------------------------------------------
            if (Config.ShowParticles && stenchLevel >= 3)
            {
                float particleInterval = GetParticleInterval(stenchLevel);
                particleAccum += dt;
                if (particleAccum >= particleInterval)
                {
                    particleAccum -= particleInterval;
                    StenchParticleSystem.SpawnAround(player, stenchLevel);
                }
            }
            else
            {
                particleAccum = 0f;
            }

            // --- Ambient fly buzz audio ------------------------------------
            TickFlyBuzzAudio(player, dt);
        }

        /// <inheritdoc/>
        public override void OnEntityDeath(DamageSource damageSourceForDeath)
        {
            base.OnEntityDeath(damageSourceForDeath);
            ResetStench();
        }

        /// <summary>Also reset on revive (respawn).</summary>
        public override void OnEntityRevive()
        {
            base.OnEntityRevive();
            ResetStench();
        }

        // -------------------------------------------------------------------------
        // Public helpers
        // -------------------------------------------------------------------------

        /// <summary>Returns the current stench level (1–5).</summary>
        public int GetCurrentLevel() => stenchLevel;

        /// <summary>Returns the raw stench value (0–100).</summary>
        public float GetCurrentValue() => stenchValue;

        /// <summary>Sets the raw stench value (0–100) and synchronizes derived state.</summary>
        public void SetCurrentValue(float value)
        {
            stenchValue = Math.Clamp(value, 0f, 100f);
            stenchLevel = CalculateLevel(stenchValue);

            if (entity.Api.Side == EnumAppSide.Server && entity is EntityPlayer player)
            {
                ApplyAnimalSeekingRangeModifier(player, out _, out _, out _);
                ApplyTemporalStabilityEffect(player, 0f);
            }

            SyncWatchedAttributes(force: true);
        }

        // -------------------------------------------------------------------------
        // Private helpers — gain
        // -------------------------------------------------------------------------

        private float GetActivityGain(EntityPlayer player, float dt)
        {
            float gain = 0f;

            if (usingVigor && vigorGetStamina != null && vigorIsExhausted != null)
            {
                // Vigor path: measure stamina drain between ticks
                float currentStamina = vigorGetStamina(player);

                if (lastStamina < 0f)
                {
                    lastStamina = currentStamina;
                }
                else
                {
                    float delta = lastStamina - currentStamina;
                    if (delta > 0f)
                        gain += delta * Config.StaminaDrainGainMultiplier;

                    // Exhaustion bonus
                    if (vigorIsExhausted(player))
                        gain += Config.BaseGainPerSecond * (Config.ExhaustedGainMultiplier - 1f) * dt;

                    lastStamina = currentStamina;
                }
            }
            else
            {
                // Fallback: use movement speed as a proxy for activity
                float speed = (float)player.Pos.Motion.Length();
                if (speed > Config.SpeedGainThreshold)
                    gain += speed * Config.SpeedGainMultiplier * dt;
            }

            return gain;
        }

        private static float GetParticleInterval(int level)
        {
            return level switch
            {
                >= 5 => 0.22f,
                4 => 0.40f,
                3 => 0.65f,
                _ => float.MaxValue
            };
        }

        private void TickFlyBuzzAudio(EntityPlayer player, float dt)
        {
            if (!Config.EnableFlyBuzzSounds || stenchLevel < 4)
            {
                flyBuzzAccum = 0f;
                nextFlyBuzzDelay = -1f;
                lastFlyBuzzLevel = 0;
                lastPlayedBuzzSound = "-";
                return;
            }

            if (lastFlyBuzzLevel != stenchLevel || nextFlyBuzzDelay < 0f)
            {
                lastFlyBuzzLevel = stenchLevel;
                flyBuzzAccum = 0f;
                nextFlyBuzzDelay = StenchSoundSystem.InitialBuzzDelaySeconds(stenchLevel, Config, entity.World.Rand);
                return;
            }

            flyBuzzAccum += dt;
            if (flyBuzzAccum < nextFlyBuzzDelay)
            {
                return;
            }

            flyBuzzAccum = 0f;
            nextFlyBuzzDelay = StenchSoundSystem.NextBuzzDelaySeconds(stenchLevel, Config, entity.World.Rand);
            string? playedSound = StenchSoundSystem.TryPlayBuzz(entity.Api, player, stenchLevel, Config);
            if (!string.IsNullOrEmpty(playedSound))
            {
                lastPlayedBuzzSound = playedSound;
            }
        }

        private float GetBuzzEtaSeconds()
        {
            if (!Config.EnableFlyBuzzSounds || stenchLevel < 4 || nextFlyBuzzDelay < 0f)
            {
                return -1f;
            }

            return Math.Max(0f, nextFlyBuzzDelay - flyBuzzAccum);
        }

        // -------------------------------------------------------------------------
        // Private helpers — reduction
        // -------------------------------------------------------------------------

        private float GetWaterReduction(float dt)
        {
            if (entity.Swimming)
            {
                // Player is fully swimming → fast reduction (after entry delay)
                waterEntryTimer += dt;
                if (waterEntryTimer >= Config.WaterEntryDelaySeconds)
                    return Config.SwimmingReductionPerSecond * dt;

                return 0f;
            }

            if (entity.FeetInLiquid)
            {
                // Only feet in water → slow reduction (after entry delay)
                waterEntryTimer += dt;
                if (waterEntryTimer >= Config.WaterEntryDelaySeconds)
                    return Config.WaterStandReductionPerSecond * dt;

                return 0f;
            }

            // Not in water → reset entry timer
            waterEntryTimer = 0f;
            return 0f;
        }

        private float GetRainReduction(float dt, out bool isRaining)
        {
            isRaining = false;

            WeatherSystemBase? weatherSys = entity.Api.ModLoader.GetModSystem<WeatherSystemBase>();
            if (weatherSys == null) return 0f;

            float precip = weatherSys.GetPrecipitation(
                entity.Pos.X, entity.Pos.Y, entity.Pos.Z);
            isRaining = precip > 0.5f;

            if (!isRaining) return 0f;

            // Check sky access using the rain height map — the same method VS uses internally
            // to decide whether a position receives precipitation.
            BlockPos playerPos = entity.Pos.AsBlockPos;
            int rainHeight = entity.World.BlockAccessor.GetRainMapHeightAt(playerPos);
            bool hasSkyAccess = playerPos.Y >= rainHeight - 1;

            if (!hasSkyAccess) return 0f;

            return Config.RainReductionPerSecond * dt;
        }

        // -------------------------------------------------------------------------
        // Private helpers — level calculation
        // -------------------------------------------------------------------------

        private int CalculateLevel(float value)
        {
            float[] thresholds = Config.LevelThresholds;

            // Thresholds define the start of each level.
            // Level 5 starts at thresholds[4], level 1 starts at thresholds[0].
            for (int i = thresholds.Length - 1; i >= 0; i--)
            {
                if (value >= thresholds[i])
                    return i + 1;
            }
            return 1;
        }

        private void ApplyTemporalStabilityEffect(EntityPlayer player, float dt)
        {
            string mode = StenchTemporalAuraSystem.NormalizeMode(Config.StabilityMode);

            if (!Config.EnableStabilityDrain)
            {
                StenchModSystem.TemporalAuraSystem?.RemoveEmitter(player.EntityId);
                lastObservedOwnStability = double.NaN;
                lastAppliedTempTopupRate = 0f;
                lastTotalTempDrainRate = 0f;
                return;
            }

            if (string.Equals(mode, StenchTemporalAuraSystem.TemporalAuraMode, StringComparison.OrdinalIgnoreCase))
            {
                StenchModSystem.TemporalAuraSystem?.UpdateEmitter(player, stenchLevel);
                ApplyTemporalAuraMinimumDrain(dt);
                return;
            }

            StenchModSystem.TemporalAuraSystem?.RemoveEmitter(player.EntityId);
            lastObservedOwnStability = double.NaN;
            lastAppliedTempTopupRate = 0f;
            lastTotalTempDrainRate = 0f;
            ApplyDirectStabilityDrain(dt);
        }

        private void ApplyTemporalAuraMinimumDrain(float dt)
        {
            lastAppliedTempTopupRate = 0f;
            lastTotalTempDrainRate = 0f;

            if (stenchLevel < 5 || dt <= 0f)
            {
                lastObservedOwnStability = double.NaN;
                return;
            }

            EntityBehaviorTemporalStabilityAffected? behavior =
                entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();

            if (behavior == null)
            {
                lastObservedOwnStability = double.NaN;
                return;
            }

            double current = behavior.OwnStability;
            double currentBeforeTopup = current;
            double targetDrainPerSecond = Config.StabilityDrainLevel5PerMin / 60.0;
            double vanillaDrainPerSecond = 0.0;

            if (!double.IsNaN(lastObservedOwnStability))
            {
                vanillaDrainPerSecond = Math.Max(0.0, lastObservedOwnStability - currentBeforeTopup) / Math.Max(dt, 0.001f);
                double desiredCurrent = Math.Max(0.0, lastObservedOwnStability - targetDrainPerSecond * dt);
                if (current > desiredCurrent)
                {
                    double topup = current - desiredCurrent;
                    behavior.OwnStability = desiredCurrent;
                    current = desiredCurrent;
                    lastAppliedTempTopupRate = (float)(topup / Math.Max(dt, 0.001f));
                }
            }

            lastTotalTempDrainRate = (float)(vanillaDrainPerSecond + lastAppliedTempTopupRate);
            lastObservedOwnStability = current;
        }

        private void ApplyDirectStabilityDrain(float dt)
        {
            lastTotalTempDrainRate = 0f;

            if (!Config.EnableStabilityDrain || stenchLevel < 5)
                return;

            EntityBehaviorTemporalStabilityAffected? behavior =
                entity.GetBehavior<EntityBehaviorTemporalStabilityAffected>();

            if (behavior == null)
                return;

            double drainPerSecond = Config.StabilityDrainLevel5PerMin / 60.0;

            behavior.OwnStability = Math.Max(0.0, behavior.OwnStability - drainPerSecond * dt);
            lastTotalTempDrainRate = (float)drainPerSecond;
        }

        private StenchTemporalAuraSystem.DebugSnapshot GetTemporalAuraDebugSnapshot(EntityPlayer player)
        {
            if (entity.Api.Side != EnumAppSide.Server)
            {
                return default;
            }

            if (StenchModSystem.TemporalAuraSystem == null)
            {
                return new StenchTemporalAuraSystem.DebugSnapshot(
                    StenchTemporalAuraSystem.NormalizeMode(Config.StabilityMode),
                    false,
                    Config.TemporalAuraRadius,
                    Config.TemporalAuraLevel5Penalty,
                    Config.TemporalAuraMinFinalStabilityLevel5,
                    0f,
                    0,
                    0f,
                    0f);
            }

            return StenchModSystem.TemporalAuraSystem.GetServerDebugSnapshot(player);
        }

        private void ApplyAnimalSeekingRangeModifier(EntityPlayer player, out float targetMultiplier, out float stenchDelta, out float finalMultiplier)
        {
            targetMultiplier = 1f;
            stenchDelta = 0f;

            player.Stats.Register(StatAnimalSeekingRange, EnumStatBlendType.WeightedSum);

            if (!Config.EnableAnimalSeekingRangeModifier)
            {
                RemoveAnimalSeekingRangeModifier(player);
                finalMultiplier = player.Stats.GetBlended(StatAnimalSeekingRange);
                return;
            }

            targetMultiplier = GetAnimalSeekingRangeMultiplier();
            stenchDelta = targetMultiplier - 1f;

            if (float.IsNaN(lastAppliedAnimalSeekDelta) || !NearlyEqual(lastAppliedAnimalSeekDelta, stenchDelta, 0.001f))
            {
                if (Math.Abs(stenchDelta) <= 0.001f)
                {
                    player.Stats.Remove(StatAnimalSeekingRange, StatSourceStench);
                }
                else
                {
                    player.Stats.Set(StatAnimalSeekingRange, StatSourceStench, stenchDelta, false);
                }

                lastAppliedAnimalSeekDelta = stenchDelta;
            }

            finalMultiplier = player.Stats.GetBlended(StatAnimalSeekingRange);
        }

        private void RemoveAnimalSeekingRangeModifier(EntityPlayer player)
        {
            if (float.IsNaN(lastAppliedAnimalSeekDelta) || Math.Abs(lastAppliedAnimalSeekDelta) <= 0.001f)
            {
                lastAppliedAnimalSeekDelta = 0f;
                return;
            }

            player.Stats.Remove(StatAnimalSeekingRange, StatSourceStench);
            lastAppliedAnimalSeekDelta = 0f;
        }

        private float GetAnimalSeekingRangeMultiplier()
        {
            float[] multipliers = Config.AnimalSeekingRangeMultipliers;
            if (multipliers == null || multipliers.Length == 0)
            {
                return 1f;
            }

            int idx = Math.Clamp(stenchLevel - 1, 0, multipliers.Length - 1);
            return Math.Max(0.05f, multipliers[idx]);
        }

        // -------------------------------------------------------------------------
        // Private helpers — reset
        // -------------------------------------------------------------------------

        private void ResetStench()
        {
            stenchValue = 0f;
            stenchLevel = 1;
            lastObservedOwnStability = double.NaN;
            lastAppliedTempTopupRate = 0f;
            lastTotalTempDrainRate = 0f;

            if (entity.Api.Side == EnumAppSide.Server && entity is EntityPlayer player)
            {
                ApplyAnimalSeekingRangeModifier(player, out _, out _, out _);
                ApplyTemporalStabilityEffect(player, 0f);
            }

            SyncWatchedAttributes(force: true);
        }

        private bool SyncWatchedAttributesIfNeeded()
        {
            bool levelChanged = lastSyncedLevel != stenchLevel;
            bool valueChanged = float.IsNaN(lastSyncedValue) || Math.Abs(stenchValue - lastSyncedValue) >= ValueSyncThreshold;

            if (!levelChanged && !valueChanged)
            {
                return false;
            }

            SyncWatchedAttributes(force: false);
            return true;
        }

        private void SyncWatchedAttributes(bool force)
        {
            bool shouldSyncValue = force || float.IsNaN(lastSyncedValue) || !NearlyEqual(lastSyncedValue, stenchValue, 0.001f);
            bool shouldSyncLevel = force || lastSyncedLevel != stenchLevel;
            string normalizedMode = StenchTemporalAuraSystem.NormalizeMode(Config.StabilityMode);
            int enabledInt = Config.EnableStabilityDrain ? 1 : 0;
            bool shouldSyncTempEnabled = force || lastSyncedTempEnabled != enabledInt;
            bool shouldSyncTempMode = force || !string.Equals(lastSyncedTempMode, normalizedMode, StringComparison.Ordinal);
            bool shouldSyncTempAuraRadius = force || float.IsNaN(lastSyncedTempAuraRadius) || !NearlyEqual(lastSyncedTempAuraRadius, Config.TemporalAuraRadius, 0.001f);
            bool shouldSyncTempAuraStrength = force || float.IsNaN(lastSyncedTempAuraStrength) || !NearlyEqual(lastSyncedTempAuraStrength, Config.TemporalAuraLevel5Penalty, 0.001f);
            bool shouldSyncTempAuraFloor = force || float.IsNaN(lastSyncedTempAuraFloor) || !NearlyEqual(lastSyncedTempAuraFloor, Config.TemporalAuraMinFinalStabilityLevel5, 0.001f);

            if (shouldSyncValue)
            {
                entity.WatchedAttributes.SetFloat(AttrValue, stenchValue);
                lastSyncedValue = stenchValue;
            }

            if (shouldSyncLevel)
            {
                entity.WatchedAttributes.SetInt(AttrLevel, stenchLevel);
                lastSyncedLevel = stenchLevel;
            }

            if (shouldSyncTempEnabled)
            {
                entity.WatchedAttributes.SetInt(AttrTempEnabled, enabledInt);
                lastSyncedTempEnabled = enabledInt;
            }

            if (shouldSyncTempMode)
            {
                entity.WatchedAttributes.SetString(AttrTempMode, normalizedMode);
                lastSyncedTempMode = normalizedMode;
            }

            if (shouldSyncTempAuraRadius)
            {
                entity.WatchedAttributes.SetFloat(AttrTempAuraRadius, Config.TemporalAuraRadius);
                lastSyncedTempAuraRadius = Config.TemporalAuraRadius;
            }

            if (shouldSyncTempAuraStrength)
            {
                entity.WatchedAttributes.SetFloat(AttrTempAuraStrength, Config.TemporalAuraLevel5Penalty);
                lastSyncedTempAuraStrength = Config.TemporalAuraLevel5Penalty;
            }

            if (shouldSyncTempAuraFloor)
            {
                entity.WatchedAttributes.SetFloat(AttrTempAuraFloor, Config.TemporalAuraMinFinalStabilityLevel5);
                lastSyncedTempAuraFloor = Config.TemporalAuraMinFinalStabilityLevel5;
            }
        }

        private void MaybeSyncDebugAttributes(float gainRate, float reductionRate, bool isRainingOut, float clothingMult, float buzzEta, int buzzLevel, string buzzLast, float animalSeekTarget, float animalSeekDelta, float animalSeekFinal, StenchTemporalAuraSystem.DebugSnapshot auraSnapshot, float ownStability, float dt, bool stateSyncedThisTick)
        {
            if (!Config.DebugMode)
            {
                debugSyncAccum = 0f;
                return;
            }

            debugSyncAccum += dt;

            float quantizedGainRate = Quantize(gainRate, DebugRateSyncStep);
            float quantizedReductionRate = Quantize(reductionRate, DebugRateSyncStep);
            float quantizedClothingMult = Quantize(clothingMult, DebugClothingSyncStep);
            float quantizedBuzzEta = buzzEta < 0f ? -1f : Quantize(buzzEta, DebugBuzzEtaSyncStep);
            float quantizedAnimalSeekTarget = Quantize(animalSeekTarget, DebugAnimalSeekSyncStep);
            float quantizedAnimalSeekDelta = Quantize(animalSeekDelta, DebugAnimalSeekSyncStep);
            float quantizedAnimalSeekFinal = Quantize(animalSeekFinal, DebugAnimalSeekSyncStep);
            float quantizedTempAuraFloor = Quantize(auraSnapshot.Floor, DebugTempAuraSyncStep);
            float quantizedTempAuraPenalty = Quantize(auraSnapshot.Penalty, DebugTempAuraSyncStep);
            float quantizedTempAuraBase = Quantize(auraSnapshot.BaseStability, DebugTempAuraSyncStep);
            float quantizedTempAuraFinal = Quantize(auraSnapshot.FinalStability, DebugTempAuraSyncStep);
            float quantizedOwnStability = Quantize(ownStability, DebugTempAuraSyncStep);
            float quantizedTempTopupRate = Quantize(lastAppliedTempTopupRate, DebugTempAuraSyncStep);
            float quantizedTempTotalDrainRate = Quantize(lastTotalTempDrainRate, DebugTempAuraSyncStep);
            int rainingInt = isRainingOut ? 1 : 0;
            int tempAuraActiveInt = auraSnapshot.Active ? 1 : 0;

            bool debugChanged =
                float.IsNaN(lastSyncedGainRate) || !NearlyEqual(lastSyncedGainRate, quantizedGainRate, 0.001f) ||
                float.IsNaN(lastSyncedReductionRate) || !NearlyEqual(lastSyncedReductionRate, quantizedReductionRate, 0.001f) ||
                lastSyncedIsRaining != rainingInt ||
                float.IsNaN(lastSyncedClothingMult) || !NearlyEqual(lastSyncedClothingMult, quantizedClothingMult, 0.001f) ||
                float.IsNaN(lastSyncedBuzzEta) || !NearlyEqual(lastSyncedBuzzEta, quantizedBuzzEta, 0.001f) ||
                lastSyncedBuzzLevel != buzzLevel ||
                !string.Equals(lastSyncedBuzzLast, buzzLast, StringComparison.Ordinal) ||
                float.IsNaN(lastSyncedAnimalSeekTarget) || !NearlyEqual(lastSyncedAnimalSeekTarget, quantizedAnimalSeekTarget, 0.001f) ||
                float.IsNaN(lastSyncedAnimalSeekDelta) || !NearlyEqual(lastSyncedAnimalSeekDelta, quantizedAnimalSeekDelta, 0.001f) ||
                float.IsNaN(lastSyncedAnimalSeekFinal) || !NearlyEqual(lastSyncedAnimalSeekFinal, quantizedAnimalSeekFinal, 0.001f) ||
                lastSyncedTempAuraActive != tempAuraActiveInt ||
                float.IsNaN(lastSyncedTempAuraFloor) || !NearlyEqual(lastSyncedTempAuraFloor, quantizedTempAuraFloor, 0.001f) ||
                float.IsNaN(lastSyncedTempAuraPenalty) || !NearlyEqual(lastSyncedTempAuraPenalty, quantizedTempAuraPenalty, 0.001f) ||
                lastSyncedTempAuraNearby != auraSnapshot.NearbyEmitters ||
                float.IsNaN(lastSyncedTempAuraBase) || !NearlyEqual(lastSyncedTempAuraBase, quantizedTempAuraBase, 0.001f) ||
                float.IsNaN(lastSyncedTempAuraFinal) || !NearlyEqual(lastSyncedTempAuraFinal, quantizedTempAuraFinal, 0.001f) ||
                float.IsNaN(lastSyncedOwnStability) || !NearlyEqual(lastSyncedOwnStability, quantizedOwnStability, 0.001f) ||
                float.IsNaN(lastSyncedTempTopupRate) || !NearlyEqual(lastSyncedTempTopupRate, quantizedTempTopupRate, 0.001f) ||
                float.IsNaN(lastSyncedTempTotalDrainRate) || !NearlyEqual(lastSyncedTempTotalDrainRate, quantizedTempTotalDrainRate, 0.001f);

            if (!debugChanged || debugSyncAccum < DebugSyncIntervalSeconds || stateSyncedThisTick)
            {
                return;
            }

            entity.WatchedAttributes.SetFloat(AttrGainRate, quantizedGainRate);
            entity.WatchedAttributes.SetFloat(AttrReductionRate, quantizedReductionRate);
            entity.WatchedAttributes.SetInt(AttrIsRaining, rainingInt);
            entity.WatchedAttributes.SetFloat(AttrClothingMult, quantizedClothingMult);
            entity.WatchedAttributes.SetFloat(AttrBuzzEta, quantizedBuzzEta);
            entity.WatchedAttributes.SetInt(AttrBuzzLevel, buzzLevel);
            entity.WatchedAttributes.SetString(AttrBuzzLast, buzzLast);
            entity.WatchedAttributes.SetFloat(AttrAnimalSeekTarget, quantizedAnimalSeekTarget);
            entity.WatchedAttributes.SetFloat(AttrAnimalSeekDelta, quantizedAnimalSeekDelta);
            entity.WatchedAttributes.SetFloat(AttrAnimalSeekFinal, quantizedAnimalSeekFinal);
            entity.WatchedAttributes.SetInt(AttrTempAuraActive, tempAuraActiveInt);
            entity.WatchedAttributes.SetFloat(AttrTempAuraFloor, quantizedTempAuraFloor);
            entity.WatchedAttributes.SetFloat(AttrTempAuraPenalty, quantizedTempAuraPenalty);
            entity.WatchedAttributes.SetInt(AttrTempAuraNearby, auraSnapshot.NearbyEmitters);
            entity.WatchedAttributes.SetFloat(AttrTempAuraBase, quantizedTempAuraBase);
            entity.WatchedAttributes.SetFloat(AttrTempAuraFinal, quantizedTempAuraFinal);
            entity.WatchedAttributes.SetFloat(AttrOwnStability, quantizedOwnStability);
            entity.WatchedAttributes.SetFloat(AttrTempTopupRate, quantizedTempTopupRate);
            entity.WatchedAttributes.SetFloat(AttrTempTotalDrainRate, quantizedTempTotalDrainRate);

            lastSyncedGainRate = quantizedGainRate;
            lastSyncedReductionRate = quantizedReductionRate;
            lastSyncedIsRaining = rainingInt;
            lastSyncedClothingMult = quantizedClothingMult;
            lastSyncedBuzzEta = quantizedBuzzEta;
            lastSyncedBuzzLevel = buzzLevel;
            lastSyncedBuzzLast = buzzLast;
            lastSyncedAnimalSeekTarget = quantizedAnimalSeekTarget;
            lastSyncedAnimalSeekDelta = quantizedAnimalSeekDelta;
            lastSyncedAnimalSeekFinal = quantizedAnimalSeekFinal;
            lastSyncedTempAuraActive = tempAuraActiveInt;
            lastSyncedTempAuraFloor = quantizedTempAuraFloor;
            lastSyncedTempAuraPenalty = quantizedTempAuraPenalty;
            lastSyncedTempAuraNearby = auraSnapshot.NearbyEmitters;
            lastSyncedTempAuraBase = quantizedTempAuraBase;
            lastSyncedTempAuraFinal = quantizedTempAuraFinal;
            lastSyncedOwnStability = quantizedOwnStability;
            lastSyncedTempTopupRate = quantizedTempTopupRate;
            lastSyncedTempTotalDrainRate = quantizedTempTotalDrainRate;
            debugSyncAccum = 0f;
        }

        private static float Quantize(float value, float step)
        {
            if (step <= 0f)
            {
                return value;
            }

            return (float)(Math.Round(value / step) * step);
        }

        private static bool NearlyEqual(float a, float b, float epsilon)
        {
            return Math.Abs(a - b) <= epsilon;
        }

        // -------------------------------------------------------------------------
        // Vigor integration via reflection (no hard DLL dependency)
        // -------------------------------------------------------------------------

        private void TryInitVigor(ICoreAPI api)
        {
            if (!api.ModLoader.IsModEnabled("vigor"))
                return;

            try
            {
                // Locate the Vigor ModSystem by searching all loaded mod systems via reflection.
                // This avoids a hard compile-time reference to the Vigor DLL.
                ModSystem? vigorSystem = null;
                foreach (ModSystem ms in api.ModLoader.Systems)
                {
                    if (ms.GetType().FullName == "Vigor.VigorModSystem"
                     || ms.GetType().Name     == "VigorModSystem")
                    {
                        vigorSystem = ms;
                        break;
                    }
                }
                if (vigorSystem == null) return;

                // Retrieve .API property
                System.Reflection.PropertyInfo? apiProp =
                    vigorSystem.GetType().GetProperty("API")
                    ?? vigorSystem.GetType().GetProperty("ServerAPI");

                if (apiProp == null) return;

                vigorApiObj = apiProp.GetValue(vigorSystem);
                if (vigorApiObj == null) return;

                System.Type vigorApiType = vigorApiObj.GetType();

                // Bind GetCurrentStamina(EntityPlayer) → float
                System.Reflection.MethodInfo? getStaminaMethod =
                    vigorApiType.GetMethod("GetCurrentStamina",
                        new[] { typeof(EntityPlayer) });

                // Bind IsExhausted(EntityPlayer) → bool
                System.Reflection.MethodInfo? isExhaustedMethod =
                    vigorApiType.GetMethod("IsExhausted",
                        new[] { typeof(EntityPlayer) });

                if (getStaminaMethod != null && isExhaustedMethod != null)
                {
                    object capturedApi = vigorApiObj;
                    vigorGetStamina  = (p) => (float)(getStaminaMethod.Invoke(capturedApi, new object[] { p }) ?? 0f);
                    vigorIsExhausted = (p) => (bool)(isExhaustedMethod.Invoke(capturedApi, new object[] { p }) ?? false);
                    usingVigor       = true;
                    api.Logger.Notification("[StenchMod] Vigor integration active.");
                }
            }
            catch (Exception ex)
            {
                api.Logger.Warning("[StenchMod] Vigor integration failed (non-fatal): " + ex.Message);
                usingVigor = false;
            }
        }
    }
}
