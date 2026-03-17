using System;
using System.Collections.Generic;
using StenchMod.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace StenchMod.Systems
{
    /// <summary>
    /// Adds a small positional temporal instability aura around level-5 dirty
    /// players by hooking the vanilla temporal stability system.
    /// </summary>
    public sealed class StenchTemporalAuraSystem : IDisposable
    {
        public const string DirectMode = "direct";
        public const string TemporalAuraMode = "temporalaura";

        private sealed class AuraEmitter
        {
            public double X;
            public double Y;
            public double Z;
        }

        public readonly struct DebugSnapshot
        {
            public readonly string Mode;
            public readonly bool Active;
            public readonly float Radius;
            public readonly float Strength;
            public readonly float Floor;
            public readonly float Penalty;
            public readonly int NearbyEmitters;
            public readonly float BaseStability;
            public readonly float FinalStability;

            public DebugSnapshot(string mode, bool active, float radius, float strength, float floor, float penalty, int nearbyEmitters, float baseStability, float finalStability)
            {
                Mode = mode;
                Active = active;
                Radius = radius;
                Strength = strength;
                Floor = floor;
                Penalty = penalty;
                NearbyEmitters = nearbyEmitters;
                BaseStability = baseStability;
                FinalStability = finalStability;
            }
        }

        private readonly Dictionary<long, AuraEmitter> serverEmitters = new Dictionary<long, AuraEmitter>();

        private ICoreServerAPI? sapi;
        private ICoreClientAPI? capi;
        private SystemTemporalStability? serverTemporalSystem;
        private SystemTemporalStability? clientTemporalSystem;

        private bool serverHooked;
        private bool clientHooked;
        private bool suppressServerHook;
        private bool suppressClientHook;

        private StenchConfig Config => StenchModSystem.Config;

        public static string NormalizeMode(string? rawMode)
        {
            return string.Equals(rawMode, DirectMode, StringComparison.OrdinalIgnoreCase)
                ? DirectMode
                : TemporalAuraMode;
        }

        public void InitializeServer(ICoreServerAPI api)
        {
            if (serverHooked)
            {
                return;
            }

            sapi = api;
            serverTemporalSystem = api.ModLoader.GetModSystem<SystemTemporalStability>();
            if (serverTemporalSystem == null)
            {
                api.Logger.Warning("[StenchMod] Temporal aura disabled on server because SystemTemporalStability was not available.");
                return;
            }

            serverTemporalSystem.OnGetTemporalStability += OnServerGetTemporalStability;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
            serverHooked = true;
        }

        public void InitializeClient(ICoreClientAPI api)
        {
            if (clientHooked)
            {
                return;
            }

            capi = api;
            clientTemporalSystem = api.ModLoader.GetModSystem<SystemTemporalStability>();
            if (clientTemporalSystem == null)
            {
                api.Logger.Warning("[StenchMod] Temporal aura visualization disabled on client because SystemTemporalStability was not available.");
                return;
            }

            clientTemporalSystem.OnGetTemporalStability += OnClientGetTemporalStability;
            clientHooked = true;
        }

        public void UpdateEmitter(EntityPlayer player, int stenchLevel)
        {
            if (sapi == null)
            {
                return;
            }

            if (!UsesTemporalAura(Config) || stenchLevel < 5)
            {
                RemoveEmitter(player.EntityId);
                return;
            }

            if (!serverEmitters.TryGetValue(player.EntityId, out AuraEmitter? emitter))
            {
                emitter = new AuraEmitter();
                serverEmitters[player.EntityId] = emitter;
            }

            emitter.X = player.ServerPos.X;
            emitter.Y = player.ServerPos.Y;
            emitter.Z = player.ServerPos.Z;
        }

        public void RemoveEmitter(long entityId)
        {
            if (entityId != 0)
            {
                serverEmitters.Remove(entityId);
            }
        }

        public DebugSnapshot GetServerDebugSnapshot(EntityPlayer player)
        {
            string mode = NormalizeMode(Config.StabilityMode);
            float radius = Config.TemporalAuraRadius;
            float strength = Config.TemporalAuraLevel5Penalty;
            float floor = Config.TemporalAuraMinFinalStabilityLevel5;

            if (!UsesTemporalAura(Config))
            {
                return new DebugSnapshot(mode, false, radius, strength, floor, 0f, 0, 0f, 0f);
            }

            float baseStability = GetRawServerTemporalStability(player.ServerPos.X, player.ServerPos.Y, player.ServerPos.Z);
            float rawPenalty = ComputePenaltyFromServerEmitters(player.ServerPos.X, player.ServerPos.Y, player.ServerPos.Z, out int nearby);
            float finalStability = ApplyAuraToStability(baseStability, rawPenalty, strength, floor, out float appliedPenalty);
            return new DebugSnapshot(mode, appliedPenalty > 0.0001f, radius, strength, floor, appliedPenalty, nearby, baseStability, finalStability);
        }

        public DebugSnapshot GetClientDebugSnapshot()
        {
            string mode = GetClientMode();
            float radius = GetClientRadius();
            float strength = GetClientStrength();
            float floor = GetClientFloor();

            if (capi?.World?.Player?.Entity is not EntityPlayer player || !UsesTemporalAura(mode, true))
            {
                return new DebugSnapshot(mode, false, radius, strength, floor, 0f, 0, 0f, 0f);
            }

            float baseStability = GetRawClientTemporalStability(player.Pos.X, player.Pos.Y, player.Pos.Z);
            float rawPenalty = ComputePenaltyFromClientPlayers(player.Pos.X, player.Pos.Y, player.Pos.Z, out int nearby);
            float finalStability = ApplyAuraToStability(baseStability, rawPenalty, strength, floor, out float appliedPenalty);
            return new DebugSnapshot(mode, appliedPenalty > 0.0001f, radius, strength, floor, appliedPenalty, nearby, baseStability, finalStability);
        }

        public void Dispose()
        {
            if (serverHooked && serverTemporalSystem != null)
            {
                serverTemporalSystem.OnGetTemporalStability -= OnServerGetTemporalStability;
                if (sapi != null)
                {
                    sapi.Event.PlayerDisconnect -= OnPlayerDisconnect;
                }
            }

            if (clientHooked && clientTemporalSystem != null)
            {
                clientTemporalSystem.OnGetTemporalStability -= OnClientGetTemporalStability;
            }

            serverHooked = false;
            clientHooked = false;
            serverEmitters.Clear();
            sapi = null;
            capi = null;
            serverTemporalSystem = null;
            clientTemporalSystem = null;
        }

        private float OnServerGetTemporalStability(float stability, double x, double y, double z)
        {
            if (suppressServerHook || !UsesTemporalAura(Config))
            {
                return stability;
            }

            float penalty = ComputePenaltyFromServerEmitters(x, y, z, out _);
            return ApplyAuraToStability(stability, penalty, Config.TemporalAuraLevel5Penalty, Config.TemporalAuraMinFinalStabilityLevel5, out _);
        }

        private float OnClientGetTemporalStability(float stability, double x, double y, double z)
        {
            if (suppressClientHook || !UsesTemporalAura(GetClientMode(), GetClientEnabled()))
            {
                return stability;
            }

            float penalty = ComputePenaltyFromClientPlayers(x, y, z, out _);
            return ApplyAuraToStability(stability, penalty, GetClientStrength(), GetClientFloor(), out _);
        }

        private float ComputePenaltyFromServerEmitters(double x, double y, double z, out int nearbyEmitters)
        {
            nearbyEmitters = 0;
            float radius = Config.TemporalAuraRadius;
            if (radius <= 0f || serverEmitters.Count == 0)
            {
                return 0f;
            }

            double radiusSq = radius * radius;
            float strongestPenalty = 0f;

            foreach (KeyValuePair<long, AuraEmitter> entry in serverEmitters)
            {
                AuraEmitter emitter = entry.Value;
                double dx = emitter.X - x;
                double dy = emitter.Y - y;
                double dz = emitter.Z - z;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > radiusSq)
                {
                    continue;
                }

                nearbyEmitters++;
                strongestPenalty = Math.Max(strongestPenalty, CalculatePenalty((float)Math.Sqrt(distSq), radius, Config.TemporalAuraLevel5Penalty));
            }

            return strongestPenalty;
        }

        private float ComputePenaltyFromClientPlayers(double x, double y, double z, out int nearbyEmitters)
        {
            nearbyEmitters = 0;
            if (capi?.World?.AllOnlinePlayers == null)
            {
                return 0f;
            }

            float radius = GetClientRadius();
            if (radius <= 0f)
            {
                return 0f;
            }

            float strength = GetClientStrength();
            double radiusSq = radius * radius;
            float strongestPenalty = 0f;

            foreach (IPlayer player in capi.World.AllOnlinePlayers)
            {
                if (player.Entity is not EntityPlayer entityPlayer)
                {
                    continue;
                }

                int level = entityPlayer.WatchedAttributes.GetInt("stench:level", 1);
                if (level < 5)
                {
                    continue;
                }

                double dx = entityPlayer.Pos.X - x;
                double dy = entityPlayer.Pos.Y - y;
                double dz = entityPlayer.Pos.Z - z;
                double distSq = dx * dx + dy * dy + dz * dz;
                if (distSq > radiusSq)
                {
                    continue;
                }

                nearbyEmitters++;
                strongestPenalty = Math.Max(strongestPenalty, CalculatePenalty((float)Math.Sqrt(distSq), radius, strength));
            }

            return strongestPenalty;
        }

        private static float CalculatePenalty(float distance, float radius, float strength)
        {
            if (radius <= 0f || strength <= 0f)
            {
                return 0f;
            }

            float normalized = 1f - Math.Clamp(distance / radius, 0f, 1f);
            return strength * normalized;
        }

        private static bool UsesTemporalAura(StenchConfig config)
        {
            return UsesTemporalAura(NormalizeMode(config.StabilityMode), config.EnableStabilityDrain);
        }

        private static bool UsesTemporalAura(string mode, bool enabled)
        {
            return enabled && string.Equals(mode, TemporalAuraMode, StringComparison.OrdinalIgnoreCase);
        }

        private string GetClientMode()
        {
            Entity? entity = capi?.World?.Player?.Entity;
            if (entity == null)
            {
                return NormalizeMode(Config.StabilityMode);
            }

            return NormalizeMode(entity.WatchedAttributes.GetString("stench:tempmode", Config.StabilityMode));
        }

        private bool GetClientEnabled()
        {
            Entity? entity = capi?.World?.Player?.Entity;
            if (entity == null)
            {
                return Config.EnableStabilityDrain;
            }

            return entity.WatchedAttributes.GetInt("stench:tempenabled", Config.EnableStabilityDrain ? 1 : 0) != 0;
        }

        private float GetClientRadius()
        {
            Entity? entity = capi?.World?.Player?.Entity;
            if (entity == null)
            {
                return Config.TemporalAuraRadius;
            }

            return entity.WatchedAttributes.GetFloat("stench:tempauraradius", Config.TemporalAuraRadius);
        }

        private float GetClientStrength()
        {
            Entity? entity = capi?.World?.Player?.Entity;
            if (entity == null)
            {
                return Config.TemporalAuraLevel5Penalty;
            }

            return entity.WatchedAttributes.GetFloat("stench:tempaurastrength", Config.TemporalAuraLevel5Penalty);
        }

        private float GetClientFloor()
        {
            Entity? entity = capi?.World?.Player?.Entity;
            if (entity == null)
            {
                return Config.TemporalAuraMinFinalStabilityLevel5;
            }

            return entity.WatchedAttributes.GetFloat("stench:tempaurafloor", Config.TemporalAuraMinFinalStabilityLevel5);
        }

        private float GetRawServerTemporalStability(double x, double y, double z)
        {
            if (serverTemporalSystem == null)
            {
                return 0f;
            }

            suppressServerHook = true;
            try
            {
                return serverTemporalSystem.GetTemporalStability(x, y, z);
            }
            finally
            {
                suppressServerHook = false;
            }
        }

        private float GetRawClientTemporalStability(double x, double y, double z)
        {
            if (clientTemporalSystem == null)
            {
                return 0f;
            }

            suppressClientHook = true;
            try
            {
                return clientTemporalSystem.GetTemporalStability(x, y, z);
            }
            finally
            {
                suppressClientHook = false;
            }
        }

        private static float ApplyAuraToStability(float baseStability, float rawPenalty, float strength, float floor, out float appliedPenalty)
        {
            float finalStability = baseStability;

            if (rawPenalty > 0f)
            {
                finalStability -= rawPenalty;
                if (floor > 0f && strength > 0f)
                {
                    float normalized = Math.Clamp(rawPenalty / strength, 0f, 1f);
                    float effectiveFloor = 1f - normalized * (1f - floor);
                    finalStability = Math.Min(finalStability, effectiveFloor);
                }
            }

            appliedPenalty = Math.Max(0f, baseStability - finalStability);
            return finalStability;
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            if (player?.Entity != null)
            {
                RemoveEmitter(player.Entity.EntityId);
            }
        }
    }
}
