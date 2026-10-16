using System.Collections.Generic;
using CityX.Character;
using CityX.World;
using CityX.World.Plan;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.View
{
    /// <summary>
    /// The doors onto the balconies (World/BalconyDoors), opened: walking into a shut one opens it, and E (or the gamepad's left face button) at one
    /// opens or shuts it. One of these in the scene (on the CityStreamer, like LightSwitchInteractor), not a component on every door: the doors are
    /// drawn in the chunks' own meshes (CityMeshBuilder.BalconyDoors), and the few near the player are found from the plan a few times a second.
    /// A door is opened through BalconyDoorState, so every machine in a session hears of it (Character.BalconyDoorsNetwork), and its chunk is built
    /// again with the door drawn open (CityStreamer). A door that does not open (BalconyDoor.Opens: half way up a room, or the stairs right behind
    /// it) rattles.
    ///
    /// IT OPENS BEFORE YOU REACH IT. Drawing a door the other way is a chunk rebuilt in the background, a moment's work, so walking toward a door
    /// opens it from further off than a street door (BuildingDoor.BumpReachMetres) and you walk through rather than into it.
    /// </summary>
    public sealed class BalconyDoorInteractor : MonoBehaviour
    {
        /// <summary>How far from the doorway's middle (horizontally) the player may be to open or shut it with E (a street door's reach).</summary>
        public const float ReachMetres = BuildingDoor.InteractRange;

        /// <summary>How far above or below the door's floor the player's feet may be (a different floor is not in reach).</summary>
        public const float ReachUpDown = 1.2f;

        /// <summary>The speed toward a shut door, m/s, at which walking into it opens it (a street door's).</summary>
        public const float BumpMinSpeed = BuildingDoor.BumpMinSpeed;

        /// <summary>How close to the door's plane, metres, a body walking toward it opens it.</summary>
        public const float BumpReachMetres = 1.6f;

        /// <summary>How far in from either jamb a body has to be heading for the doorway for walking into it to count, metres.</summary>
        public const float BumpInset = 0.15f;

        /// <summary>A door within this distance (horizontally, of the doorway's middle) is looked at every frame.</summary>
        public const float NearMetres = 6.0f;

        private const float SearchSeconds = 0.25f;
        private const float RattleSeconds = 0.8f;

        /// <summary>A door near the player: the building, the door, and where it stands (BalconyDoors.InWorld).</summary>
        public readonly struct Near
        {
            public readonly BuildingSite Site;
            public readonly BalconyDoor Door;
            public readonly Vector3 Corner;
            public readonly Vector3 Along;
            public readonly Vector3 Outward;

            public Near(BuildingSite site, in BalconyDoor door)
            {
                Site = site;
                Door = door;
                BalconyDoors.InWorld(site, door, out Corner, out Along, out Outward);
            }

            /// <summary>The middle of the doorway at a hand's height: what E is aimed at.</summary>
            public Vector3 Handle => Corner + Along * (Door.Width * 0.5f) + Vector3.up * Mathf.Min(1.2f, Door.Height * 0.5f);

            /// <summary>A point in the door's own frame: x along the wall from its low jamb, y above its floor, z out over the balcony.</summary>
            public Vector3 Local(Vector3 point)
            {
                Vector3 d = point - Corner;
                return new Vector3(Vector3.Dot(d, Along), d.y, Vector3.Dot(d, Outward));
            }

            public bool IsOpen => BalconyDoorState.Shared.IsOpen(Site.Id, Door.Unit, Door.Row);
        }

        private readonly List<Near> _near = new List<Near>();
        private readonly Dictionary<int, List<BalconyDoor>> _doorsOf = new Dictionary<int, List<BalconyDoor>>();
        private CityWorld _doorsWorld;
        private PlayerInputReader _player;
        private CharacterController _body;
        private float _sinceLookup = 99.0f;
        private float _sinceSearch = 99.0f;
        private Vector3 _searchedAt;
        private float _lastRattle = -99.0f;
        private static AudioClip _slide;
        private static AudioClip _rattle;

        /// <summary>The doors near the player at the last look (for tests and the HUD).</summary>
        public IReadOnlyList<Near> NearDoors => _near;

        /// <summary>The last door this machine's player opened or shut, and whether it was one that does not open.</summary>
        public long LastUsed { get; private set; } = -1;

        public bool LastWasLocked { get; private set; }

        private void Update()
        {
            CityWorld world = CurrentWorld.Instance;
            Transform player = LocalPlayer();
            if (world == null || player == null)
            {
                _near.Clear();
                return;
            }
            Vector3 feet = player.position;
            _sinceSearch += Time.unscaledDeltaTime;
            if (_sinceSearch >= SearchSeconds || (feet - _searchedAt).sqrMagnitude > 1.0f)
            {
                _sinceSearch = 0.0f;
                _searchedAt = feet;
                FindNear(world, feet, _near);
            }
            if (_near.Count == 0)
            {
                return;
            }

            // Walking into a shut door opens it (a locked one rattles).
            if (_body != null && _body.enabled)
            {
                int bumped = Bumped(_near, feet, _body.velocity);
                if (bumped >= 0)
                {
                    Use(_near[bumped], feet, true);
                }
            }

            bool pressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame);
            if (!pressed)
            {
                return;
            }
            Camera camera = Camera.main;
            Vector3 look = camera != null ? camera.transform.forward : player.forward;
            Vector3 eye = camera != null ? camera.transform.position : feet + Vector3.up * 2.2f;
            int picked = Pick(_near, feet, eye, look);
            if (picked >= 0)
            {
                Near door = _near[picked];
                // Not straight away: a light switch or a street door may be what the player is looking at (InteractionFocus picks one).
                InteractionFocus.Offer(InteractionFocus.DegreesOff(door.Handle, eye, look), () => Use(door, feet, !door.IsOpen));
            }
        }

        /// <summary>
        /// What E does at the door the player is looking at, from <paramref name="feet"/> looking along <paramref name="look"/>: opens it or shuts
        /// it. Public so a test can drive it without a keyboard. False when no door is in reach.
        /// </summary>
        public bool TryUse(Vector3 feet, Vector3 eye, Vector3 look)
        {
            CityWorld world = CurrentWorld.Instance;
            if (world == null)
            {
                return false;
            }
            FindNear(world, feet, _near);
            int picked = Pick(_near, feet, eye, look);
            if (picked < 0)
            {
                return false;
            }
            Use(_near[picked], feet, !_near[picked].IsOpen);
            return true;
        }

        /// <summary>Opens or shuts a door for the player standing at <paramref name="feet"/>: a locked one rattles, and one is not shut on them.</summary>
        private void Use(in Near door, Vector3 feet, bool open)
        {
            LastUsed = BalconyDoorState.Key(door.Site.Id, door.Door.Unit, door.Door.Row);
            LastWasLocked = !door.Door.Opens;
            if (!door.Door.Opens)
            {
                if (Time.unscaledTime - _lastRattle >= RattleSeconds)
                {
                    _lastRattle = Time.unscaledTime;
                    PositionalSound.Play(Rattle, door.Handle, 0.6f, 1.0f, 0.0f, 15.0f);
                }
                return;
            }
            if (!open && InTheDoorway(door, feet))
            {
                return;   // shut on the player standing in it, they would be inside the door
            }
            BalconyDoorState.Shared.Use(door.Site.Id, door.Door.Unit, door.Door.Row, open);
            if (door.Door.Kind == BalconyDoorKind.Sliding)
            {
                PositionalSound.Play(Slide, door.Handle, 0.55f, open ? 1.0f : 0.92f, 0.0f, 20.0f);
            }
            else
            {
                PositionalSound.Play(open ? DoorSounds.Open : DoorSounds.Shut, door.Handle, open ? 0.6f : 0.45f, 1.0f, 0.0f, 25.0f);
            }
        }

        /// <summary>Is a body at <paramref name="feet"/> standing in the doorway, where a shut door would be?</summary>
        public static bool InTheDoorway(in Near door, Vector3 feet)
        {
            Vector3 p = door.Local(feet);
            return p.x > -0.3f && p.x < door.Door.Width + 0.3f && p.z > -BuildingInterior.WallThickness - 0.35f && p.z < 0.35f
                && p.y > -1.0f && p.y < door.Door.Height;
        }

        /// <summary>
        /// The doors of the buildings round <paramref name="feet"/> within <see cref="NearMetres"/> of it and on its floor, into
        /// <paramref name="into"/> (cleared first). Only a building whose back wall is near is asked for its doors, and each building's are kept.
        /// </summary>
        public void FindNear(CityWorld world, Vector3 feet, List<Near> into)
        {
            into.Clear();
            if (_doorsWorld != world || _doorsOf.Count > 256)
            {
                _doorsOf.Clear();
                _doorsWorld = world;
            }
            int cx = PlanWorld.ChunkOf(feet.x), cy = PlanWorld.ChunkOf(feet.z);
            for (int dy = -3; dy <= 3; dy++)
            {
                for (int dx = -3; dx <= 3; dx++)
                {
                    List<int> owned = world.BuildingsOwnedBy(cx + dx, cy + dy);
                    for (int i = 0; i < owned.Count; i++)
                    {
                        BuildingSite b = world.Building(owned[i]);
                        if (!b.Exists || b.IsOpenGround || feet.y < b.BaseHeight - 1.0f || feet.y > b.Top + 1.0f)
                        {
                            continue;
                        }
                        b.WorldToLocal(feet.x, feet.z, out double lx, out double lz);
                        double back = -b.Params.Depth * 0.5;
                        if (System.Math.Abs(lx) > b.Params.Width * 0.5 + 1.0 || lz < back - ApartmentFront.MaxDepth - NearMetres * 0.5
                            || lz > back + NearMetres)
                        {
                            continue;
                        }
                        List<BalconyDoor> doors = DoorsOf(b);
                        for (int k = 0; k < doors.Count; k++)
                        {
                            BalconyDoor door = doors[k];
                            float floorY = b.BaseHeight + door.Y0;
                            if (Mathf.Abs(feet.y - floorY) > ReachUpDown + 1.0f)
                            {
                                continue;
                            }
                            var near = new Near(b, door);
                            Vector3 middle = near.Corner + near.Along * (door.Width * 0.5f);
                            if (new Vector2(middle.x - feet.x, middle.z - feet.z).sqrMagnitude <= NearMetres * NearMetres)
                            {
                                into.Add(near);
                            }
                        }
                    }
                }
            }
        }

        private List<BalconyDoor> DoorsOf(BuildingSite site)
        {
            if (!_doorsOf.TryGetValue(site.Id, out List<BalconyDoor> doors))
            {
                doors = new List<BalconyDoor>();
                WindowPlan plan = WindowPlan.For(site);
                BalconyDoors.For(site, plan, doors);
                // And the footbridges' doorways (Footbridges), on whichever wall a bridge meets.
                CityWorld world = CurrentWorld.Instance;
                if (world != null)
                {
                    Footbridges.DoorsOf(world, site, plan, doors);
                }
                _doorsOf[site.Id] = doors;
            }
            return doors;
        }

        /// <summary>
        /// The door E would act on, for a player at <paramref name="feet"/> looking along <paramref name="look"/> from <paramref name="eye"/>: of
        /// the doors in reach, on the player's floor and in front of where they are looking, the one nearest the crosshair. -1 when there is none.
        /// </summary>
        public static int Pick(List<Near> doors, Vector3 feet, Vector3 eye, Vector3 look)
        {
            int best = -1;
            float bestDegrees = float.MaxValue;
            for (int i = 0; i < doors.Count; i++)
            {
                Near d = doors[i];
                if (Mathf.Abs(feet.y - d.Corner.y) > ReachUpDown)
                {
                    continue;
                }
                Vector3 at = d.Handle;
                float flat = new Vector2(at.x - feet.x, at.z - feet.z).magnitude;
                if (flat > ReachMetres || !BuildingDoor.IsLookingToward(eye, look, at))
                {
                    continue;
                }
                Vector3 to = at - eye;
                float degrees = to.sqrMagnitude < 1e-6f ? 0.0f : Vector3.Angle(look, to);
                if (degrees < bestDegrees)
                {
                    bestDegrees = degrees;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>
        /// The shut door a body at <paramref name="feet"/> moving at <paramref name="velocity"/> is walking into, if any: one whose plane it is
        /// close to (<see cref="BumpReachMetres"/>), heading for the doorway between its jambs at <see cref="BumpMinSpeed"/> or more. -1 when none.
        /// </summary>
        public static int Bumped(List<Near> doors, Vector3 feet, Vector3 velocity)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                Near d = doors[i];
                Vector3 p = d.Local(feet);
                if (p.y < -0.4f || p.y > 0.8f || Mathf.Abs(p.z) > BumpReachMetres || d.IsOpen)
                {
                    continue;
                }
                float side = p.z >= 0.0f ? 1.0f : -1.0f;
                float toward = -side * Vector3.Dot(velocity, d.Outward);
                if (toward < BumpMinSpeed)
                {
                    continue;
                }
                // Where along the wall the body will be when it gets there.
                float x = p.x + Vector3.Dot(velocity, d.Along) * (Mathf.Abs(p.z) / toward);
                if (x < BumpInset || x > d.Door.Width - BumpInset)
                {
                    continue;
                }
                return i;
            }
            return -1;
        }

        /// <summary>The body to open doors for, when set; otherwise the local player's (the one whose input is on). A test sets a stand-in.</summary>
        [System.NonSerialized] public CharacterController PlayerBody;

        private Transform LocalPlayer()
        {
            if (PlayerBody != null)
            {
                _body = PlayerBody;
                return PlayerBody.transform;
            }
            _sinceLookup += Time.unscaledDeltaTime;
            if ((_player == null || !_player.isActiveAndEnabled) && _sinceLookup > 0.5f)
            {
                _sinceLookup = 0.0f;
                _player = null;
                _body = null;
                foreach (PlayerInputReader reader in FindObjectsByType<PlayerInputReader>(FindObjectsSortMode.None))
                {
                    // A copy of another player's body has its input switched off (NetworkedPlayerIdentity): the one with input is ours.
                    if (reader.isActiveAndEnabled)
                    {
                        _player = reader;
                        _body = reader.GetComponent<CharacterController>();
                        break;
                    }
                }
            }
            return _player != null ? _player.transform : null;
        }

        /// <summary>A sliding door running on its track: a low rumble that swells and fades, and a soft knock as it stops. Made here (no sound asset).</summary>
        private static AudioClip Slide
        {
            get
            {
                if (_slide == null)
                {
                    const int rate = 22050;
                    var samples = new float[(int)(rate * 0.55f)];
                    var random = new System.Random(11);
                    float low = 0.0f, lower = 0.0f;
                    for (int i = 0; i < samples.Length; i++)
                    {
                        float t = i / (float)rate;
                        float noise = (float)random.NextDouble() * 2.0f - 1.0f;
                        low += (noise - low) * 0.08f;       // rolling wheels: filtered noise
                        lower += (low - lower) * 0.2f;
                        float swell = Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
                        float knock = t > 0.45f ? Mathf.Exp(-(t - 0.45f) * 60.0f) * Mathf.Sin((t - 0.45f) * 2.0f * Mathf.PI * 180.0f) : 0.0f;
                        samples[i] = lower * swell * 2.2f + knock * 0.5f;
                    }
                    _slide = AudioClip.Create("BalconyDoorSlide", samples.Length, 1, rate, false);
                    _slide.SetData(samples, 0);
                }
                return _slide;
            }
        }

        /// <summary>A locked door tried: the handle jiggled three times against its latch.</summary>
        private static AudioClip Rattle
        {
            get
            {
                if (_rattle == null)
                {
                    const int rate = 22050;
                    var samples = new float[(int)(rate * 0.32f)];
                    var random = new System.Random(5);
                    for (int i = 0; i < samples.Length; i++)
                    {
                        float t = i / (float)rate;
                        float s = 0.0f;
                        for (int k = 0; k < 3; k++)
                        {
                            float at = t - k * 0.09f;
                            if (at >= 0.0f)
                            {
                                float envelope = Mathf.Exp(-at * 70.0f);
                                s += ((float)random.NextDouble() * 2.0f - 1.0f) * envelope * 0.5f
                                    + Mathf.Sin(at * 2.0f * Mathf.PI * (1500.0f + 200.0f * k)) * envelope * 0.3f;
                            }
                        }
                        samples[i] = s;
                    }
                    _rattle = AudioClip.Create("BalconyDoorRattle", samples.Length, 1, rate, false);
                    _rattle.SetData(samples, 0);
                }
                return _rattle;
            }
        }
    }
}
