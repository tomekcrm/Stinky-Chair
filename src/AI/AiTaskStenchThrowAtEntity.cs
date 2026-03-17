using StenchMod.Behaviors;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace StenchMod.AI
{
    public class AiTaskStenchThrowAtEntity : AiTaskThrowAtEntity
    {
        private static readonly FieldInfo? MaxDistField =
            typeof(AiTaskThrowAtEntity).GetField("maxDist", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public AiTaskStenchThrowAtEntity(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig)
            : base(entity, taskConfig, aiConfig)
        {
        }

        public override bool IsTargetableEntity(Vintagestory.API.Common.Entities.Entity e, float range, bool ignoreEntityCode)
        {
            if (!base.IsTargetableEntity(e, range, ignoreEntityCode))
                return false;

            return GetBehavior()?.CanTargetEntity(e, range, applyRandomIgnore: true) ?? true;
        }

        public override bool ShouldExecute()
        {
            DropInvalidTarget(GetBaseThrowRange());
            return base.ShouldExecute();
        }

        public override bool ContinueExecute(float dt)
        {
            DropInvalidTarget(GetBaseThrowRange());
            if (targetEntity == null)
                return false;

            return base.ContinueExecute(dt);
        }

        private void DropInvalidTarget(float range)
        {
            if (targetEntity is EntityPlayer player
                && !(GetBehavior()?.CanTargetEntity(player, range, applyRandomIgnore: false) ?? true))
            {
                targetEntity = null;
                attackedByEntity = null;
                attackedByEntityMs = 0;
            }
        }

        private EntityBehaviorDrifterStenchAI? GetBehavior()
        {
            return entity.GetBehavior<EntityBehaviorDrifterStenchAI>();
        }

        private float GetBaseThrowRange()
        {
            if (MaxDistField?.GetValue(this) is float maxDist && maxDist > 0f)
                return maxDist;

            return 0f;
        }
    }
}
