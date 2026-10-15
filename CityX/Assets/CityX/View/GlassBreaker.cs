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

        /// <summary>How many glass pieces are made while the city loads, so the first window to break makes none on its frame.</summary>
        public const int PrewarmedPieces = 64;

        private CityStreamer _streamer;
        private CityWorld _world;
        private Material _shardMaterial;

        private void Awake()
        {
            _streamer = GetComponent<CityStreamer>();
            _world = new CityWorld(_streamer.WorldSeed);
        }

        /// <summary>
        /// THE FIRST WINDOW TO BREAK cost a frame ~10 ms of loading (measured 2026-10-14, WindowSprayFramePlayTests): its sounds were read
        /// off disk on first play, and the shard burst's twelve convex colliders were cooked the first time any shard existed. Both are done
        /// here instead, while the city is still streaming in; the cooked shapes stay with the shard meshes for every burst after.
        /// </summary>
        private void Start()
        {
            foreach (AudioClip[] bank in new[] { CrackSounds, BreakSounds })
            {
                if (bank == null)
                {
                    continue;
                }
                foreach (AudioClip clip in bank)
                {
                    if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                    {
                        clip.LoadAudioData();
                    }
                }
            }
            if (ShardPrefab != null)
            {
                foreach (MeshCollider shard in ShardPrefab.GetComponentsInChildren<MeshCollider>(true))
                {
                    if (shard.sharedMesh != null)
                    {
                        Physics.BakeMesh(shard.sharedMesh.GetInstanceID(), shard.convex, shard.cookingOptions);
                    }
                }
                // The broken pane's own pieces (GlassFractureBurst) are drawn with the shards' glass, and a first batch is made now.
                MeshRenderer look = ShardPrefab.GetComponentInChildren<MeshRenderer>(true);
                _shardMaterial = look != null ? look.sharedMaterial : null;
                if (_shardMaterial != null)
                {
                    GlassFractureBurst.Prewarm(PrewarmedPieces, _shardMaterial);
                }
            }
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

            // The building the point is on or beside. A point can sit a few centimetres outside the wall it hit (a
            // contact skin, a bullet's impact), so the building is looked up with a little margin. Height matters: a tower stands on
            // its podium's roof, so two buildings can be over the same ground.
            if (!_world.TryBuildingAt(point, out BuildingSite site, 0.6) ||
                !GlassDamage.TryPaneAt(_world, site, point, out PaneKey key))
            {
                return false;
            }

            Vector3 outward = CityMeshBuilder.WallNormal(site, key.Wall);
            PaneState was = _streamer.Damage.StateOf(key);
            Vector2 impactUV = GlassDamage.FractionWithinPane(_world, site, key, point);
            if (strength == BlowStrength.Bullet)
            {
                // A bullet makes its own hole and the window stays up -- until the fifth (GlassDamage.ShatteringBullet) brings it down. Otherwise only
                // something knocking into it brings it down (see CrackedPane.IsAKnock).
                PaneState after = _streamer.Damage.Shoot(key, impactUV, out bool madeHole);
                if (!madeHole)
                {
                    return false;   // already gone
                }
                Report(after, site, key, point, outward, direction, strength);
                return true;
            }

            PaneState now = _streamer.Damage.Hit(key, impactUV, shatterOutright: strength == BlowStrength.Heavy);
            if (now == was)
            {
                return false;   // already gone
            }

            Report(now, site, key, point, outward, direction, strength);
            return true;
        }

        /// <summary>
        /// The noise and the mess. A crack knocks; a shatter breaks and throws
        /// shards -- city_chunk.gd's own two sounds, now with a real physics
        /// shard burst (GlassShardBurst) rather than a fading sprite, which is
        /// the only part of a broken window you actually see happen.
        /// </summary>
        private void Report(PaneState state, BuildingSite site, PaneKey key, Vector3 point, Vector3 outward, Vector3 blowDirection,
            BlowStrength strength)
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

            if (state != PaneState.Shattered)
            {
                return;
            }
            // THE PANE BREAKS INTO ITS OWN PIECES (GlassFractureBurst): cut where it stood round the blow, falling the way the blow went. A round
            // building's facet keeps the old handful of shards.
            Vector3 through = ShardDirection(outward, blowDirection);
            if (_shardMaterial != null && GlassPaneFrame.TryFrame(_world, site, key, point, out PaneFrame frame))
            {
                int seed = unchecked(key.GetHashCode() * 31 + Time.frameCount);
                GlassFractureBurst.Shatter(_shardMaterial, frame, point, through, strength, seed);
            }
            else if (ShardPrefab != null)
            {
                GlassShardBurst.Spawn(ShardPrefab, point, through);
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
