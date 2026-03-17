using System;
using StenchMod.Behaviors;
using StenchMod.BlockEntities;
using StenchMod.Systems;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace StenchMod.Blocks
{
    /// <summary>
    /// The main barrel-shower block (placed at Y=0).  This block:
    /// <list type="bullet">
    ///   <item>Places two invisible <see cref="BlockBarrelShowerPart"/> helper blocks at Y+1 and Y+2
    ///         when placed, and removes them when broken.</item>
    ///   <item>Stores water via <see cref="BEBarrelShower"/>.</item>
    ///   <item>Accepts water from buckets and watering cans on right-click.</item>
    ///   <item>Washes any <see cref="EntityBehaviorStench"/> entity that stands inside.</item>
    /// </list>
    /// </summary>
    public class BlockBarrelShower : Block
    {
        /// <summary>Number of invisible helper sections placed above the main block.</summary>
        private const int SectionCount = 2;

        private static readonly AssetLocation PartLoc      = new AssetLocation("stench:barrel-shower-part");
        private static readonly AssetLocation TopLoc       = new AssetLocation("stench:barrel-shower-top");
        private static readonly AssetLocation[] SoundsEmpty =
        {
            new AssetLocation("stench:sounds/block/shower-empty1"),
            new AssetLocation("stench:sounds/block/shower-empty2"),
            new AssetLocation("stench:sounds/block/shower-empty3"),
        };

        // -----------------------------------------------------------------------
        // Placement — verify headroom, then place helper sections
        // -----------------------------------------------------------------------

        public override bool TryPlaceBlock(
            IWorldAccessor world,
            IPlayer byPlayer,
            ItemStack itemstack,
            BlockSelection blockSel,
            ref string failureCode)
        {
            BlockPos basePos = blockSel.Position;

            for (int i = 1; i <= SectionCount; i++)
            {
                BlockPos above = basePos.AddCopy(0, i, 0);
                Block occupant = world.BlockAccessor.GetBlock(above);
                if (occupant.Replaceable < 6000)
                {
                    failureCode = "notsufficientspace";
                    return false;
                }
            }

            bool placed = base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
            if (!placed) return false;

            PlaceSections(world, basePos);
            return true;
        }

        private void PlaceSections(IWorldAccessor world, BlockPos basePos)
        {
            Block partBlock = world.GetBlock(PartLoc);
            Block topBlock  = world.GetBlock(TopLoc);

            if (partBlock == null || topBlock == null)
            {
                world.Api.Logger.Warning("[StenchMod] Could not find shower section blocks – sections skipped.");
                return;
            }

            // Y+1 = open middle section (barrel-shower-part)
            // Y+2 = solid top barrel (barrel-shower-top)
            Block[] sectionBlocks = { partBlock, topBlock };

            for (int i = 1; i <= SectionCount; i++)
            {
                BlockPos pos = basePos.AddCopy(0, i, 0);
                world.BlockAccessor.SetBlock(sectionBlocks[i - 1].BlockId, pos);

                if (world.BlockAccessor.GetBlockEntity(pos) is BEBarrelShowerPart part)
                {
                    part.MainBlockPos = basePos.Copy();
                }
            }
        }

        // -----------------------------------------------------------------------
        // Breaking — remove helper sections first
        // -----------------------------------------------------------------------

        public override void OnBlockBroken(
            IWorldAccessor world,
            BlockPos pos,
            IPlayer byPlayer,
            float dropQuantityMultiplier)
        {
            RemoveSections(world, pos);
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        private void RemoveSections(IWorldAccessor world, BlockPos basePos)
        {
            for (int i = 1; i <= SectionCount; i++)
            {
                BlockPos above = basePos.AddCopy(0, i, 0);
                AssetLocation? code = world.BlockAccessor.GetBlock(above)?.Code;
                if (code?.Equals(PartLoc) == true || code?.Equals(TopLoc) == true)
                {
                    world.BlockAccessor.SetBlock(0, above);
                }
            }
        }

        // -----------------------------------------------------------------------
        // Interaction — fill / drain with bucket or watering can
        // -----------------------------------------------------------------------

        public override bool OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel)
        {
            if (world.Side != EnumAppSide.Server) return true;

            BEBarrelShower? be = world.BlockAccessor.GetBlockEntity<BEBarrelShower>(blockSel.Position);
            if (be == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            ItemSlot handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (handSlot.Empty)
                return TryToggleShower(world, byPlayer, be, blockSel.Position);

            if (TryFillShower(world, byPlayer, handSlot, be, blockSel.Position)) return true;
            if (TryDrainShower(world, byPlayer, handSlot, be, blockSel.Position)) return true;

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        /// <summary>
        /// Toggles the shower active/inactive when right-clicked with empty hand.
        /// </summary>
        internal static bool TryToggleShower(
            IWorldAccessor world,
            IPlayer byPlayer,
            BEBarrelShower be,
            BlockPos pos)
        {
            if (be.CurrentLitres <= 0f)
            {
                if (byPlayer is IServerPlayer serverPlayerDry)
                {
                    serverPlayerDry.SendMessage(
                        GlobalConstants.GeneralChatGroup,
                        Lang.Get("stench:barrelshower-empty"),
                        EnumChatType.Notification,
                        "");
                }
                world.PlaySoundAt(SoundsEmpty[world.Rand.Next(SoundsEmpty.Length)],
                    pos.X + 0.5, pos.Y + 1.5, pos.Z + 0.5,
                    byPlayer, randomizePitch: true, range: 16f, volume: 1f);
                return true;
            }

            bool nowActive = be.ToggleActive();
            string msgKey = nowActive ? "stench:barrelshower-on" : "stench:barrelshower-off";
            if (byPlayer is IServerPlayer serverPlayerMsg)
            {
                serverPlayerMsg.SendMessage(
                    GlobalConstants.GeneralChatGroup,
                    Lang.Get(msgKey),
                    EnumChatType.Notification,
                    "");
            }

            world.PlaySoundAt(
                new AssetLocation("game:sounds/effect/water-pour"),
                pos.X + 0.5, pos.Y + 1.5, pos.Z + 0.5,
                byPlayer, randomizePitch: true, range: 16f, volume: 0.7f);

            return true;
        }

        /// <summary>
        /// Transfers liquid from a held container item into the shower.
        /// </summary>
        internal static bool TryFillShower(
            IWorldAccessor world,
            IPlayer byPlayer,
            ItemSlot handSlot,
            BEBarrelShower be,
            BlockPos pos)
        {
            if (!be.CanAcceptLiquid) return false;
            if (handSlot.Itemstack?.Collectible is not BlockLiquidContainerBase liqContainer) return false;

            ItemStack? content = liqContainer.GetContent(handSlot.Itemstack);
            if (content == null) return false;

            WaterTightContainableProps? props = BlockLiquidContainerBase.GetContainableProps(content);
            if (props == null || props.ItemsPerLitre <= 0f) return false;
            float litresPerItem = 1f / props.ItemsPerLitre;

            float litresAvailable = content.StackSize * litresPerItem;
            float added = be.TryAddLiquid(content, litresAvailable);
            if (added <= 0f) return false;

            // Consume exactly the transferred amount from the container.
            int itemsConsumed = (int)Math.Ceiling(added / litresPerItem);
            ItemStack updatedContent = content.Clone();
            updatedContent.StackSize = Math.Max(0, updatedContent.StackSize - itemsConsumed);
            liqContainer.SetContent(handSlot.Itemstack, updatedContent.StackSize > 0 ? updatedContent : null);
            handSlot.MarkDirty();

            PlayFillSound(world, props, pos, byPlayer);
            world.Api.Logger.Notification(
                "[StenchMod] BarrelShower filled +{0:F2}L → {1:F2}/{2:F0}L",
                added, be.CurrentLitres, be.CapacityLitres);
            return true;
        }

        /// <summary>
        /// Transfers liquid from the shower into an empty held container item.
        /// </summary>
        internal static bool TryDrainShower(
            IWorldAccessor world,
            IPlayer byPlayer,
            ItemSlot handSlot,
            BEBarrelShower be,
            BlockPos pos)
        {
            if (be.CurrentLitres <= 0f || be.LiquidCode == null) return false;
            if (handSlot.Itemstack?.Collectible is not BlockLiquidContainerBase liqContainer) return false;
            if (liqContainer.GetContent(handSlot.Itemstack) != null) return false; // container already has liquid

            // Build a stack representing the liquid stored in the shower.
            CollectibleObject? liquidCollectible = world.GetItem(be.LiquidCode)
                ?? (CollectibleObject?)world.GetBlock(be.LiquidCode);
            if (liquidCollectible == null) return false;

            WaterTightContainableProps? props = BlockLiquidContainerBase.GetContainableProps(
                new ItemStack(liquidCollectible));
            if (props == null || props.ItemsPerLitre <= 0f) return false;
            float litresPerItem = 1f / props.ItemsPerLitre;

            float containerCap = liqContainer.CapacityLitres;
            float litresToTake = Math.Min(be.CurrentLitres, containerCap);
            if (litresToTake <= 0f) return false;

            int itemCount = (int)(litresToTake / litresPerItem);
            if (itemCount <= 0) return false;

            float actualLitres = itemCount * litresPerItem;
            ItemStack fillWith = new ItemStack(liquidCollectible, itemCount);
            liqContainer.SetContent(handSlot.Itemstack, fillWith);
            be.TakeLiquid(actualLitres);
            handSlot.MarkDirty();

            PlayFillSound(world, props, pos, byPlayer);
            return true;
        }

        private static void PlayFillSound(
            IWorldAccessor world,
            WaterTightContainableProps props,
            BlockPos pos,
            IPlayer byPlayer)
        {
            if (props.FillSound == null) return;
            world.PlaySoundAt(
                props.FillSound,
                pos.X + 0.5,
                pos.Y + 1.5,
                pos.Z + 0.5,
                byPlayer,
                randomizePitch: true,
                range: 16f,
                volume: 1f);
        }

        // -----------------------------------------------------------------------
        // Entity inside — stench washing
        // -----------------------------------------------------------------------

        public override void OnEntityInside(IWorldAccessor world, Entity entity, BlockPos pos)
        {
            if (world.Side != EnumAppSide.Server) return;
            if (entity is not EntityPlayer player) return;

            // Only wash when the player is physically inside the shower (feet below Y+2).
            // Feet at pos.Y+2 or higher means standing on top of the structure.
            if (entity.Pos.Y >= pos.Y + 2.0) return;

            BEBarrelShower? be = world.BlockAccessor.GetBlockEntity<BEBarrelShower>(pos);
            if (be == null || be.CurrentLitres <= 0f || !be.IsActive) return;

            EntityBehaviorStench? stench = player.GetBehavior<EntityBehaviorStench>();
            if (stench == null) return;

            // OnEntityInside runs every game tick (~20 Hz).
            float washRate = StenchModSystem.Config.SwimmingReductionPerSecond;
            float waterCostPerPoint = Attributes?["waterCostPerWashPoint"].AsFloat(0.02f) ?? 0.02f;

            float removed = stench.ApplyExternalWash(washRate / 20f);
            if (removed > 0f)
            {
                be.ConsumeWater(removed * waterCostPerPoint);
                StenchWashParticleSystem.SpawnAround(entity);
            }
        }

        // -----------------------------------------------------------------------
        // Info overlay
        // -----------------------------------------------------------------------

        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            // Info is built by BEBarrelShower.GetBlockInfo — return empty to avoid duplication.
            return string.Empty;
        }

    }
}
