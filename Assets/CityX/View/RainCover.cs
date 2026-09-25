using CityX.Character;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// A map from the sky down to the first thing a raindrop would hit, over
    /// the patch of city around the player. It is what lets rain behave like
    /// rain: nothing falls inside a building or under a highway deck, drops
    /// end -- and splash -- on the rooftop, skybridge or car they actually
    /// land on rather than on the ground far below, and the sound of it goes
    /// muffled when there is a roof overhead.
    ///
    /// THE TECHNIQUE IS THE SHIPPED-GAME ONE. Remember Me's rain renders "a
    /// depth map from above" and uses it both to keep rain out from under
    /// cover and to place splashes on real geometry (Lagarde, "Water drop 2b").
    /// A streamed, unbounded city has no depth pass to spare and no shadow
    /// camera looking down, so the same map is built the cheap way: a grid of
    /// downward ray casts, one per two metres, over a 48m window that follows
    /// the player. About 580 cells; a walking player crosses one cell in a
    /// quarter of a second, so only the row of cells that just came into the
    /// window is stale at any time, and everything else is re-checked on a
    /// slow age-out so a car driving in or a door closing shows up within a
    /// second.
    ///
    /// CELLS ARE WORLD-ALIGNED AND KEYED BY THEIR OWN COORDINATES, in a
    /// toroidal array: as the window slides, the cells that stay keep their
    /// heights and the ones that just arrived are the only ones whose stored
    /// key no longer matches.
    ///
    /// THE CASTER IS INJECTED, so the grid logic is tested with a flat fake
    /// and a "roof" and no physics scene; <see cref="CastFromSky"/> is the
    /// real one.
    /// </summary>
    public sealed class RainCover : IRainCover
    {
        /// <summary>Metres per cell.</summary>
        public const float CellSize = 2.0f;

        /// <summary>Cells along each side of the window (48m).</summary>
        public const int Cells = 24;

        /// <summary>
        /// Rays start this high in the world -- above the tallest possible
        /// building (72m of terrain relief plus a 320m supertall) -- so the
        /// first thing a ray finds is always the outermost roof, even for a
        /// player standing on the 50th floor whose own ceiling is far above
        /// where a ray from just overhead would begin.
        /// </summary>
        public const float SkyHeight = 450.0f;

        /// <summary>A surface this far above a head counts as a roof over it.</summary>
        public const float HeadClearance = 2.2f;

        /// <summary>A cell is re-checked after this long, so a moving car or a shut door is noticed.</summary>
        public const float MaxAgeSeconds = 1.0f;

        /// <summary>Most aged-out cells re-cast in one refresh (cells that just entered the window always are).</summary>
        public const int AgingBudgetPerRefresh = 40;

        /// <summary>The first solid surface below (x, fromY) at (x, z), or NaN if there is none to find.</summary>
        public delegate float SurfaceCaster(float x, float z, float fromY);

        private readonly float[] _height = new float[Cells * Cells];
        private readonly int[] _keyX = new int[Cells * Cells];
        private readonly int[] _keyZ = new int[Cells * Cells];
        private readonly float[] _age = new float[Cells * Cells];
        private readonly bool[] _valid = new bool[Cells * Cells];
        private readonly SurfaceCaster _caster;

        /// <summary>Rays cast by the last refresh -- for tests and profiling.</summary>
        public int LastRefreshRays { get; private set; }

        public RainCover(SurfaceCaster caster)
        {
            _caster = caster;
        }

        private static int Mod(int value, int modulus)
        {
            int r = value % modulus;
            return r < 0 ? r + modulus : r;
        }

        private static int Slot(int cellX, int cellZ)
        {
            return Mod(cellZ, Cells) * Cells + Mod(cellX, Cells);
        }

        private static int CellOf(float world)
        {
            return Mathf.FloorToInt(world / CellSize);
        }

        /// <summary>
        /// Brings the window around <paramref name="centre"/> up to date: every
        /// cell that just entered it is cast now, and up to
        /// <see cref="AgingBudgetPerRefresh"/> cells older than
        /// <see cref="MaxAgeSeconds"/> are re-cast, oldest window order first.
        /// </summary>
        public void Refresh(Vector3 centre, float deltaTime)
        {
            LastRefreshRays = 0;
            float skyY = Mathf.Max(SkyHeight, centre.y + 60.0f);
            int firstX = CellOf(centre.x) - Cells / 2;
            int firstZ = CellOf(centre.z) - Cells / 2;
            int aged = 0;

            for (int dz = 0; dz < Cells; dz++)
            {
                for (int dx = 0; dx < Cells; dx++)
                {
                    int cellX = firstX + dx;
                    int cellZ = firstZ + dz;
                    int slot = Slot(cellX, cellZ);

                    bool entering = !_valid[slot] || _keyX[slot] != cellX || _keyZ[slot] != cellZ;
                    if (!entering)
                    {
                        _age[slot] += deltaTime;
                        if (_age[slot] < MaxAgeSeconds || aged >= AgingBudgetPerRefresh)
                        {
                            continue;
                        }
                        aged++;
                    }

                    float x = (cellX + 0.5f) * CellSize;
                    float z = (cellZ + 0.5f) * CellSize;
                    _height[slot] = _caster(x, z, skyY);
                    _keyX[slot] = cellX;
                    _keyZ[slot] = cellZ;
                    _age[slot] = 0.0f;
                    _valid[slot] = true;
                    LastRefreshRays++;
                }
            }
        }

        /// <summary>The surface height in the cell holding (x, z), or NaN if that cell is outside the window or found nothing.</summary>
        public float SurfaceHeightAt(float x, float z)
        {
            int cellX = CellOf(x);
            int cellZ = CellOf(z);
            int slot = Slot(cellX, cellZ);
            if (!_valid[slot] || _keyX[slot] != cellX || _keyZ[slot] != cellZ)
            {
                return float.NaN;
            }
            return _height[slot];
        }

        private bool TryCovered(int cellX, int cellZ, float pointY, out float covered)
        {
            int slot = Slot(cellX, cellZ);
            covered = 0.0f;
            if (!_valid[slot] || _keyX[slot] != cellX || _keyZ[slot] != cellZ)
            {
                return false;
            }
            float surface = _height[slot];
            covered = !float.IsNaN(surface) && surface > pointY + HeadClearance ? 1.0f : 0.0f;
            return true;
        }

        /// <summary>
        /// Cover over a point, blended bilinearly over the four nearest cells
        /// so walking out from under an overhang fades the shelter out over a
        /// couple of metres instead of flipping at a cell edge. A point
        /// outside the mapped window is answered by one direct cast.
        /// </summary>
        public float Shelter(Vector3 worldPoint)
        {
            float gx = worldPoint.x / CellSize - 0.5f;
            float gz = worldPoint.z / CellSize - 0.5f;
            int x0 = Mathf.FloorToInt(gx);
            int z0 = Mathf.FloorToInt(gz);
            float fx = gx - x0;
            float fz = gz - z0;

            if (TryCovered(x0, z0, worldPoint.y, out float c00) &&
                TryCovered(x0 + 1, z0, worldPoint.y, out float c10) &&
                TryCovered(x0, z0 + 1, worldPoint.y, out float c01) &&
                TryCovered(x0 + 1, z0 + 1, worldPoint.y, out float c11))
            {
                float a = Mathf.Lerp(c00, c10, fx);
                float b = Mathf.Lerp(c01, c11, fx);
                return Mathf.Lerp(a, b, fz);
            }

            float surface = _caster(worldPoint.x, worldPoint.z, Mathf.Max(SkyHeight, worldPoint.y + 60.0f));
            return !float.IsNaN(surface) && surface > worldPoint.y + HeadClearance ? 1.0f : 0.0f;
        }

        /// <summary>Forgets everything, so the next refresh casts the whole window -- after a teleport.</summary>
        public void Invalidate()
        {
            for (int i = 0; i < _valid.Length; i++)
            {
                _valid[i] = false;
            }
        }

        /// <summary>
        /// The real caster: a ray straight down from the sky through the
        /// world's colliders. A character standing in the column (the player,
        /// a pedestrian, a ragdoll) is skipped and the ray carries on -- rain
        /// that fell on somebody's head still ends at the ground for the
        /// purposes of "is this a roof". Falls back to the terrain's own
        /// height where no collider has streamed in yet, so a chunk that is
        /// still loading is treated as open ground rather than as a hole.
        /// </summary>
        public static float CastFromSky(float x, float z, float fromY)
        {
            var origin = new Vector3(x, fromY, z);
            for (int step = 0; step < 6; step++)
            {
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, origin.y + 40.0f,
                        ~0, QueryTriggerInteraction.Ignore))
                {
                    break;
                }
                if (hit.collider.GetComponentInParent<IDamageable>() == null)
                {
                    return hit.point.y;
                }
                origin = new Vector3(x, hit.point.y - 0.05f, z);
            }

            TerrainField terrain = CurrentWorld.Instance?.Terrain;
            return terrain != null ? (float)terrain.ContinuousHeightAt(x, z) : float.NaN;
        }
    }
}
