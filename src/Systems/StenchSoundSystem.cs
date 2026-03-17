using System;
using System.Collections.Generic;
using StenchMod.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace StenchMod.Systems
{
    /// <summary>
    /// Handles optional ambient fly buzz one-shots for high stench levels.
    /// Sounds are played server-side so nearby players can hear them too.
    /// </summary>
    public static class StenchSoundSystem
    {
        private sealed class BuzzSample
        {
            public string Code { get; }
            public float VolumeMultiplier { get; }

            public BuzzSample(string code, float volumeMultiplier)
            {
                Code = code;
                VolumeMultiplier = volumeMultiplier;
            }
        }

        private static readonly BuzzSample[] Level4Buzzes =
        {
            new BuzzSample("l4_01", 1.00f),
            new BuzzSample("l4_02", 1.35f)
        };

        private static readonly BuzzSample[] Level5Buzzes =
        {
            new BuzzSample("l5_01", 1.20f),
            new BuzzSample("l5_02", 1.00f),
            new BuzzSample("l5_03", 1.00f)
        };
        private const string WashSoundCode = "playerwash";
        private static readonly HashSet<string> MissingAssetWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static float NextBuzzDelaySeconds(int level, StenchConfig config, Random rand)
        {
            float minSeconds = level >= 5 ? config.FlyBuzzLevel5MinSeconds : config.FlyBuzzLevel4MinSeconds;
            float maxSeconds = level >= 5 ? config.FlyBuzzLevel5MaxSeconds : config.FlyBuzzLevel4MaxSeconds;
            if (maxSeconds <= minSeconds)
            {
                return Math.Max(0.1f, minSeconds);
            }

            return minSeconds + (float)rand.NextDouble() * (maxSeconds - minSeconds);
        }

        public static float InitialBuzzDelaySeconds(int level, StenchConfig config, Random rand)
        {
            if (level >= 5)
            {
                return RandomRange(rand, 4f, 8f);
            }

            return RandomRange(rand, 20f, 45f);
        }

        public static string? TryPlayBuzz(ICoreAPI api, EntityPlayer player, int level, StenchConfig config)
        {
            BuzzSample[] pool = GetPool(level);
            BuzzSample sample = pool[player.World.Rand.Next(pool.Length)];

            if (!TryResolveSound(api, sample.Code, out AssetLocation sound, out string debugPath))
            {
                return null;
            }

            float pitch = level >= 5
                ? RandomRange(player.World.Rand, 0.92f, 1.08f)
                : RandomRange(player.World.Rand, 0.97f, 1.04f);
            float range = Math.Max(1f, config.FlyBuzzRange);
            float baseVolume = level >= 5 ? config.FlyBuzzLevel5Volume : config.FlyBuzzLevel4Volume;
            float volume = Math.Max(0.05f, baseVolume * sample.VolumeMultiplier * RandomRange(player.World.Rand, 0.92f, 1.08f));

            player.World.PlaySoundAt(sound, player, null, pitch, range, volume);
            return debugPath;
        }

        public static string? TryPlayWash(ICoreAPI api, Entity sourceEntity, Entity targetEntity)
        {
            if (!TryResolveModSound(api, "wash", WashSoundCode, out AssetLocation sound, out string debugPath))
            {
                return null;
            }

            float pitch = RandomRange(targetEntity.World.Rand, 0.96f, 1.04f);
            targetEntity.World.PlaySoundAt(sound, targetEntity, null, pitch, 24f, 0.95f);
            return debugPath;
        }

        private static BuzzSample[] GetPool(int level)
        {
            return level >= 5 ? Level5Buzzes : Level4Buzzes;
        }

        private static bool TryResolveSound(ICoreAPI api, string code, out AssetLocation sound, out string debugPath)
        {
            return TryResolveModSound(api, "flybuzz", code, out sound, out debugPath);
        }

        private static bool TryResolveModSound(ICoreAPI api, string folder, string code, out AssetLocation sound, out string debugPath)
        {
            AssetLocation fileAsset = new AssetLocation("stench", $"sounds/{folder}/{code}.ogg");
            if (api.Assets.Exists(fileAsset))
            {
                sound = new AssetLocation("stench", $"sounds/{folder}/{code}");
                debugPath = sound.Path;
                return true;
            }

            sound = default!;
            debugPath = $"-missing:{folder}/{code}-";

            string warningKey = $"{folder}/{code}";
            if (MissingAssetWarnings.Add(warningKey))
            {
                api.Logger.Warning("[StenchMod] Sound asset missing: {0}", fileAsset);
            }

            return false;
        }

        private static float RandomRange(Random rand, float min, float max)
        {
            return min + (float)rand.NextDouble() * (max - min);
        }
    }
}
