using System;
using System.Collections.Concurrent;
using StenchMod.Behaviors;
using StenchMod.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace StenchMod.Blocks
{
    /// <summary>
    /// Extends the vanilla watering can with probe diagnostics and server-side
    /// wash-session refreshes. Vanilla block watering remains in the base class.
    /// </summary>
    public class BlockWateringCanProbe : BlockWateringCan
    {
        private const string AttrProbeActive = "stench:probeactive";
        private const string AttrProbePhase = "stench:probephase";
        private const string AttrProbeSeconds = "stench:probeseconds";
        private const string AttrProbeEntitySel = "stench:probeentitysel";
        private const string AttrProbeEntityPos = "stench:probeentitypos";
        private const string AttrProbeBlockSel = "stench:probeblocksel";
        private const string AttrProbeRayEntity = "stench:proberayentity";
        private const string AttrProbeRayBlock = "stench:proberayblock";
        private const string AttrProbeRayDistance = "stench:proberaydistance";
        private const string AttrProbeWashTarget = "stench:probewashtarget";
        private const string AttrProbeWashValue = "stench:probewashvalue";
        private const string AttrProbeWashLevel = "stench:probewashlevel";
        private const string AttrProbeWashApplied = "stench:probewashapplied";
        private const string AttrProbeWashFlow = "stench:probewashflow";

        private const float ProbeRange = StenchWashSystem.DefaultWashRange;
        private const float ProbeLogIntervalSeconds = 0.25f;

        private static readonly ConcurrentDictionary<long, int> LastLoggedBuckets = new();

        public override void OnHeldUseStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            EnumHandInteract useType,
            bool firstEvent,
            ref EnumHandHandling handling)
        {
            EntitySelection resolvedEntitySel = null!;
            BlockSelection resolvedBlockSel = null!;
            bool hasWashTarget = TryResolveWashTarget(byEntity, entitySel, out Entity targetEntity, out _, out resolvedEntitySel, out resolvedBlockSel);

            if (hasWashTarget)
            {
                StenchWashSystem.BeginDirectWashSession(byEntity, targetEntity, "held-use-start");
            }

            base.OnHeldUseStart(slot, byEntity, blockSel, entitySel, useType, firstEvent, ref handling);
            ProbeInteraction("use-start", 0f, slot, byEntity, blockSel ?? resolvedBlockSel, entitySel ?? resolvedEntitySel, $"first={firstEvent}, handling={handling}");
        }

        public override void OnHeldInteractStart(
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            bool firstEvent,
            ref EnumHandHandling handling)
        {
            bool hasWashTarget = TryResolveWashTarget(byEntity, entitySel, out Entity targetEntity, out _, out _, out _);
            if (hasWashTarget)
            {
                StenchWashSystem.BeginDirectWashSession(byEntity, targetEntity, "held-start");
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            ProbeInteraction("start", 0f, slot, byEntity, blockSel, entitySel, $"first={firstEvent}, handling={handling}");
        }

        public override bool OnHeldInteractStep(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel)
        {
            bool result = base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel);

            string flow = "block";
            if (TryResolveWashTarget(byEntity, entitySel, out Entity targetEntity, out _, out EntitySelection resolvedEntitySel, out BlockSelection resolvedBlockSel))
            {
                StenchWashSystem.BeginDirectWashSession(byEntity, targetEntity, "held-step");
                entitySel ??= resolvedEntitySel;
                blockSel ??= resolvedBlockSel;
                flow = "entity-session";
            }

            ProbeInteraction("step", secondsUsed, slot, byEntity, blockSel, entitySel, $"continue={result}, wash={flow}");
            return result;
        }

        public override void OnHeldInteractStop(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel)
        {
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel);
            ProbeInteraction("stop", secondsUsed, slot, byEntity, blockSel, entitySel, null);
            ClearInteractionState(byEntity);
        }

        public override bool OnHeldInteractCancel(
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            EnumItemUseCancelReason cancelReason)
        {
            bool result = base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason);
            ProbeInteraction("cancel", secondsUsed, slot, byEntity, blockSel, entitySel, $"reason={cancelReason}, allowCancel={result}");
            ClearInteractionState(byEntity);
            return result;
        }

        private static void ClearInteractionState(EntityAgent byEntity)
        {
            LastLoggedBuckets.TryRemove(byEntity.EntityId, out _);
            StenchWashSystem.ClearWashSession(byEntity);
        }

        private void ProbeInteraction(
            string phase,
            float secondsUsed,
            ItemSlot slot,
            EntityAgent byEntity,
            BlockSelection blockSel,
            EntitySelection entitySel,
            string? extra)
        {
            if (!StenchModSystem.Config.DebugMode || byEntity?.Api?.Side != EnumAppSide.Server)
            {
                return;
            }

            if (byEntity is not EntityPlayer player)
            {
                return;
            }

            ProbeSnapshot snapshot = BuildSnapshot(byEntity, blockSel, entitySel);
            WriteProbeAttributes(player, phase, secondsUsed, snapshot);

            if (!ShouldLog(byEntity.EntityId, phase, secondsUsed))
            {
                return;
            }

            byEntity.Api.Logger.Notification(
                "[StenchMod] WateringCan probe {0} player={1} seconds={2:F2} item={3} entitySel={4} blockSel={5} rayEntity={6} rayBlock={7} rayDist={8} washTarget={9} washValue={10} washLevel={11} washApplied={12}{13}",
                phase,
                player.PlayerUID,
                secondsUsed,
                slot?.Itemstack?.Collectible?.Code,
                snapshot.EntitySelectionSummary,
                snapshot.BlockSelectionSummary,
                snapshot.RayEntitySummary,
                snapshot.RayBlockSummary,
                snapshot.RayDistanceText,
                snapshot.WashTargetSummary,
                snapshot.WashValueText,
                snapshot.WashLevelText,
                snapshot.WashAppliedText,
                string.IsNullOrWhiteSpace(extra) ? string.Empty : $" ({extra})");
        }

        private static bool ShouldLog(long entityId, string phase, float secondsUsed)
        {
            if (phase != "step")
            {
                return true;
            }

            int bucket = (int)Math.Floor(secondsUsed / ProbeLogIntervalSeconds);
            if (LastLoggedBuckets.TryGetValue(entityId, out int previousBucket) && previousBucket == bucket)
            {
                return false;
            }

            LastLoggedBuckets[entityId] = bucket;
            return true;
        }

        private static bool TryResolveWashTarget(
            EntityAgent byEntity,
            EntitySelection? directEntitySel,
            out Entity targetEntity,
            out EntityBehaviorStench targetBehavior,
            out EntitySelection resolvedEntitySel,
            out BlockSelection resolvedBlockSel)
        {
            targetEntity = null!;
            targetBehavior = null!;
            resolvedEntitySel = null!;
            resolvedBlockSel = null!;

            if (directEntitySel?.Entity != null
                && directEntitySel.Entity.EntityId != byEntity.EntityId
                && directEntitySel.Entity.GetBehavior<EntityBehaviorStench>() is EntityBehaviorStench directBehavior)
            {
                targetEntity = directEntitySel.Entity;
                targetBehavior = directBehavior;
                resolvedEntitySel = directEntitySel;
                return true;
            }

            return StenchWashSystem.TryResolveTarget(byEntity, ProbeRange, out targetEntity, out targetBehavior, out resolvedEntitySel, out resolvedBlockSel);
        }

        private static ProbeSnapshot BuildSnapshot(EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
        {
            ProbeSnapshot snapshot = new ProbeSnapshot
            {
                EntitySelectionSummary = DescribeEntitySelection(entitySel),
                EntitySelectionPosition = DescribeEntitySelectionPosition(entitySel),
                BlockSelectionSummary = DescribeBlockSelection(blockSel)
            };

            BlockSelection rayBlockSel = null!;
            EntitySelection rayEntitySel = null!;

            Vec3d eyePos = new Vec3d(
                byEntity.SidedPos.X,
                byEntity.SidedPos.Y + byEntity.LocalEyePos.Y,
                byEntity.SidedPos.Z);

            byEntity.World.RayTraceForSelection(
                eyePos,
                byEntity.SidedPos.Pitch,
                byEntity.SidedPos.Yaw,
                ProbeRange,
                ref rayBlockSel,
                ref rayEntitySel,
                null,
                candidate => candidate.EntityId != byEntity.EntityId);

            double rayDistance = rayEntitySel?.Entity == null
                ? -1
                : rayEntitySel.Entity.ServerPos.XYZ.DistanceTo(eyePos);

            snapshot.RayEntitySummary = DescribeEntitySelection(rayEntitySel);
            snapshot.RayBlockSummary = DescribeBlockSelection(rayBlockSel);
            snapshot.RayDistanceText = rayDistance < 0 ? "-" : rayDistance.ToString("F2");

            if (StenchWashSystem.TryGetSessionDebug(byEntity, out Entity sessionEntity, out EntityBehaviorStench sessionBehavior, out string source, out string appliedText))
            {
                snapshot.WashTargetSummary = StenchWashSystem.DescribeTarget(sessionEntity);
                snapshot.WashValueText = sessionBehavior.GetCurrentValue().ToString("F1");
                snapshot.WashLevelText = sessionBehavior.GetCurrentLevel().ToString();
                snapshot.WashAppliedText = appliedText;
                snapshot.WashFlowText = source;
            }
            else if (TryResolveWashTarget(byEntity, entitySel, out Entity targetEntity, out EntityBehaviorStench targetBehavior, out _, out _))
            {
                snapshot.WashTargetSummary = StenchWashSystem.DescribeTarget(targetEntity);
                snapshot.WashValueText = targetBehavior.GetCurrentValue().ToString("F1");
                snapshot.WashLevelText = targetBehavior.GetCurrentLevel().ToString();
                snapshot.WashFlowText = blockSel != null ? "block+aim" : "aim";
            }
            else if (blockSel != null)
            {
                snapshot.WashFlowText = "block";
            }

            return snapshot;
        }

        private static void WriteProbeAttributes(EntityPlayer player, string phase, float secondsUsed, ProbeSnapshot snapshot)
        {
            int isActive = phase is "use-start" or "start" or "step" ? 1 : 0;

            player.WatchedAttributes.SetInt(AttrProbeActive, isActive);
            player.WatchedAttributes.SetString(AttrProbePhase, phase);
            player.WatchedAttributes.SetFloat(AttrProbeSeconds, secondsUsed);
            player.WatchedAttributes.SetString(AttrProbeEntitySel, snapshot.EntitySelectionSummary);
            player.WatchedAttributes.SetString(AttrProbeEntityPos, snapshot.EntitySelectionPosition);
            player.WatchedAttributes.SetString(AttrProbeBlockSel, snapshot.BlockSelectionSummary);
            player.WatchedAttributes.SetString(AttrProbeRayEntity, snapshot.RayEntitySummary);
            player.WatchedAttributes.SetString(AttrProbeRayBlock, snapshot.RayBlockSummary);
            player.WatchedAttributes.SetString(AttrProbeRayDistance, snapshot.RayDistanceText);
            player.WatchedAttributes.SetString(AttrProbeWashTarget, snapshot.WashTargetSummary);
            player.WatchedAttributes.SetString(AttrProbeWashValue, snapshot.WashValueText);
            player.WatchedAttributes.SetString(AttrProbeWashLevel, snapshot.WashLevelText);
            player.WatchedAttributes.SetString(AttrProbeWashApplied, snapshot.WashAppliedText);
            player.WatchedAttributes.SetString(AttrProbeWashFlow, snapshot.WashFlowText);
        }

        private static string DescribeEntitySelection(EntitySelection? entitySel)
        {
            if (entitySel?.Entity == null)
            {
                return "-";
            }

            return $"{entitySel.Entity.Code}#{entitySel.Entity.EntityId}";
        }

        private static string DescribeEntitySelectionPosition(EntitySelection? entitySel)
        {
            if (entitySel?.Position == null)
            {
                return "-";
            }

            return $"{entitySel.Position.X:F2}, {entitySel.Position.Y:F2}, {entitySel.Position.Z:F2}";
        }

        private static string DescribeBlockSelection(BlockSelection? blockSel)
        {
            if (blockSel?.Position == null)
            {
                return "-";
            }

            return $"{blockSel.Position.X}, {blockSel.Position.Y}, {blockSel.Position.Z}";
        }

        private sealed class ProbeSnapshot
        {
            public string EntitySelectionSummary = "-";
            public string EntitySelectionPosition = "-";
            public string BlockSelectionSummary = "-";
            public string RayEntitySummary = "-";
            public string RayBlockSummary = "-";
            public string RayDistanceText = "-";
            public string WashTargetSummary = "-";
            public string WashValueText = "-";
            public string WashLevelText = "-";
            public string WashAppliedText = "-";
            public string WashFlowText = "-";
        }
    }
}
