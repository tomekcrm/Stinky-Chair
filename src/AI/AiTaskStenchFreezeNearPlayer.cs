using System;
using StenchMod.Behaviors;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace StenchMod.AI
{
    /// <summary>
    /// High-priority drifter task inspired by the Cats mod's freezeNear task.
    /// When a smelly player is nearby and has not provoked the drifter, the drifter
    /// keeps facing that player and suppresses aggressive tasks.
    /// </summary>
    public class AiTaskStenchFreezeNearPlayer : AiTaskBase
    {
        private EntityPlayer? targetPlayer;
        private readonly float seekingRange;
        private long lastSearchTotalMs;

        public AiTaskStenchFreezeNearPlayer(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
            : base(entity, taskConfig, aiConfig)
        {
            seekingRange = taskConfig["seekingRange"].AsFloat(20f);
        }

        public override bool ShouldExecute()
        {
            if (lastSearchTotalMs + 750 > entity.World.ElapsedMilliseconds)
                return false;

            lastSearchTotalMs = entity.World.ElapsedMilliseconds;
            targetPlayer = GetBehavior()?.FindFreezeTarget(seekingRange);
            return targetPlayer != null;
        }

        public override void StartExecute()
        {
            base.StartExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            if (targetPlayer == null || !targetPlayer.Alive)
                return false;

            EntityBehaviorDrifterStenchAI? behavior = GetBehavior();
            if (behavior == null || !behavior.ShouldFreezeTarget(targetPlayer, seekingRange))
                return false;

            FaceTarget(targetPlayer, dt);
            return true;
        }

        public override void FinishExecute(bool cancelled)
        {
            targetPlayer = null;
            base.FinishExecute(cancelled);
        }

        private void FaceTarget(EntityPlayer player, float dt)
        {
            Vec3f targetVec = new Vec3f(
                (float)(player.ServerPos.X - entity.ServerPos.X),
                (float)(player.ServerPos.Y - entity.ServerPos.Y),
                (float)(player.ServerPos.Z - entity.ServerPos.Z)
            );

            float desiredYaw = (float)Math.Atan2(targetVec.X, targetVec.Z);
            float yawDist = GameMath.AngleRadDistance(entity.ServerPos.Yaw, desiredYaw);
            entity.ServerPos.Yaw += GameMath.Clamp(yawDist, -250 * dt, 250 * dt);
            entity.ServerPos.Yaw %= GameMath.TWOPI;
        }

        private EntityBehaviorDrifterStenchAI? GetBehavior()
        {
            return entity.GetBehavior<EntityBehaviorDrifterStenchAI>();
        }
    }
}
