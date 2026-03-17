using System;
using System.Collections.Generic;
using StenchMod.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace StenchMod.Behaviors
{
    /// <summary>
    /// Server-side drifter state for stench-aware targeting.
    /// Tracks per-player provocation so custom drifter AI tasks can decide
    /// whether a specific player is a valid target.
    /// </summary>
    public class EntityBehaviorDrifterStenchAI : EntityBehavior
    {
        private const string AttrLevel = "stench:level";
        private const long RetaliationWindowMs = 30_000;

        private readonly Dictionary<long, RetaliationMemory> retaliationByPlayer = new();
        private bool enabledForEntity;

        private StenchConfig Config => StenchModSystem.Config;

        public EntityBehaviorDrifterStenchAI(Entity entity) : base(entity) { }

        public override string PropertyName() => "stench.drifterai";

        public override void Initialize(EntityProperties properties, Vintagestory.API.Datastructures.JsonObject typeAttributes)
        {
            base.Initialize(properties, typeAttributes);

            if (entity.Api.Side != EnumAppSide.Server || entity is not EntityAgent)
                return;

            string entityPath = entity.Code?.Path ?? string.Empty;
            enabledForEntity = Config.EnableDrifterAI && ContainsAny(entityPath, Config.AffectedDrifters);

        }

        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (!enabledForEntity || retaliationByPlayer.Count == 0)
                return;

            CleanupRetaliationMemory();
        }

        public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
        {
            base.OnEntityReceiveDamage(damageSource, ref damage);

            if (!enabledForEntity)
                return;

            if (damageSource.GetCauseEntity() is not EntityPlayer attacker || !attacker.Alive)
                return;

            long nowMs = entity.World.ElapsedMilliseconds;
            long key = attacker.EntityId;
            retaliationByPlayer.TryGetValue(key, out RetaliationMemory memory);
            memory.HitCount = Math.Min(memory.HitCount + 1, 8);
            memory.LastHitMs = nowMs;
            retaliationByPlayer[key] = memory;

        }

        public bool CanTargetEntity(Entity candidate, float baseRange, bool applyRandomIgnore)
        {
            if (!enabledForEntity || candidate is not EntityPlayer player || !player.Alive)
                return true;

            CleanupRetaliationMemory();

            int level = GetPlayerStenchLevel(player);
            if (level < 5)
                return true;

            if (!IsWithinRange(player, baseRange))
            {
                return false;
            }

            bool provoked = GetRetaliationHits(player) >= 1;
            return provoked;
        }

        public EntityPlayer? FindFreezeTarget(float range)
        {
            if (!enabledForEntity)
                return null;

            EntityPlayer? nearestPlayer = null;
            double nearestDistanceSq = double.MaxValue;

            foreach (IPlayer playerRef in entity.World.AllPlayers)
            {
                if (playerRef?.Entity is not EntityPlayer player || !player.Alive)
                    continue;

                if (!ShouldFreezeTarget(player, range))
                    continue;

                double distanceSq = entity.Pos.SquareDistanceTo(player.Pos.XYZ);
                if (distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearestPlayer = player;
                }
            }

            return nearestPlayer;
        }

        public bool ShouldFreezeTarget(EntityPlayer player, float range)
        {
            if (!enabledForEntity || !player.Alive)
                return false;

            int level = GetStenchLevel(player);
            if (level < 5)
                return false;

            if (!IsWithinRange(player, range))
                return false;

            return GetRetaliationHits(player) == 0;
        }

        public int GetStenchLevel(EntityPlayer player)
        {
            return GetPlayerStenchLevel(player);
        }

        private bool IsWithinRange(EntityPlayer player, float range)
        {
            if (range <= 0f)
                return true;

            return entity.Pos.DistanceTo(player.Pos.XYZ) <= range;
        }

        private int GetRetaliationHits(EntityPlayer player)
        {
            if (!retaliationByPlayer.TryGetValue(player.EntityId, out RetaliationMemory memory))
                return 0;

            long nowMs = entity.World.ElapsedMilliseconds;
            if (nowMs - memory.LastHitMs > RetaliationWindowMs)
            {
                retaliationByPlayer.Remove(player.EntityId);
                return 0;
            }

            return memory.HitCount;
        }

        private void CleanupRetaliationMemory()
        {
            if (retaliationByPlayer.Count == 0)
                return;

            long nowMs = entity.World.ElapsedMilliseconds;
            List<long>? expired = null;

            foreach ((long playerId, RetaliationMemory memory) in retaliationByPlayer)
            {
                if (nowMs - memory.LastHitMs > RetaliationWindowMs)
                {
                    expired ??= new List<long>();
                    expired.Add(playerId);
                }
            }

            if (expired == null)
                return;

            foreach (long playerId in expired)
            {
                retaliationByPlayer.Remove(playerId);
            }
        }

        private static int GetPlayerStenchLevel(EntityPlayer player)
        {
            EntityBehaviorStench? behavior = player.GetBehavior<EntityBehaviorStench>();
            if (behavior != null)
            {
                return Math.Clamp(behavior.GetCurrentLevel(), 1, 5);
            }

            return Math.Clamp(player.WatchedAttributes.GetInt(AttrLevel, 1), 1, 5);
        }

        private static bool ContainsAny(string source, List<string> values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value)
                    && source.Contains(value, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private struct RetaliationMemory
        {
            public int HitCount;
            public long LastHitMs;
        }
    }
}
