using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace StenchMod.Systems
{
    /// <summary>
    /// Defines and spawns stench particle effects around entities at levels 3, 4 and 5.
    /// All particle property objects are static and initialized once to avoid GC pressure.
    /// </summary>
    public static class StenchParticleSystem
    {
        /// <summary>Particle properties used at stench level 3 (light, faint haze).</summary>
        public static SimpleParticleProperties StenchParticlesLv3 = null!;

        /// <summary>Particle properties used at stench level 4 (sparse, greenish-brown).</summary>
        public static SimpleParticleProperties StenchParticlesLv4 = null!;

        /// <summary>Particle properties used at stench level 5 (denser, darker).</summary>
        public static SimpleParticleProperties StenchParticlesLv5 = null!;

        private static bool _initialized = false;

        /// <summary>
        /// Must be called once before <see cref="SpawnAround"/> is used.
        /// Initialises the particle property objects.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // -----------------------------------------------------------------
            // Level 3 — light, faint, subtle haze
            // -----------------------------------------------------------------
            StenchParticlesLv3 = new SimpleParticleProperties(
                0.06f,
                0.20f,
                ColorUtil.ColorFromRgba(95, 135, 55, 90),
                new Vec3d(),
                new Vec3d(0.6, 1.8, 0.6),
                new Vec3f(-0.03f,  0.03f, -0.03f),
                new Vec3f( 0.06f,  0.06f,  0.06f)
            )
            {
                LifeLength        = 1.2f,
                GravityEffect     = -0.01f,
                MinSize           = 0.04f,
                MaxSize           = 0.10f,
                ParticleModel     = EnumParticleModel.Quad,
                ShouldDieInAir    = false,
                ShouldDieInLiquid = true,
                OpacityEvolve     = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.9f)
            };

            // -----------------------------------------------------------------
            // Level 4 — clearly visible, thicker and more numerous than before
            // -----------------------------------------------------------------
            StenchParticlesLv4 = new SimpleParticleProperties(
                0.45f,
                1.20f,
                ColorUtil.ColorFromRgba(78, 120, 36, 145),
                new Vec3d(),
                new Vec3d(0.6, 1.8, 0.6),
                new Vec3f(-0.07f,  0.05f, -0.07f),
                new Vec3f( 0.14f,  0.12f,  0.14f)
            )
            {
                LifeLength        = 1.8f,
                GravityEffect     = -0.025f,
                MinSize           = 0.07f,
                MaxSize           = 0.19f,
                ParticleModel     = EnumParticleModel.Quad,
                ShouldDieInAir   = false,
                ShouldDieInLiquid = true,
                OpacityEvolve     = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.65f)
            };

            // -----------------------------------------------------------------
            // Level 5 — very visible, dense and dark
            // -----------------------------------------------------------------
            StenchParticlesLv5 = new SimpleParticleProperties(
                1.40f,
                3.20f,
                ColorUtil.ColorFromRgba(55, 78, 16, 175),
                new Vec3d(),
                new Vec3d(0.6, 1.8, 0.6),
                new Vec3f(-0.10f,  0.06f, -0.10f),
                new Vec3f( 0.20f,  0.18f,  0.20f)
            )
            {
                LifeLength        = 2.2f,
                GravityEffect     = -0.04f,
                MinSize           = 0.10f,
                MaxSize           = 0.28f,
                ParticleModel     = EnumParticleModel.Quad,
                ShouldDieInAir    = false,
                ShouldDieInLiquid = true,
                OpacityEvolve     = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -0.5f)
            };
        }

        /// <summary>
        /// Spawns stench particles around the given entity using the properties
        /// appropriate for the supplied stench level.
        /// Call this server-side; particles are automatically broadcast to nearby clients.
        /// </summary>
        /// <param name="entity">The entity at whose position particles should appear.</param>
        /// <param name="level">Stench level (3, 4 or 5). Values below 3 are ignored.</param>
        public static void SpawnAround(EntityAgent entity, int level)
        {
            if (!_initialized || level < 3 || entity?.World == null)
                return;

            SimpleParticleProperties props = level switch
            {
                >= 5 => StenchParticlesLv5,
                4 => StenchParticlesLv4,
                _ => StenchParticlesLv3
            };

            // Position particles around the entity's feet, spanning full body height
            props.MinPos.Set(
                entity.Pos.X - 0.3,
                entity.Pos.Y,
                entity.Pos.Z - 0.3
            );
            props.AddPos.Set(0.6, 1.8, 0.6);

            entity.World.SpawnParticles(props);
        }
    }
}
