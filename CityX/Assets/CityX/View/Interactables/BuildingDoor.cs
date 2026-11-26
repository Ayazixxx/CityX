using CityX.Character;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The door in a building's entrance. Press E while near it and looking at it to swing it open, again
    /// to close it -- or just walk (or run) into it.
    ///
    /// Ported from city/building_door.gd, then reworked against what door design in third-person games
    /// converges on (the Level Design Book's door chapter, Liz England's "The Door Problem", and how
    /// The Last of Us Part II lets your speed decide how you go through one):
    ///
    /// IT SWINGS AWAY FROM WHOEVER OPENS IT, and works from both sides. It used to swing inward always,
    /// which from outside is away from you but from inside is straight into you (and the kinematic leaf
    /// then shoved you). Now the side you stand on decides: from the street it opens inward, from the
    /// lobby it opens outward. "Double-hinged, opening away from the character" is the standing advice
    /// for a game door, and err on the side of the player's convenience over realism.
    ///
    /// IT NEVER PUSHES ANYONE. A swinging leaf sweeps a lot of room and is easily blocked; instead of
    /// shoving whoever is in the way (or closing on them), it STOPS where it is until they move -- the
    /// "Blocked" state a door needs alongside Opening and Closing.
    ///
    /// WALKING INTO IT OPENS IT, so a door never stops you dead while you are running from something.
    /// How fast you were going decides how it opens: eased at a walk, thrown open at a run.
    ///
    /// E ONLY WORKS ON THE DOOR YOU ARE LOOKING AT, not any door within reach behind you or through a wall.
    ///
    /// ITS MOTION IS A DAMPED SPRING rather than a fixed-time ease: it starts quickly, settles gently, and
    /// pressing E again mid-swing turns it round smoothly instead of waiting for it to finish.
    ///
    /// AND IT SOUNDS LIKE ONE: a latch and a creak as it starts to open, a thud as it meets its frame
    /// (DoorSounds -- made in code, so they add nothing to the game's size).
    ///
    /// AND IT ASKS FOR THE KEY ITSELF rather than being told by the player. That is Godot's arrangement, and
    /// here it also happens to be the only arrangement that does not make the character code depend on the
    /// city code to open a door.
    /// </summary>
    public sealed class BuildingDoor : MonoBehaviour, IOpenableDoor
    {
        /// <summary>Part of IOpenableDoor -- OldGuy's own way to find this door without CityX.Character depending on CityX.View.</summary>
        Vector3 IOpenableDoor.Position => transform.position;

        public const float Thickness = 0.08f;

        /// <summary>
        /// A hair past ninety, so it reads as pushed open rather than as
        /// precisely perpendicular.
        /// </summary>
        public const float OpenAngleDegrees = 100.0f;

        /// <summary>How long a door takes to open at a walk, roughly: the spring's settling time, not a fixed timer.</summary>
        public const float OpenSeconds = 0.5f;

        public const float InteractRange = 2.5f;

        /// <summary>The spring's time constant, in seconds, for a normal push open. Settles in about four times this.</summary>
        public const float OpenSmoothSeconds = 0.14f;

        /// <summary>The same for a door thrown open by someone at a run.</summary>
        public const float BurstSmoothSeconds = 0.07f;

        /// <summary>The same when it swings shut.</summary>
        public const float CloseSmoothSeconds = 0.17f;

        /// <summary>How much of a door's swing E needs the player to be looking toward: within this many degrees of the door.</summary>
        public const float LookAtDegrees = 65.0f;

        /// <summary>The speed, in m/s, toward a closed door at which walking into it opens it.</summary>
        public const float BumpMinSpeed = 1.2f;

        /// <summary>Toward-door speed at which it is thrown open (the burst) rather than eased.</summary>
        public const float BurstSpeed = 5.5f;

        /// <summary>How close to the door's plane, in metres, a body has to be for walking into it to count.</summary>
        public const float BumpReachMetres = 0.85f;

        /// <summary>Where a door panel goes, given the building it belongs to.</summary>
        public readonly struct Panel
        {
            /// <summary>The hinge: the opening's left edge, on the wall.</summary>
            public readonly Vector3 Hinge;

            public readonly float Width;
            public readonly float Height;

            /// <summary>Which way the door faces when shut -- the wall's outward normal.</summary>
            public readonly Vector3 Facing;

            public Panel(Vector3 hinge, float width, float height, Vector3 facing)
            {
                Hinge = hinge;
                Width = width;
                Height = height;
                Facing = facing;
            }
        }

        /// <summary>
        /// The panel that fills a building's entrance, in world space.
        ///
        /// PURE, AND SEPARATE FROM THE BEHAVIOUR, so a test can check that a
        /// door actually fills the hole the wall builder cut without putting a
        /// swinging object in a scene. The two numbers have to agree exactly:
        /// a door narrower than its opening leaves a gap you can walk through,
        /// and a door wider than it clips into the wall as it swings.
        /// </summary>
        public static Panel PanelFor(BuildingSite site)
        {
            float width = BuildingInterior.DoorWidth;
            // The door is in the middle of the front wall (local +Z), hinged at its left edge as seen from the street.
            site.LocalToWorld(-width * 0.5, site.FrontHalfDepth - BuildingInterior.DoorSetBack, out double hingeX, out double hingeZ);
            return new Panel(
                new Vector3((float)hingeX, site.BaseHeight, (float)hingeZ),
                width,
                BuildingInterior.DoorHeight(site.Params.Height),
                new Vector3((float)-site.AxisZ, 0.0f, (float)site.AxisX));
        }

        /// <summary>
        /// The leaves that fill a building's entrance, in world space: one door (<see cref="PanelFor"/>), or for a pair
        /// (BuildingInterior.HasDoubleEntrance, 2026-10-15) a left leaf hinged at the doorway's left edge and a right one hinged at its right edge.
        /// The right leaf FACES INTO THE BUILDING, so that it hangs back toward the middle from its hinge (a leaf hangs to the +X side of its
        /// hinge, and +X is to the right of where it faces); its hinge is a door's thickness further in, so both leaves stand in the same
        /// plane. Each still swings away from whoever opens it (<see cref="SwingSignFor"/> works from its own facing).
        /// </summary>
        public static Panel[] PanelsFor(BuildingSite site)
        {
            if (!BuildingInterior.HasDoubleEntrance(site))
            {
                return new[] { PanelFor(site) };
            }
            float leaf = BuildingInterior.DoubleDoorWidth * 0.5f;
            float height = BuildingInterior.DoorHeight(site.Params.Height);
            var outward = new Vector3((float)-site.AxisZ, 0.0f, (float)site.AxisX);
            double plane = site.FrontHalfDepth - BuildingInterior.DoorSetBack;
            site.LocalToWorld(-leaf, plane, out double leftX, out double leftZ);
            site.LocalToWorld(leaf, plane - Thickness, out double rightX, out double rightZ);
            return new[]
            {
                new Panel(new Vector3((float)leftX, site.BaseHeight, (float)leftZ), leaf, height, outward),
                new Panel(new Vector3((float)rightX, site.BaseHeight, (float)rightZ), leaf, height, -outward),
            };
        }

        /// <summary>The other leaf of a pair of doors: opening or shutting one opens or shuts both, each away from whoever did it.</summary>
        public BuildingDoor Partner { get; set; }

        /// <summary>
        /// Which way a door opens for someone standing where <paramref name="opener"/> is: +1 swings it
        /// INWARD (from the street side, away from you) and -1 OUTWARD (from inside, away from you).
        /// Pure, so it can be tested without a scene. On the wall plane exactly it opens inward, as it always did.
        /// </summary>
        public static int SwingSignFor(Vector3 hinge, Vector3 facing, Vector3 opener)
        {
            return Vector3.Dot(opener - hinge, facing) >= 0.0f ? 1 : -1;
        }

        /// <summary>Raised when the door settles open or shut, so the streamer can remember.</summary>
        public event System.Action<bool> Toggled;

        private Transform _leaf;
        private float _shutYaw;
        private Vector3 _hinge;
        private Vector3 _facing = Vector3.forward;
        private float _width;
        private float _height;

        private bool _open;
        private int _swingSign = 1;        // +1 opens inward, -1 outward
        private float _angle;              // degrees from shut, signed the same way
        private float _angleVelocity;
        private bool _burst;               // the current opening was a run-through
        private bool _shutSoundPending;    // a closing that has not yet made its thud
        private bool _blocked;

        // The local player, if they are within reach: who E and walking into the door belong to.
        private Transform _player;
        private CharacterController _playerBody;

        private static readonly Collider[] Overlaps = new Collider[16];

        /// <summary>
        /// Builds the leaf and hangs it. <paramref name="startOpen"/> restores
        /// a door the player already opened -- see the streamer's own note on
        /// why that has to be remembered somewhere other than here.
        /// </summary>
        public void Hang(in Panel panel, Material leafMaterial, Material handleMaterial,
            bool startOpen, Vector3? openFrom = null)
        {
            transform.position = panel.Hinge;
            _hinge = panel.Hinge;
            _facing = panel.Facing;
            _width = panel.Width;
            _height = panel.Height;
            _shutYaw = Quaternion.LookRotation(panel.Facing, Vector3.up).eulerAngles.y;

            var leaf = new GameObject("Leaf");
            _leaf = leaf.transform;
            _leaf.SetParent(transform, false);

            // Built offset from the hinge rather than centred on the object, so
            // rotating the object IS the swing and nothing has to work out an
            // arc. The leaf hangs to the +X side of its hinge.
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(_leaf, false);
            slab.transform.localPosition =
                new Vector3(panel.Width * 0.5f, panel.Height * 0.5f, -Thickness * 0.5f);
            slab.transform.localScale = new Vector3(panel.Width, panel.Height, Thickness);
            slab.GetComponent<MeshRenderer>().sharedMaterial = leafMaterial;

            // The handle, on the swinging edge -- the far side from the hinge,
            // which is where a hand goes -- and on the STREET face. The leaf
            // hangs inboard of the wall plane, so its outward face is local
            // zero and the inward one is negative; putting the handle at
            // negative z, which is the arithmetic that looks right, buries it
            // inside the lobby where nobody walking up to the door can see it.
            // Handles on BOTH faces (the door opens from either side). They are children of the leaf, which carries the one
            // kinematic body, so they turn with the slab. The old handle was a sibling of a slab with its OWN interpolated
            // body: the slab's interpolation lagged behind the turning root while the handle did not, so it floated off.
            float handleY = panel.Height * 0.45f;
            float handleX = panel.Width - 0.14f;
            AddHandle(_leaf, handleMaterial, new Vector3(handleX, handleY, 0.0f), 1.0f);
            AddHandle(_leaf, handleMaterial, new Vector3(handleX, handleY, -Thickness), -1.0f);

            // A KINEMATIC RIGIDBODY, not a bare collider. Godot's own note: a
            // static body's collision is treated as not moving between frames,
            // so a player standing at the threshold can be tunnelled through or
            // shoved wrongly as the panel sweeps past. The same is true here.
            // No interpolation: the leaf is turned through its transform, and an interpolated body draws a frame behind it.
            var body = leaf.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;

            // The reach. Bigger than the doorway, because you press E standing
            // in front of a door rather than inside it.
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = InteractRange;
            trigger.center = new Vector3(panel.Width * 0.5f, panel.Height * 0.5f, 0.0f);

            _open = startOpen;
            // Restored open, it stands swung away from the street (openFrom, when given): a pair's right leaf faces in, so "away" is its other way.
            _swingSign = openFrom.HasValue ? SwingSignFor(panel.Hinge, panel.Facing, openFrom.Value) : 1;
            _angle = startOpen ? _swingSign * OpenAngleDegrees : 0.0f;
            _angleVelocity = 0.0f;
            _shutSoundPending = false;
            ApplyAngle();
        }

        /// <summary>A rose plate, neck and lever on one face of the leaf; <paramref name="side"/> is +1 for the outer face, -1 for the inner.</summary>
        private static void AddHandle(Transform leaf, Material material, Vector3 face, float side)
        {
            AddPiece(leaf, material, PrimitiveType.Cylinder, "HandleRose", face + new Vector3(0.0f, 0.0f, side * 0.01f),
                Quaternion.Euler(90.0f, 0.0f, 0.0f), new Vector3(0.12f, 0.01f, 0.12f));
            AddPiece(leaf, material, PrimitiveType.Cube, "HandleNeck", face + new Vector3(0.0f, 0.0f, side * 0.04f),
                Quaternion.identity, new Vector3(0.035f, 0.035f, 0.06f));
            AddPiece(leaf, material, PrimitiveType.Cube, "HandleLever", face + new Vector3(-0.08f, 0.0f, side * 0.07f),
                Quaternion.identity, new Vector3(0.18f, 0.035f, 0.035f));
        }

        private static void AddPiece(Transform leaf, Material material, PrimitiveType type, string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject piece = GameObject.CreatePrimitive(type);
            piece.name = name;
            piece.transform.SetParent(leaf, false);
            piece.transform.localPosition = position;
            piece.transform.localRotation = rotation;
            piece.transform.localScale = scale;
            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
            Destroy(piece.GetComponent<Collider>());
        }

        public bool IsOpen => _open;

        /// <summary>True while something stands in the door's way and it is holding still.</summary>
        public bool IsBlocked => _blocked;

        /// <summary>How far open it is, in degrees from shut (positive inward, negative outward).</summary>
        public float Angle => _angle;

        /// <summary>
        /// Opens or shuts the door programmatically, without needing an
        /// E-press from anyone standing nearby -- what OldGuy uses to let
        /// himself in. Still fires Toggled, so CityStreamer's own
        /// remembered-open-doors tracking never falls out of step with a
        /// door someone other than the player opened.
        /// </summary>
        public void SetOpen(bool open)
        {
            SetOpen(open, null);
        }

        /// <summary>As <see cref="SetOpen(bool)"/>, swinging away from <paramref name="opener"/> when one is given.</summary>
        public void SetOpen(bool open, Transform opener)
        {
            if (_open == open)
            {
                return;
            }
            if (open)
            {
                BeginOpening(opener, burst: false);
            }
            else
            {
                BeginClosing();
            }
        }

        /// <summary>
        /// What pressing E does, for a body at <paramref name="opener"/>. Public so a test can press it
        /// without a keyboard. Returns false if it did nothing (the way is blocked, or it was not looking).
        /// </summary>
        public bool Interact(Transform opener)
        {
            if (_open)
            {
                if (SomethingInTheDoorway() || (Partner != null && Partner.SomethingInTheDoorway()))
                {
                    return false;   // never shut a door on someone
                }
                BeginClosing();
                return true;
            }
            BeginOpening(opener, burst: false);
            return true;
        }

        /// <summary>
        /// Walking into a closed door: opens it away from the body, and throws it open if they were running.
        /// Public for tests. <paramref name="towardDoorSpeed"/> is how fast, in m/s, the body was moving at the door.
        /// </summary>
        public bool Bump(Transform body, float towardDoorSpeed)
        {
            if (_open || towardDoorSpeed < BumpMinSpeed)
            {
                return false;
            }
            BeginOpening(body, burst: towardDoorSpeed >= BurstSpeed);
            return true;
        }

        private void BeginOpening(Transform opener, bool burst, bool sound = true)
        {
            _swingSign = opener != null ? SwingSignFor(_hinge, _facing, opener.position) : _swingSign;
            _open = true;
            _burst = burst;
            _shutSoundPending = false;
            if (sound)
            {
                PositionalSound.Play(DoorSounds.Open, DoorCentre(), burst ? 1.0f : 0.7f, burst ? 1.15f : 1.0f, 0.0f, 25.0f);
            }
            Toggled?.Invoke(true);
            if (Partner != null && !Partner._open)
            {
                Partner.BeginOpening(opener, burst, false);   // the pair opens as one (one latch, one creak)
            }
        }

        private void BeginClosing(bool sound = true)
        {
            _open = false;
            _burst = false;
            _shutSoundPending = sound;
            Toggled?.Invoke(false);
            if (Partner != null && Partner._open)
            {
                Partner.BeginClosing(false);
            }
        }

        private Vector3 DoorCentre()
        {
            return _hinge + Quaternion.Euler(0.0f, _shutYaw, 0.0f) * new Vector3(_width * 0.5f, _height * 0.5f, 0.0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Only the local player: an NPC walking past must not make E work from across the map.
            if (other.GetComponentInParent<PlayerInputReader>() != null)
            {
                CharacterController body = other.GetComponentInParent<CharacterController>();
                _player = body != null ? body.transform : other.transform;
                _playerBody = body;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!CityX.Character.InteractionReach.IsBodyLeaving(other))
            {
                return;   // a limb's collider swinging out of the trigger is not the player walking away
            }
            if (_player != null && other.transform.IsChildOf(_player.root))
            {
                _player = null;
                _playerBody = null;
            }
        }

        private void Update()
        {
            // A collider disabled while still inside the trigger never fires
            // OnTriggerExit -- exactly what happens the instant the player
            // dies (Ragdoll.Activate() turns their CharacterController off
            // mid-frame), which would otherwise leave _player stale and this
            // door openable by E from anywhere, forever after.
            if (_player != null && Vector3.Distance(_player.position, transform.position) > InteractRange + 1.0f)
            {
                _player = null;
                _playerBody = null;
            }

            if (_player != null)
            {
                if (GameControls.Now.Interact.Pressed && IsLookedAt(_player))
                {
                    // Not straight away: the light switch beside the door may be what the player is looking at (InteractionFocus picks one).
                    Transform opener = _player;
                    InteractionFocus.Offer(InteractionFocus.DegreesOff(DoorCentre(), opener.position + Vector3.up * 1.5f, opener.forward),
                        () => Interact(opener));
                }
                else
                {
                    TryBump();
                }
            }

            Swing(Time.deltaTime);
        }

        /// <summary>The player is looking toward the door: within LookAtDegrees of it, from the camera when there is one.</summary>
        private bool IsLookedAt(Transform player)
        {
            Camera camera = Camera.main;
            Vector3 from = camera != null ? camera.transform.position : player.position + Vector3.up * 1.5f;
            Vector3 look = camera != null ? camera.transform.forward : player.forward;
            return IsLookingToward(from, look, DoorCentre());
        }

        /// <summary>Is <paramref name="look"/>, from <paramref name="from"/>, within LookAtDegrees of <paramref name="target"/>? Pure, for tests.</summary>
        public static bool IsLookingToward(Vector3 from, Vector3 look, Vector3 target)
        {
            Vector3 toTarget = target - from;
            return toTarget.sqrMagnitude < 1e-4f || Vector3.Angle(look, toTarget) <= LookAtDegrees;
        }

        private void TryBump()
        {
            if (_open || _playerBody == null || !_playerBody.enabled)
            {
                return;
            }
            // In the door's own frame while it is shut: x runs along the leaf from the hinge, z is out of the wall.
            Vector3 local = transform.InverseTransformPoint(_player.position);
            if (local.x < 0.15f || local.x > _width - 0.15f || Mathf.Abs(local.z) > BumpReachMetres)
            {
                return;
            }
            float side = local.z >= 0.0f ? 1.0f : -1.0f;
            float toward = -side * Vector3.Dot(_playerBody.velocity, _facing);
            Bump(_player, toward);
        }

        /// <summary>Moves the leaf a step toward where it is meant to be, holding still while something is in the way.</summary>
        private void Swing(float deltaTime)
        {
            if (_leaf == null)
            {
                return;
            }

            float target = _open ? _swingSign * OpenAngleDegrees : 0.0f;
            if (Mathf.Approximately(_angle, target) && Mathf.Abs(_angleVelocity) < 0.01f)
            {
                _blocked = false;
                return;
            }

            float smooth = _open ? (_burst ? BurstSmoothSeconds : OpenSmoothSeconds) : CloseSmoothSeconds;
            float velocity = _angleVelocity;
            float next = Mathf.SmoothDamp(_angle, target, ref velocity, smooth, 500.0f, deltaTime);

            // A door stops for whatever is in its way instead of shoving it, and goes on when it has moved.
            if (Mathf.Abs(next - _angle) > 1e-4f && LeafAtWouldTouchABody(next))
            {
                _blocked = true;
                _angleVelocity = 0.0f;
                return;
            }
            _blocked = false;

            float closingSpeed = Mathf.Abs(velocity);
            _angle = next;
            _angleVelocity = velocity;

            if (Mathf.Abs(_angle - target) < 0.05f && Mathf.Abs(_angleVelocity) < 1.0f)
            {
                _angle = target;
                _angleVelocity = 0.0f;
            }
            ApplyAngle();

            // The thud, the moment a closing door meets its frame -- louder the harder it came.
            if (_shutSoundPending && !_open && Mathf.Abs(_angle) < 2.0f)
            {
                _shutSoundPending = false;
                PositionalSound.Play(DoorSounds.Shut, DoorCentre(), Mathf.Clamp(0.35f + closingSpeed / 260.0f, 0.35f, 1.0f), 1.0f, 0.0f, 25.0f);
            }
        }

        private void ApplyAngle()
        {
            if (_leaf == null)
            {
                return;
            }
            transform.rotation = Quaternion.Euler(0.0f, _shutYaw + _angle, 0.0f);
        }

        /// <summary>
        /// Would the leaf, turned to <paramref name="angle"/>, run into a person? Only bodies AHEAD of the
        /// moving leaf count -- one standing on the side it is swinging away from (the opener, pressed
        /// against the door) is not in its way, and the wall and the floor are always brushing it.
        /// </summary>
        private bool LeafAtWouldTouchABody(float angle)
        {
            Quaternion rotation = Quaternion.Euler(0.0f, _shutYaw + angle, 0.0f);
            Vector3 centre = _hinge + rotation * new Vector3(_width * 0.5f, _height * 0.5f, -Thickness * 0.5f);
            // A little bigger than the leaf, so a body is stopped before it is touched.
            Vector3 half = new Vector3(_width * 0.5f, _height * 0.5f - 0.05f, Thickness * 0.5f + 0.12f);
            float motion = Mathf.Sign(angle - _angle);
            return AnyBodyIn(centre, half, rotation, motion, rotation);
        }

        /// <summary>Is someone standing in the doorway itself (where a closing door would land)?</summary>
        private bool SomethingInTheDoorway()
        {
            Quaternion rotation = Quaternion.Euler(0.0f, _shutYaw, 0.0f);
            Vector3 centre = _hinge + rotation * new Vector3(_width * 0.5f, _height * 0.5f, 0.0f);
            return AnyBodyIn(centre, new Vector3(_width * 0.5f, _height * 0.5f - 0.05f, 0.35f), rotation, 0.0f, rotation);
        }

        /// <param name="motionSign">+1 / -1 for the way the leaf is turning (only bodies ahead of it count), 0 for any body at all.</param>
        private bool AnyBodyIn(Vector3 centre, Vector3 halfExtents, Quaternion boxRotation, float motionSign, Quaternion leafRotation)
        {
            int count = Physics.OverlapBoxNonAlloc(centre, halfExtents, Overlaps, boxRotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = Overlaps[i];
                if (collider == null || collider.transform.IsChildOf(transform))
                {
                    continue;
                }
                CharacterController body = collider.GetComponentInParent<CharacterController>();
                if (body == null || !body.enabled)
                {
                    continue;
                }
                if (motionSign == 0.0f || IsAheadOfLeaf(collider.bounds.center, leafRotation, motionSign))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Is a body in the direction the leaf is moving? The leaf's centre line runs from the hinge; the point
        /// of it nearest the body moves along (z, -x) of its offset from the hinge when the yaw grows.
        /// </summary>
        private bool IsAheadOfLeaf(Vector3 bodyCentre, Quaternion leafRotation, float motionSign)
        {
            Vector3 along = leafRotation * Vector3.right;                 // the leaf's length, from the hinge
            Vector3 fromHinge = bodyCentre - _hinge;
            fromHinge.y = 0.0f;
            float distanceAlong = Mathf.Clamp(Vector3.Dot(fromHinge, along), 0.0f, _width);
            Vector3 nearest = along * distanceAlong;                       // the leaf's nearest point, from the hinge
            Vector3 velocity = motionSign * new Vector3(nearest.z, 0.0f, -nearest.x);
            if (velocity.sqrMagnitude < 1e-6f)
            {
                return true;   // right at the hinge: no telling which way it moves, so be careful
            }
            return Vector3.Dot(fromHinge - nearest, velocity) > 0.0f;
        }
    }
}
