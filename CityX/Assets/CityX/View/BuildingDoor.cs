using CityX.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.View
{
    /// <summary>
    /// The door in a building's entrance. Press E while near it to swing it
    /// open, again to close it.
    ///
    /// Ported from city/building_door.gd, including its two decisions worth
    /// keeping.
    ///
    /// IT SWINGS INWARD, around its own left edge. Outward would swing out over
    /// the pavement, which reads wrong and would shove a player standing just
    /// outside it.
    ///
    /// AND IT ASKS FOR THE KEY ITSELF rather than being told by the player.
    /// That is Godot's arrangement -- the door tracks who is near it and checks
    /// the interact action in its own process -- and here it also happens to be
    /// the only arrangement that does not make the character code depend on the
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

        public const float OpenSeconds = 0.5f;
        public const float InteractRange = 2.5f;

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
            return new Panel(
                new Vector3(
                    (float)site.CentreX - width * 0.5f,
                    site.BaseHeight,
                    (float)site.MaxZ),
                width,
                BuildingInterior.DoorHeight(site.Params.Height),
                Vector3.forward);
        }

        /// <summary>Raised when the door settles open or shut, so the streamer can remember.</summary>
        public event System.Action<bool> Toggled;

        private Transform _leaf;
        private float _shutYaw;
        private bool _open;
        private float _swing;          // 0 shut, 1 open
        private Transform _nearby;

        /// <summary>
        /// Builds the leaf and hangs it. <paramref name="startOpen"/> restores
        /// a door the player already opened -- see the streamer's own note on
        /// why that has to be remembered somewhere other than here.
        /// </summary>
        public void Hang(in Panel panel, Material leafMaterial, Material handleMaterial,
            bool startOpen)
        {
            transform.position = panel.Hinge;
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
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Handle";
            handle.transform.SetParent(_leaf, false);
            handle.transform.localPosition = new Vector3(
                panel.Width - 0.12f, panel.Height * 0.45f, 0.04f);
            handle.transform.localScale = new Vector3(0.06f, 0.16f, 0.06f);
            handle.GetComponent<MeshRenderer>().sharedMaterial = handleMaterial;
            Destroy(handle.GetComponent<BoxCollider>());

            // A KINEMATIC RIGIDBODY, not a bare collider. Godot's own note: a
            // static body's collision is treated as not moving between frames,
            // so a player standing at the threshold can be tunnelled through or
            // shoved wrongly as the panel sweeps past. The same is true here.
            var body = slab.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            // The reach. Bigger than the doorway, because you press E standing
            // in front of a door rather than inside it.
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = InteractRange;
            trigger.center = new Vector3(panel.Width * 0.5f, panel.Height * 0.5f, 0.0f);

            _open = startOpen;
            _swing = startOpen ? 1.0f : 0.0f;
            ApplySwing();
        }

        public bool IsOpen => _open;

        /// <summary>
        /// Opens or shuts the door programmatically, without needing an
        /// E-press from anyone standing nearby -- what OldGuy uses to let
        /// himself in. Still fires Toggled, so CityStreamer's own
        /// remembered-open-doors tracking never falls out of step with a
        /// door someone other than the player opened.
        /// </summary>
        public void SetOpen(bool open)
        {
            if (_open == open)
            {
                return;
            }
            _open = open;
            Toggled?.Invoke(_open);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<CharacterController>() != null)
            {
                _nearby = other.transform;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_nearby != null && other.transform.IsChildOf(_nearby.root))
            {
                _nearby = null;
            }
        }

        private void Update()
        {
            if (_nearby != null && Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                _open = !_open;
                Toggled?.Invoke(_open);
            }

            float target = _open ? 1.0f : 0.0f;
            if (!Mathf.Approximately(_swing, target))
            {
                _swing = Mathf.MoveTowards(_swing, target, Time.deltaTime / OpenSeconds);
                ApplySwing();
            }
        }

        private void ApplySwing()
        {
            if (_leaf == null)
            {
                return;
            }
            // Smoothed rather than linear, so it reads as a door being pushed
            // rather than as a hinge being driven at constant speed.
            float eased = Mathf.SmoothStep(0.0f, 1.0f, _swing);
            transform.rotation = Quaternion.Euler(
                0.0f, _shutYaw + OpenAngleDegrees * eased, 0.0f);
        }
    }
}
