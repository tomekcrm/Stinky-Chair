using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace StenchMod.Systems
{
    /// <summary>
    /// Short-lived water-cleaning particles shown while a target is being washed.
    /// </summary>
    public static class StenchWashParticleSystem
    {
        private static bool initialized;
        private static SimpleParticleProperties washParticles = null!;
        private static SimpleParticleProperties washStreamParticles = null!;
        private static SimpleParticleProperties showerDropParticles = null!;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            // Water droplets falling from the showerhead — same colour as wash particles.
            showerDropParticles = new SimpleParticleProperties(
                4f, 9f,
                ColorUtil.ColorFromRgba(255, 245, 235, 125),
                new Vec3d(),
                new Vec3d(0.6, 0.05, 0.6),
                new Vec3f(-0.06f, -2.2f, -0.06f),
                new Vec3f(0.12f, 0.5f, 0.12f))
            {
                LifeLength = 0.55f,
                GravityEffect = 1.3f,
                MinSize = 0.06f,
                MaxSize = 0.16f,
                ParticleModel = EnumParticleModel.Quad,
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.5f),
                ShouldDieInLiquid = false,
                ShouldDieInAir = false
            };

            washParticles = new SimpleParticleProperties(
                10f,
                18f,
                ColorUtil.ColorFromRgba(255, 245, 235, 125),
                new Vec3d(),
                new Vec3d(0.7, 1.4, 0.7),
                new Vec3f(-0.10f, 0.04f, -0.10f),
                new Vec3f(0.20f, 0.18f, 0.20f))
            {
                LifeLength = 0.9f,
                GravityEffect = -0.02f,
                MinSize = 0.05f,
                MaxSize = 0.14f,
                ParticleModel = EnumParticleModel.Quad,
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.85f),
                ShouldDieInLiquid = false,
                ShouldDieInAir = false
            };

            washStreamParticles = new SimpleParticleProperties(
                8f,
                14f,
                ColorUtil.ColorFromRgba(255, 165, 80, 220),
                new Vec3d(),
                new Vec3d(0.15, 0.15, 0.15),
                new Vec3f(-0.08f, -0.20f, -0.08f),
                new Vec3f(0.16f, 0.04f, 0.16f))
            {
                LifeLength = 0.35f,
                GravityEffect = 0.65f,
                MinSize = 0.03f,
                MaxSize = 0.08f,
                ParticleModel = EnumParticleModel.Quad,
                OpacityEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.9f),
                ShouldDieInLiquid = false,
                ShouldDieInAir = false
            };
        }

        public static void SpawnAround(Entity entity)
        {
            if (!initialized || entity?.World == null)
            {
                return;
            }

            double width = entity.SelectionBox?.XSize ?? 0.7;
            double height = entity.SelectionBox?.YSize ?? 1.8;
            double depth = entity.SelectionBox?.ZSize ?? 0.7;

            washParticles.MinPos.Set(
                entity.Pos.X - width * 0.5,
                entity.Pos.Y + Math.Min(0.2, height * 0.1),
                entity.Pos.Z - depth * 0.5);
            washParticles.AddPos.Set(width, Math.Max(0.8, height * 0.8), depth);

            entity.World.SpawnParticles(washParticles);
        }

        /// <summary>
        /// Spawns water droplets falling from the showerhead at the top of the shower structure.
        /// Should be called server-side on a regular tick while the shower is active.
        /// </summary>
        public static void SpawnShower(IWorldAccessor world, BlockPos basePos)
        {
            if (!initialized || world == null) return;

            showerDropParticles.MinPos.Set(basePos.X + 0.2, basePos.Y + 2.35, basePos.Z + 0.2);
            showerDropParticles.AddPos.Set(0.6, 0.05, 0.6);
            world.SpawnParticles(showerDropParticles);
        }

        public static void SpawnStream(EntityAgent washer, Entity target)
        {
            if (!initialized || washer?.World == null || target == null)
            {
                return;
            }

            Vec3d origin = new Vec3d(
                washer.Pos.X,
                washer.Pos.Y + washer.LocalEyePos.Y - 0.15,
                washer.Pos.Z);

            double targetHeight = target.SelectionBox?.Y2 ?? 1.4;
            Vec3d targetPos = new Vec3d(
                target.Pos.X,
                target.Pos.Y + targetHeight * 0.5,
                target.Pos.Z);

            Vec3d direction = targetPos.SubCopy(origin);
            double distance = Math.Max(0.25, direction.Length());
            direction.Normalize();

            washStreamParticles.MinPos.Set(
                origin.X + direction.X * 0.2,
                origin.Y + direction.Y * 0.2,
                origin.Z + direction.Z * 0.2);
            washStreamParticles.AddPos.Set(
                direction.X * Math.Min(distance, 1.8),
                direction.Y * Math.Min(distance, 1.8),
                direction.Z * Math.Min(distance, 1.8));

            washer.World.SpawnParticles(washStreamParticles);
        }
    }
}
