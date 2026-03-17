using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace StenchMod.CollectibleBehaviors
{
    public class ConsiderPetFoodBehavior : CollectibleBehavior
    {
        public ConsiderPetFoodBehavior(CollectibleObject collObj) : base(collObj)
        {
        }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }
    }
}
