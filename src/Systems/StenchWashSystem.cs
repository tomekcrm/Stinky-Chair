using System;
using StenchMod.Behaviors;
using System.Collections.Concurrent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace StenchMod.Systems
{
    /// <summary>
    /// Shared helper for resolving stench-wash targets in front of an entity.
    /// Valid targets are any non-self entities carrying <see cref="EntityBehaviorStench"/>.
    /// </summary>
    public static class StenchWashSystem
    {
        public const float DefaultWashRange = 4f;
        public const string PlayerPropCodePath = "strawdummy";
        private const string WateringSecondsAttr = "wateringSeconds";
        private const float SessionRange = DefaultWashRange + 1.5f;
        private const long WashIntervalMs = 250;
        private const long SessionGraceMs = 1250;
        private static readonly ConcurrentDictionary<long, WashSession> ActiveWashSessions = new();

        private sealed class WashSession
        {
            public long TargetEntityId;
            public long LastApplyMs;
            public long LastRefreshMs;
            public long LastSoundMs;
            public float LastAppliedAmount;
            public string Source = "unknown";
        }

        public static bool IsWateringCan(ItemSlot? slot)
        {
            string? path = slot?.Itemstack?.Collectible?.Code?.Path;
            return path != null && path.Contains("wateringcan");
        }

        public static void BeginDirectWashSession(EntityAgent byEntity, Entity targetEntity, string source = "direct")
        {
            long nowMs = Environment.TickCount64;

            ActiveWashSessions.AddOrUpdate(byEntity.EntityId,
                _ => new WashSession
                {
                    TargetEntityId = targetEntity.EntityId,
                    LastApplyMs = 0,
                    LastRefreshMs = nowMs,
                    LastSoundMs = 0,
                    LastAppliedAmount = 0f,
                    Source = source
                },
                (_, session) =>
                {
                    session.TargetEntityId = targetEntity.EntityId;
                    session.LastRefreshMs = nowMs;
                    session.Source = source;
                    return session;
                });
        }

        public static float PrimeDirectWash(EntityAgent byEntity, Entity targetEntity, string source = "direct")
        {
            BeginDirectWashSession(byEntity, targetEntity, source);

            ItemSlot handSlot = byEntity switch
            {
                EntityPlayer player => player.RightHandItemSlot,
                _ => byEntity.RightHandItemSlot
            };

            if (!ActiveWashSessions.TryGetValue(byEntity.EntityId, out WashSession? session)
                || !TryConsumeWateringSeconds(byEntity, handSlot, WashIntervalMs / 1000f)
                || targetEntity.GetBehavior<EntityBehaviorStench>() is not EntityBehaviorStench targetBehavior)
            {
                return 0f;
            }

            return ApplyWash(session, byEntity, targetBehavior, Environment.TickCount64);
        }

        public static void ClearWashSession(EntityAgent byEntity)
        {
            ActiveWashSessions.TryRemove(byEntity.EntityId, out _);
        }

        public static bool TryGetActiveWashTarget(
            EntityAgent byEntity,
            float range,
            out Entity targetEntity,
            out EntityBehaviorStench targetBehavior,
            out EntitySelection entitySelection)
        {
            targetEntity = null!;
            targetBehavior = null!;
            entitySelection = null!;

            if (!ActiveWashSessions.TryGetValue(byEntity.EntityId, out WashSession? session))
            {
                return false;
            }

            if (byEntity.World.GetEntityById(session.TargetEntityId) is not Entity entity
                || entity.EntityId == byEntity.EntityId
                || entity.GetBehavior<EntityBehaviorStench>() is not EntityBehaviorStench behavior)
            {
                ActiveWashSessions.TryRemove(byEntity.EntityId, out _);
                return false;
            }

            Vec3d eyePos = new Vec3d(
                byEntity.SidedPos.X,
                byEntity.SidedPos.Y + byEntity.LocalEyePos.Y,
                byEntity.SidedPos.Z);

            if (entity.ServerPos.XYZ.DistanceTo(eyePos) > range)
            {
                ActiveWashSessions.TryRemove(byEntity.EntityId, out _);
                return false;
            }

            targetEntity = entity;
            targetBehavior = behavior;
            entitySelection = new EntitySelection { Entity = entity };
            return true;
        }

        public static void TickServer(ICoreServerAPI api)
        {
            long nowMs = Environment.TickCount64;

            foreach ((long washerEntityId, WashSession session) in ActiveWashSessions.ToArray())
            {
                if (api.World.GetEntityById(washerEntityId) is not EntityAgent washer)
                {
                    ActiveWashSessions.TryRemove(washerEntityId, out _);
                    continue;
                }

                ItemSlot? handSlot = washer switch
                {
                    EntityPlayer player => player.RightHandItemSlot,
                    _ => washer.RightHandItemSlot
                };

                bool isUsingHand = washer.Controls.RightMouseDown || washer.Controls.HandUse != EnumHandInteract.None;
                bool insideGraceWindow = nowMs - session.LastRefreshMs <= SessionGraceMs;

                if (!IsWateringCan(handSlot) || (!isUsingHand && !insideGraceWindow))
                {
                    ActiveWashSessions.TryRemove(washerEntityId, out _);
                    continue;
                }

                if (!TryGetActiveWashTarget(washer, SessionRange, out Entity targetEntity, out EntityBehaviorStench targetBehavior, out _))
                {
                    ActiveWashSessions.TryRemove(washerEntityId, out _);
                    continue;
                }

                if (!IsStillAimedAt(washer, targetEntity))
                {
                    ActiveWashSessions.TryRemove(washerEntityId, out _);
                    continue;
                }

                if (nowMs - session.LastApplyMs < WashIntervalMs)
                {
                    MaybeEmitWashFeedback(washer, targetEntity, session, nowMs);
                    continue;
                }

                if (!TryConsumeWateringSeconds(washer, handSlot, WashIntervalMs / 1000f))
                {
                    ActiveWashSessions.TryRemove(washerEntityId, out _);
                    continue;
                }

                ApplyWash(session, washer, targetBehavior, nowMs);
            }
        }

        public static bool TryGetSessionDebug(
            EntityAgent byEntity,
            out Entity targetEntity,
            out EntityBehaviorStench targetBehavior,
            out string source,
            out string appliedText)
        {
            targetEntity = null!;
            targetBehavior = null!;
            source = "-";
            appliedText = "-";

            if (!ActiveWashSessions.TryGetValue(byEntity.EntityId, out WashSession? session))
            {
                return false;
            }

            if (!TryGetActiveWashTarget(byEntity, SessionRange, out targetEntity, out targetBehavior, out _))
            {
                return false;
            }

            source = session.Source;
            appliedText = session.LastAppliedAmount > 0f ? session.LastAppliedAmount.ToString("F2") : "0.00";
            return true;
        }

        private static float ApplyWash(WashSession session, EntityAgent washer, EntityBehaviorStench targetBehavior, long nowMs)
        {
            float washAmount = StenchModSystem.Config.SwimmingReductionPerSecond * (WashIntervalMs / 1000f);
            float removed = targetBehavior.ApplyExternalWash(washAmount);
            session.LastApplyMs = nowMs;
            session.LastAppliedAmount = removed;

            // Water stream and wash audio should continue while the can is actively pouring,
            // even if the target is already fully clean.
            MaybeEmitWashFeedback(washer, targetBehavior.entity, session, nowMs, forceSound: true);

            if (removed > 0f)
            {
                StenchWashParticleSystem.SpawnAround(targetBehavior.entity);
            }

            return removed;
        }

        private static bool TryConsumeWateringSeconds(EntityAgent washer, ItemSlot handSlot, float deltaSeconds)
        {
            if (handSlot?.Itemstack?.Attributes is not ITreeAttribute attrs)
            {
                return false;
            }

            float current = attrs.GetFloat(WateringSecondsAttr, 0f);
            if (current <= 0f)
            {
                return false;
            }

            float next = Math.Max(0f, current - deltaSeconds);
            attrs.SetFloat(WateringSecondsAttr, next);
            handSlot.MarkDirty();
            washer.Api?.Logger.Notification("[StenchMod] Watering can drain: {0:0.###} -> {1:0.###}", current, next);
            if (washer is EntityPlayer player && player.Player?.InventoryManager != null)
            {
                player.Player.InventoryManager.BroadcastHotbarSlot();
            }
            return next > 0f;
        }

        private static void MaybeEmitWashFeedback(EntityAgent washer, Entity targetEntity, WashSession session, long nowMs, bool forceSound = false)
        {
            StenchWashParticleSystem.SpawnStream(washer, targetEntity);

            if (!forceSound && nowMs - session.LastSoundMs < 700)
            {
                return;
            }

            session.LastSoundMs = nowMs;
            StenchSoundSystem.TryPlayWash(washer.Api, washer, targetEntity);
        }

        private static bool IsStillAimedAt(EntityAgent washer, Entity targetEntity)
        {
            Vec3d eyePos = new Vec3d(
                washer.SidedPos.X,
                washer.SidedPos.Y + washer.LocalEyePos.Y,
                washer.SidedPos.Z);

            double targetHeight = targetEntity.SelectionBox?.Y2 ?? 1.4;
            Vec3d targetPos = new Vec3d(
                targetEntity.ServerPos.X,
                targetEntity.ServerPos.Y + targetHeight * 0.5,
                targetEntity.ServerPos.Z);

            Vec3d toTarget = targetPos.SubCopy(eyePos);
            double distance = toTarget.Length();
            if (distance > SessionRange)
            {
                return false;
            }

            toTarget.Normalize();
            Vec3d lookDir = new Vec3d(
                -GameMath.Sin(washer.SidedPos.Yaw) * GameMath.Cos(washer.SidedPos.Pitch),
                -GameMath.Sin(washer.SidedPos.Pitch),
                GameMath.Cos(washer.SidedPos.Yaw) * GameMath.Cos(washer.SidedPos.Pitch));

            double dot = lookDir.Dot(toTarget);
            return dot >= 0.88;
        }

        public static bool TryResolveTarget(
            EntityAgent byEntity,
            float range,
            out Entity targetEntity,
            out EntityBehaviorStench targetBehavior,
            out EntitySelection entitySelection,
            out BlockSelection blockSelection)
        {
            targetEntity = null!;
            targetBehavior = null!;
            entitySelection = null!;
            blockSelection = null!;

            Vec3d eyePos = new Vec3d(
                byEntity.SidedPos.X,
                byEntity.SidedPos.Y + byEntity.LocalEyePos.Y,
                byEntity.SidedPos.Z);

            EntitySelection? rayEntitySel = null;
            BlockSelection? rayBlockSel = null;

            byEntity.World.RayTraceForSelection(
                eyePos,
                byEntity.SidedPos.Pitch,
                byEntity.SidedPos.Yaw,
                range,
                ref rayBlockSel,
                ref rayEntitySel,
                null,
                candidate => candidate.EntityId != byEntity.EntityId && candidate.GetBehavior<EntityBehaviorStench>() != null);

            if (rayEntitySel?.Entity == null)
            {
                return false;
            }

            EntityBehaviorStench? behavior = rayEntitySel.Entity.GetBehavior<EntityBehaviorStench>();
            if (behavior == null)
            {
                return false;
            }

            targetEntity = rayEntitySel.Entity;
            targetBehavior = behavior;
            entitySelection = rayEntitySel;
            blockSelection = rayBlockSel!;
            return true;
        }

        public static string DescribeTarget(Entity? entity)
        {
            if (entity == null)
            {
                return "-";
            }

            return $"{entity.Code}#{entity.EntityId}";
        }
    }
}
