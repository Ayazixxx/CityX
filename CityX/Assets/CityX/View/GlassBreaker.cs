using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Turns a blow on a chunk into a broken window.
    ///
    /// SITS ON THE STREAMER, NOT ON A CHUNK. Chunks are thrown away constantly
    /// and a component on one would be reborn with no idea what had happened to
    /// it -- and a punch has to reach the damage record, which is the
    /// streamer's, not any chunk's. Whoever punched a chunk collider finds this
    /// by walking up from the collider they hit, which is the same
    /// GetComponentInParent that finds a body from a limb.
    ///
    /// Ported from city_chunk.gd's building_glass.gd, at the level that
    /// survives the move: per-WINDOW state, one hit to crack it open and make
    /// it walkable, a second to shatter it away. Godot needed a node per pane
    /// to do that; here the panes are triangles in a chunk mesh, so the state
    /// is a dictionary and the mesh is rebuilt.
    /// </summary>
    [RequireComponent(typeof(CityStreamer))]
    public sealed class GlassBreaker : MonoBehaviour, IBreakableSurface
    {
        [Tooltip("The dull knock of a window cracking. One picked at random per hit.")]
        public AudioClip[] CrackSounds;

        [Tooltip("The full break, when a cracked window finally goes.")]
        public AudioClip[] BreakSounds;

        [Tooltip("Shards. Same self-freeing burst the blood splatter uses.")]
        public GameObject ShardPrefab;

        [Tooltip("How far a sound carries. A window across the street should be audible.")]
        public float HearingRange = 30.0f;

        private CityStreamer _streamer;
        private CityWorld _world;

        private void Awake()
        {
            _streamer = GetComponent<CityStreamer>();
            _world = new CityWorld(_streamer.WorldSeed);
        }

        /// <summary>
        /// A blow at a world point. Finds the building it landed on, the window
        /// within it, and moves that window one step along.
        ///
        /// FALSE FOR A PUNCH ON PLAIN CONCRETE, which is most of a wall. The
        /// caller uses that to decide whether the swing connected, so a fist
        /// against blank wall stays as silent as a fist against air.
        /// </summary>
        public bool TakePunch(Vector3 point) => TakeBlow(point, Vector3.zero, BlowStrength.Light);

        /// <summary>
        /// A blow of a known kind. A LIGHT one (a fist, a bullet) moves the pane one step -- cracked, then
        /// gone -- and a HEAVY one (a blade, a thrown bottle, a body at speed) takes it out at once.
        /// <paramref name="direction"/> is the way the blow was travelling (zero when unknown): the
        /// shards fly that way, through the pane, instead of always outward at whoever swung.
        /// </summary>
        public bool TakeBlow(Vector3 point, Vector3 direction, BlowStrength strength)
        {
            if (_world == null)
            {
                _world = new CityWorld(_streamer.WorldSeed);
            }

            int chunkX = Mathf.FloorToInt(point.x / CityLayout.ChunkSize);
            int chunkY = Mathf.FloorToInt(point.z / CityLayout.ChunkSize);

            // The point can sit a few centimetres outside the building it hit,
            // and a wall on a chunk boundary belongs to the neighbour, so the
            // nine chunks around it are all candidates. Anchors are checked
            // rather than every chunk, because a multi-chunk building is only
            // ever held by one of them.
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    BuildingSite site = _world.BuildingAt(chunkX + dx, chunkY + dy);
                    if (!site.Exists)
                    {
                        continue;
                    }
                    if (!GlassDamage.TryPaneAt(_world, site, point, out PaneKey key))
                    {
                        continue;
                    }

                    PaneState was = _streamer.Damage.StateOf(key);
                    Vector2 impactUV = GlassDamage.FractionWithinPane(site, key, point);
                    PaneState now = _streamer.Damage.Hit(key, impactUV, shatterOutright: strength == BlowStrength.Heavy);
                    if (now == was)
                    {
                        return false;   // already gone
                    }

                    Report(now, point, key.Wall, direction, strength);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The noise and the mess. A crack knocks; a shatter breaks and throws
        /// shards -- city_chunk.gd's own two sounds, now with a real physics
        /// shard burst (GlassShardBurst) rather than a fading sprite, which is
        /// the only part of a broken window you actually see happen.
        /// </summary>
        private void Report(PaneState state, Vector3 point, WindowGrid.Wall wall, Vector3 blowDirection, BlowStrength strength)
        {
            // GLASS SOUNDS LIKE A SEQUENCE, NOT A SAMPLE: the contact and crack or shatter first, then the
            // tinkle of the pieces landing a moment later (sound-design practice for breaking glass). A
            // crack is only the first; the break gets the tinkle as a second, higher, quieter layer.
            AudioClip[] bank = state == PaneState.Cracked ? CrackSounds : BreakSounds;
            AudioClip clip = PickClip(bank);
            if (clip != null)
            {
                // A little pitch variation so a run of windows does not repeat one sample; a heavy blow
                // is a fuller, lower sound.
                float pitch = Random.Range(0.92f, 1.08f) * (strength == BlowStrength.Heavy ? 0.94f : 1.0f);
                PositionalSound.Play(clip, point, 1.0f, pitch, 0.0f, HearingRange);
                if (state == PaneState.Shattered)
                {
                    PositionalSound.Play(clip, point, 0.35f, Random.Range(1.5f, 1.9f), Random.Range(0.22f, 0.45f), HearingRange);
                }
            }

            if (state == PaneState.Shattered && ShardPrefab != null)
            {
                GlassShardBurst.Spawn(ShardPrefab, point, ShardDirection(WindowGrid.OutwardNormal(wall), blowDirection));
            }
        }

        private static AudioClip PickClip(AudioClip[] bank)
        {
            return bank != null && bank.Length > 0 ? bank[Random.Range(0, bank.Length)] : null;
        }

        /// <summary>
        /// The way the shards fly: through the pane, the way the blow was going. Someone outside hitting a
        /// window sends the glass INTO the building; someone inside sends it out over the street. With no
        /// known direction it is the wall's outward normal, as it always was.
        /// </summary>
        public static Vector3 ShardDirection(Vector3 outward, Vector3 blowDirection)
        {
            if (blowDirection.sqrMagnitude < 1e-6f)
            {
                return outward;
            }
            return Vector3.Dot(blowDirection, outward) < 0.0f ? -outward : outward;
        }
    }
}
