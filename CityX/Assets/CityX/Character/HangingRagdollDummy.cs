using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A real, standing ragdoll hanging from a rope tied to its own hand --
    /// permanently limp from the moment the scene starts, not triggered by
    /// death. Built specifically as a live reference: this project's own
    /// grapple-swing distortion investigation kept finding that Ragdoll.cs's
    /// own physics is stable in isolation (see the memory that led here),
    /// and this is the same claim made concrete and walkable-up-to in the
    /// actual game, near spawn, so it can be judged directly rather than
    /// only through a diagnostic render.
    ///
    /// EVERYTHING BUT THE ROPE'S OWN VISUAL is just Ragdoll.cs plus one
    /// extra joint. Activation, gravity, self-collision -- all the same
    /// code every other ragdoll in the game already uses. The rope mesh is
    /// the one thing that has to run every frame, since the hand it is tied
    /// to keeps swinging.
    /// </summary>
    public sealed class HangingRagdollDummy : MonoBehaviour
    {
        [Tooltip("The ragdoll to activate on Start. Never stands back up.")]
        public Ragdoll Ragdoll;

        [Tooltip("The lower-arm rigidbody the rope is tied to -- RagdollBuilder gives the hand bone itself no rigidbody of its own, only the capsule spanning elbow to hand.")]
        public Rigidbody HandBody;

        [Tooltip("The actual hand bone -- read once in Start to work out where on HandBody's own local space the rope should really anchor, rather than HandBody's own capsule centre.")]
        public Transform HandBone;

        [Tooltip("Where the rope's other end is anchored -- a fixed point on the pole/hook, not a moving body.")]
        public Transform AnchorPoint;

        [Tooltip("How far below AnchorPoint the rope is allowed to hang before going taut.")]
        public float RopeLength = 1.4f;

        public Material RopeMaterial;

        /// <summary>character.gd has no equivalent -- this prop's own number, tuned once against a live render rather than ported from anywhere.</summary>
        private const float RopeRadius = 0.02f;

        private const int RopeSides = 6;

        private SpringJoint _joint;
        private MeshFilter _rope;
        private Mesh _ropeMesh;
        private Vector3[] _ropeVertices;
        private Vector3[] _ropeNormals;

        private void Start()
        {
            if (Ragdoll == null || HandBody == null || AnchorPoint == null)
            {
                Debug.LogError("CityX: HangingRagdollDummy is missing a wired reference -- nothing to hang.");
                return;
            }

            // PERMANENTLY LIMP FROM FRAME ONE -- no standing state, no
            // trigger, matching the whole point of this prop: a real,
            // always-on ragdoll to walk up to.
            Ragdoll.Activate(Vector3.zero);

            // THE ONE JOINT THAT ISN'T ALREADY PART OF Ragdoll.cs's OWN
            // RIG -- a real distance tether, added AFTER Activate so it
            // only ever has to constrain an already-dynamic body, the same
            // ordering every internal Ragdoll joint is built in relative to
            // when it starts actually mattering.
            _joint = HandBody.gameObject.AddComponent<SpringJoint>();
            _joint.autoConfigureConnectedAnchor = false;
            // THE REAL HAND POSITION, not HandBody's own capsule centre --
            // see HandBone's own doc comment for why RagdollBuilder never
            // gives the hand bone a rigidbody to anchor onto directly.
            _joint.anchor = HandBone != null
                ? HandBody.transform.InverseTransformPoint(HandBone.position)
                : Vector3.zero;
            _joint.connectedBody = null;
            _joint.connectedAnchor = AnchorPoint.position;
            _joint.minDistance = 0.0f;
            _joint.maxDistance = RopeLength;
            _joint.spring = 4000.0f;
            _joint.damper = 120.0f;
            _joint.tolerance = 0.01f;
            _joint.enableCollision = false;

            BuildRope();
        }

        private void BuildRope()
        {
            var ropeObject = new GameObject("HangingDummyRope");
            ropeObject.transform.SetParent(transform, false);
            _rope = ropeObject.AddComponent<MeshFilter>();
            var renderer = ropeObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = RopeMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            _ropeMesh = new Mesh { name = "HangingDummyRopeMesh" };
            _ropeMesh.MarkDynamic();
            _ropeVertices = new Vector3[RopeSides * 2];
            _ropeNormals = new Vector3[RopeSides * 2];

            var triangles = new int[RopeSides * 6];
            for (int i = 0; i < RopeSides; i++)
            {
                int next = (i + 1) % RopeSides;
                int a0 = i;
                int b0 = i + RopeSides;
                int a1 = next;
                int b1 = next + RopeSides;
                int t = i * 6;
                triangles[t + 0] = a0;
                triangles[t + 1] = b0;
                triangles[t + 2] = b1;
                triangles[t + 3] = a0;
                triangles[t + 4] = b1;
                triangles[t + 5] = a1;
            }
            _ropeMesh.vertices = _ropeVertices;
            _ropeMesh.triangles = triangles;
            _rope.sharedMesh = _ropeMesh;
        }

        /// <summary>
        /// PUBLIC BECAUSE LATEUPDATE IS NOT AVAILABLE OUTSIDE PLAY MODE --
        /// same reasoning every other procedural mesh in this project
        /// already gives (GrapplingHookItem.UpdateRope, CharacterHair's own
        /// doc comment) -- a capture/render tool needs to be able to drive
        /// this by hand.
        /// </summary>
        public void UpdateRope()
        {
            if (_rope == null || HandBody == null || AnchorPoint == null)
            {
                return;
            }

            Vector3 from = AnchorPoint.position;
            // THE REAL HAND POSITION, not HandBody's own capsule-centre
            // transform -- HandBone is a child of the lower-arm bone
            // physics is driving, so its world position already tracks
            // correctly; HandBody.position would draw the rope to the
            // elbow-ish capsule centre instead of the hand it is tied to.
            Vector3 to = HandBone != null ? HandBone.position : HandBody.position;
            Vector3 axis = to - from;
            float length = axis.magnitude;
            Vector3 direction = length > 0.0001f ? axis / length : Vector3.down;
            Vector3 helper = Mathf.Abs(Vector3.Dot(direction, Vector3.forward)) < 0.95f
                ? Vector3.forward
                : Vector3.right;
            Vector3 xAxis = Vector3.Cross(direction, helper).normalized;
            Vector3 zAxis = Vector3.Cross(xAxis, direction).normalized;

            for (int i = 0; i < RopeSides; i++)
            {
                float angle = (float)i / RopeSides * Mathf.PI * 2.0f;
                Vector3 radial = xAxis * Mathf.Cos(angle) + zAxis * Mathf.Sin(angle);
                _ropeVertices[i] = from + radial * RopeRadius;
                _ropeVertices[i + RopeSides] = to + radial * RopeRadius;
                _ropeNormals[i] = radial;
                _ropeNormals[i + RopeSides] = radial;
            }
            _ropeMesh.SetVertices(_ropeVertices);
            _ropeMesh.SetNormals(_ropeNormals);
            _ropeMesh.RecalculateBounds();
        }

        private void LateUpdate()
        {
            UpdateRope();
        }
    }
}
