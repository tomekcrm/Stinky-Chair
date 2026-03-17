using StenchMod.BlockEntities;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace StenchMod.Blocks
{
    /// <summary>
    /// Invisible helper block placed at Y+1 and Y+2 above a <see cref="BlockBarrelShower"/>.
    /// Has a solid collision box so the game prevents other blocks from being placed
    /// into the shower's volume, and so that the player receives <c>OnEntityInside</c>
    /// callbacks when standing in the upper sections.
    /// <para>
    /// Breaking this block delegates to breaking the main block, which in turn removes
    /// all sections and drops the item.
    /// </para>
    /// </summary>
    public class BlockBarrelShowerPart : Block
    {
        public override void OnBlockBroken(
            IWorldAccessor world,
            BlockPos pos,
            IPlayer byPlayer,
            float dropQuantityMultiplier)
        {
            // Find the main block below and break that instead.
            BlockPos? mainPos = FindMainBlockPos(world, pos);
            if (mainPos != null)
            {
                world.BlockAccessor.BreakBlock(mainPos, byPlayer, dropQuantityMultiplier);
                // The main block's OnBlockBroken will remove this section.
            }
            else
            {
                // Orphan section – remove silently.
                world.BlockAccessor.SetBlock(0, pos);
            }
        }

        /// <summary>
        /// Searches downward (up to <see cref="BlockBarrelShower.SectionCount"/> blocks)
        /// for the main barrel-shower block that owns this section.
        /// </summary>
        private static BlockPos? FindMainBlockPos(IWorldAccessor world, BlockPos partPos)
        {
            const int maxSearch = 3;
            for (int dy = 1; dy <= maxSearch; dy++)
            {
                BlockPos candidate = partPos.AddCopy(0, -dy, 0);
                if (world.BlockAccessor.GetBlock(candidate) is BlockBarrelShower)
                    return candidate;
            }
            return null;
        }

        /// <summary>
        /// Forwards right-click interactions to the main block:
        /// empty hand → toggle valve; water container → fill; empty container → drain.
        /// </summary>
        public override bool OnBlockInteractStart(
            IWorldAccessor world,
            IPlayer byPlayer,
            BlockSelection blockSel)
        {
            if (world.Side != EnumAppSide.Server) return true;

            BlockPos? mainPos = FindMainBlockPos(world, blockSel.Position);
            if (mainPos == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            BEBarrelShower? be = world.BlockAccessor.GetBlockEntity<BEBarrelShower>(mainPos);
            if (be == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

            ItemSlot handSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (handSlot.Empty)
                return BlockBarrelShower.TryToggleShower(world, byPlayer, be, mainPos);

            if (BlockBarrelShower.TryFillShower(world, byPlayer, handSlot, be, mainPos)) return true;
            if (BlockBarrelShower.TryDrainShower(world, byPlayer, handSlot, be, mainPos)) return true;

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        /// <summary>
        /// Shows the same hover info as the main barrel-shower block.
        /// </summary>
        public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
        {
            BlockPos? mainPos = FindMainBlockPos(world, pos);
            BEBarrelShower? be = mainPos != null
                ? world.BlockAccessor.GetBlockEntity<BEBarrelShower>(mainPos)
                : null;
            if (be == null) return string.Empty;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            be.AppendInfo(sb);
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Forwards the <c>OnEntityInside</c> event to the main block so that
        /// washing applies even when a player stands in the upper barrel sections.
        /// </summary>
        public override void OnEntityInside(IWorldAccessor world, Vintagestory.API.Common.Entities.Entity entity, BlockPos pos)
        {
            BlockPos? mainPos = FindMainBlockPos(world, pos);
            if (mainPos == null) return;

            Block? mainBlock = world.BlockAccessor.GetBlock(mainPos);
            mainBlock?.OnEntityInside(world, entity, mainPos);
        }
    }

    // ---------------------------------------------------------------------------
    // BlockEntity to store the reference to the main block's position.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Lightweight block entity for <see cref="BlockBarrelShowerPart"/>.
    /// Persists the position of the owning <see cref="BlockBarrelShower"/> so that
    /// we can find the main block quickly without searching.
    /// </summary>
    public class BEBarrelShowerPart : BlockEntity
    {
        private const string AttrMainPos = "mainBlockPos";

        public BlockPos? MainBlockPos { get; set; }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (MainBlockPos != null)
            {
                tree.SetInt(AttrMainPos + "X", MainBlockPos.X);
                tree.SetInt(AttrMainPos + "Y", MainBlockPos.Y);
                tree.SetInt(AttrMainPos + "Z", MainBlockPos.Z);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            if (tree.HasAttribute(AttrMainPos + "X"))
            {
                MainBlockPos = new BlockPos(
                    tree.GetInt(AttrMainPos + "X"),
                    tree.GetInt(AttrMainPos + "Y"),
                    tree.GetInt(AttrMainPos + "Z"));
            }
        }
    }
}
