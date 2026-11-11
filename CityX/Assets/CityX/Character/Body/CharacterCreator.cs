using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// The character creator: orbit the body by dragging the space around it,
    /// paint it by dragging the body itself. Ported from character_creator.gd.
    ///
    /// WHICH ONE HAPPENS IS DECIDED ONCE, AT MOUSE-DOWN, by whether that click
    /// actually hit the character. So a drag started on empty space stays a
    /// rotate even if the spinning model sweeps under the cursor halfway
    /// through, and a stroke started on the body stays a stroke even if it
    /// wanders off the silhouette.
    /// </summary>
    public sealed partial class CharacterCreator : MonoBehaviour
    {
        public enum Tool
        {
            Brush,
            Bucket,
            Eyedropper,
        }

        [Header("Wired by CharacterCreatorBuilder")]
        public Transform RotationPivot;

        public Camera Camera;

        [Tooltip("The body's collider, so a click can be turned into a triangle.")]
        public MeshCollider BodyCollider;

        public SkinnedMeshRenderer BodyRenderer;

        [Tooltip("The skull's own collider. It is a separate mesh -- see HeadMesh.")]
        public MeshCollider HeadCollider;

        public MeshRenderer HeadRenderer;

        [Tooltip("The hairstyle on the body being made. Set by the Hair buttons.")]
        public CharacterHair Hair;

        [Tooltip("The body keeps its ragdoll (held still by its Ragdoll until it is let go): the title's creator, whose body is dropped off the belt's end. Off, every joint, rigidbody and collider but the click targets is taken off when it wakes.")]
        public bool KeepsRagdoll;

        [Header("Tools")]
        public Tool Current = Tool.Brush;

        public Color Colour = new Color(0.2f, 0.2f, 0.25f);

        public bool Erasing;

        public bool Mirroring;

        [Tooltip("The brush size, 1..15: each step is BodyPaint.BrushStepMetres more radius ON THE BODY (the old name is kept for saved scenes).")]
        [Range(1, 15)]
        public int BrushRadiusPixels = DefaultBrushRadius;

        // --- character_creator.gd's own constants ---

        public const int DefaultBrushRadius = 1;

        public const float RayLength = 50.0f;

        public const float RotateSensitivity = 0.25f;

        public static readonly Color EraseColour = Color.white;

        /// <summary>
        /// The furthest apart, in SCREEN pixels, two consecutive stamps of a
        /// drag are allowed to be.
        ///
        /// A fast drag jumps many screen pixels between mouse events, and the
        /// atlas is not uniformly scaled across the body -- limb radius and
        /// length vary, and the blended joints deviate from the straight
        /// cylinder the UV formula assumes -- so the same screen jump can land
        /// stamps far apart in ATLAS space and leave a dotted line.
        /// Re-raycasting at even steps along the screen path closes those gaps
        /// whatever the local distortion is.
        /// </summary>
        public const float PaintStepPixels = 2.0f;

        /// <summary>From the body's middle: close enough for the head (0.4 m in front of it) to fill the view.</summary>
        public const float MinimumCameraDistance = 0.9f;

        public const float MaximumCameraDistance = 8.0f;

        public const float ZoomStep = 0.35f;

        private BodyPaint _paint;
        private bool _painting;
        private bool _rotating;
        private float _yaw;
        private float _pitch;
        private Vector2 _lastPaintScreenPosition;
        private Vector3? _cameraHome;

        public BodyPaint Paint => _paint;

        private void Awake()
        {
            Initialise();
        }

        /// <summary>
        /// Builds the atlas and points the body at it.
        ///
        /// PUBLIC, AND SAFE TO CALL TWICE, so the editor's screenshot tool can
        /// get the same body the player sees. Awake does not run outside play
        /// mode, and without this the shot came out with the body wearing its
        /// world material over paint-atlas UVs, which is not what the creator
        /// ever shows.
        /// </summary>
        public void Initialise()
        {
            if (_paint != null)
            {
                return;
            }

            Disarm();
            _paint = new BodyPaint(EraseColour);

            if (BodyRenderer != null && BodyRenderer.sharedMesh != null)
            {
                Mesh head = HeadRenderer != null
                    ? HeadRenderer.GetComponent<MeshFilter>()?.sharedMesh
                    : null;

                // Into the body's space. The head is a child of the head bone
                // and unposed while it is being painted, so this is fixed.
                Matrix4x4 headToBody = head != null
                    ? BodyRenderer.transform.worldToLocalMatrix
                      * HeadRenderer.transform.localToWorldMatrix
                    : Matrix4x4.identity;

                // THE BODY AS IT STANDS, NOT ITS MESH AT REST. The mesh is modelled arms-down (BodyRig.RestBindMatrix) and the creator shows it in
                // a T-pose, so a collider and a brush on the rest mesh put the arms where they are not: a click on an outstretched arm missed it,
                // and one beside the hips painted an arm nobody could see. Baked once -- the body holds still here (Disarm).
                Mesh posed = PosedBody();
                _paint.Bind(posed, head, headToBody);
                RememberStillSurface(posed, head, headToBody);
                if (BodyCollider != null)
                {
                    BodyCollider.sharedMesh = posed;
                }
                if (Application.isPlaying)
                {
                    _paint.PrepareBrush();
                }
                ApplyToRenderer();
            }
        }

        private Mesh PosedBody()
        {
            var posed = new Mesh { name = "CreatorPosedBody", hideFlags = HideFlags.DontSave };
            BodyRenderer.BakeMesh(posed);
            posed.RecalculateBounds();
            return posed.vertexCount == BodyRenderer.sharedMesh.vertexCount ? posed : BodyRenderer.sharedMesh;
        }

        /// <summary>Points the body's material at the paint texture.</summary>
        public void ApplyToRenderer()
        {
            if (BodyRenderer == null)
            {
                return;
            }
            Wear(BodyRenderer);
            // The head wears the same atlas from its own row of it.
            Wear(HeadRenderer);
        }

        private void Wear(Renderer target)
        {
            if (target == null)
            {
                return;
            }
            Material instance = target.material;
            if (instance == null)
            {
                return;
            }
            if (instance.HasProperty("_BaseMap"))
            {
                instance.SetTexture("_BaseMap", _paint.Atlas);
            }
            instance.mainTexture = _paint.Atlas;
            // White, so the paint shows as the colour it was painted rather
            // than tinted by whatever skin colour was set underneath.
            if (instance.HasProperty("_BaseColor"))
            {
                instance.SetColor("_BaseColor", Color.white);
            }
        }

        /// <summary>
        /// Set while a prompt is up, so the body underneath does not keep
        /// taking clicks meant for the thing on top of it.
        /// </summary>
        public bool InputBlocked;

        /// <summary>
        /// Off while the creator's HAIR or FACE tab is up (CreatorTabs): a press on the body then turns it, as one beside it does, and no
        /// brush ring shows, so turning the head round to look at a hairstyle never leaves a stray dot of paint.
        /// </summary>
        public bool PaintEnabled = true;

        /// <summary>
        /// Whether the body is in the pose it woke in, which is the pose the click target is. False while it stands and idles (CreatorLife):
        /// the ray then finds the body as it stands this frame (<see cref="RaycastAsItStands"/>).
        /// </summary>
        public bool HoldingStill = true;

        /// <summary>
        /// How long a frame spends pouring a bucket fill, milliseconds (owner, 2026-11-11: "there is a small drop in frames whenever i use the
        /// paint bucket tool"): the whole body pours over in a few tenths of a second instead of the game stopping while it is done.
        /// </summary>
        public const double BucketMillisecondsPerFrame = 5.0;

        /// <summary>How many texels are poured between looks at the clock.</summary>
        private const int BucketTexelsAtATime = 8192;

        /// <summary>Switched off with a fill still pouring (PROCEED as it spreads): it is finished, so what is saved and seen is the whole of it.</summary>
        private void OnDisable()
        {
            if (_paint != null && _paint.BucketFilling)
            {
                _paint.FinishBucketFill();
                _paint.Apply();
            }
        }

        /// <summary>Pours the fill under way for <see cref="BucketMillisecondsPerFrame"/>, and shows it.</summary>
        private void PourTheBucket()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (_paint.ContinueBucketFill(BucketTexelsAtATime) && clock.Elapsed.TotalMilliseconds < BucketMillisecondsPerFrame)
            {
            }
            _paint.Apply();
        }

        private void Update()
        {
            if (_paint != null && _paint.BucketFilling)
            {
                PourTheBucket();
            }
            Mouse mouse = Mouse.current;
            if (mouse == null || Camera == null || InputBlocked)
            {
                return;
            }

            Vector2 position = mouse.position.ReadValue();

            // Not over the panel: there the wheel scrolls the hairstyle pictures, and zooming the body as well made the list unusable.
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !OverInterface())
            {
                ZoomAt(position, scroll > 0.0f);
            }

            // [ and ] step the brush size, as in most paint programs, without reaching for the slider.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftBracketKey.wasPressedThisFrame)
                {
                    SetBrushSize(BrushRadiusPixels - 1);
                }
                else if (keyboard.rightBracketKey.wasPressedThisFrame)
                {
                    SetBrushSize(BrushRadiusPixels + 1);
                }
            }

            ShowBrushRing(position);

            if (mouse.leftButton.wasPressedThisFrame)
            {
                // Godot's creator read the mouse from _unhandled_input, which a
                // Control that was clicked has already swallowed. Reading the
                // device directly here does not get that for free: without this
                // check, dragging the brush slider also spun the model, because
                // the press missed the body and started a rotate.
                if (OverInterface())
                {
                    return;
                }
                PressAt(position);
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                _painting = false;
                _rotating = false;
            }
            else if (mouse.leftButton.isPressed)
            {
                Drag(position, mouse.delta.ReadValue());
            }
        }

        /// <summary>
        /// Takes the ragdoll off the body being made, and stops it animating.
        ///
        /// THE BUILDER ALREADY DOES THIS, AND IT IS STILL WORTH DOING HERE.
        /// The body is cloned from the world prefab, which is a full ragdoll --
        /// a rigidbody, a joint and a collider on every bone. The first attempt
        /// to strip that destroyed the rigidbodies before the joints, and Unity
        /// will not remove a component another one depends on, so ten of each
        /// survived into the saved scene with their colliders gone. They had
        /// nothing to rest on: the instant play started the bones fell away and
        /// dragged the skin out into a twisted ribbon.
        ///
        /// Doing it again at runtime costs one pass over the hierarchy and
        /// means a scene that was built wrong -- or built before the fix, and
        /// never rebuilt -- still opens a creator you can paint in.
        /// </summary>
        private void Disarm()
        {
            // The body made on the belt keeps its whole ragdoll (KeepsRagdoll: it is dropped off the belt's end). Its Ragdoll holds every
            // bone still until it is let go, and its colliders are whole, so nothing falls away.
            if (!KeepsRagdoll)
            {
                // Joints before bodies: a joint depends on the body beside it.
                foreach (Joint joint in GetComponentsInChildren<Joint>(true))
                {
                    Remove(joint);
                }
                foreach (Rigidbody rigid in GetComponentsInChildren<Rigidbody>(true))
                {
                    // Kinematic first, because Destroy only takes effect at the end
                    // of the frame and a fixed step can land before then -- one
                    // step of a colliderless ragdoll is one step too many.
                    rigid.isKinematic = true;
                    Remove(rigid);
                }
                foreach (Collider collider in GetComponentsInChildren<Collider>(true))
                {
                    // Except the two a click is tested against.
                    if (collider != BodyCollider && collider != HeadCollider)
                    {
                        Remove(collider);
                    }
                }
            }

            // And still: the paint collider is baked from the UNPOSED mesh, so
            // a body that animates is a body where strokes land off target.
            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }
        }

        /// <summary>
        /// Destroy refuses to run outside play mode, and Initialise is called
        /// by the editor's screenshot tool as well as by Awake.
        /// </summary>
        private static void Remove(Component component)
        {
            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }

        private static bool OverInterface()
        {
            EventSystem events = EventSystem.current;
            return events != null && events.IsPointerOverGameObject();
        }

        /// <summary>Straight in or out (negative is in), with no mouse to zoom toward.</summary>
        public void Zoom(float step)
        {
            if (Camera == null)
            {
                return;
            }
            Vector3 local = Camera.transform.localPosition;
            local.z += local.z < 0.0f ? -step : step;
            Camera.transform.localPosition = CreatorZoom.Kept(local, CameraHome, MinimumCameraDistance, MaximumCameraDistance);
        }

        /// <summary>One notch in or out, keeping what is under <paramref name="screenPosition"/> under it (CreatorZoom).</summary>
        public void ZoomAt(Vector2 screenPosition, bool closer)
        {
            if (Camera == null)
            {
                return;
            }
            Vector3 home = CameraHome;
            Transform view = Camera.transform;
            view.localPosition = CreatorZoom.Step(view.localPosition, CreatorZoom.UpAndDownOnly(FocusUnder(screenPosition), home), closer, home, MinimumCameraDistance, MaximumCameraDistance);
        }

        /// <summary>Where the camera stands, in its parent's space, before it is zoomed: where the belt is seen from (the character list's view too).</summary>
        public Vector3 CameraHome
        {
            get
            {
                _cameraHome ??= Camera.transform.localPosition;
                return _cameraHome.Value;
            }
        }

        /// <summary>What a screen point is over, in the camera's parent's space: the body or head, or else the upright plane through the body's middle.</summary>
        private Vector3 FocusUnder(Vector2 screenPosition)
        {
            Ray ray = Camera.ScreenPointToRay(screenPosition);
            Transform stage = Camera.transform.parent;
            float nearest = RayLength;
            bool found = false;
            Vector3 point = default;
            foreach (Collider part in new Collider[] { BodyCollider, HeadCollider })
            {
                if (part != null && part.Raycast(ray, out RaycastHit hit, nearest))
                {
                    nearest = hit.distance;
                    point = hit.point;
                    found = true;
                }
            }
            if (found)
            {
                return stage != null ? stage.InverseTransformPoint(point) : point;
            }

            return OnTheBodysPlane(ray);
        }

        /// <summary>Where <paramref name="ray"/> crosses the upright plane through the body's middle, in the stage's frame.</summary>
        private Vector3 OnTheBodysPlane(Ray ray)
        {
            Transform stage = Camera.transform.parent;
            Vector3 from = stage != null ? stage.InverseTransformPoint(ray.origin) : ray.origin;
            Vector3 way = stage != null ? stage.InverseTransformDirection(ray.direction) : ray.direction;
            float along = Mathf.Abs(way.z) > 1e-4f ? -from.z / way.z : -1.0f;
            return along > 0.0f ? from + way * along : from + way;
        }

        /// <summary>
        /// The view the body is painted from, as a share of the camera's distance from where it starts: close enough for detail, the whole body
        /// still in sight (owner, 2026-11-10: "make it zoom into this camera angle when as the editor tools fade in, at the current pov it is too
        /// far back for any detailed painting"; the screenshot had the body at 62% of the screen's height, against 43%).
        /// </summary>
        public const float PaintingViewShare = 0.69f;

        /// <summary>What the painting view closes in on: the point this far up the screen from where the camera starts (the body's middle).</summary>
        private const float PaintingViewFocusHeight = 0.44f;

        /// <summary>The camera <paramref name="share"/> of the way (eased) from where it starts to the painting view.</summary>
        public void EaseIntoPaintingView(float share)
        {
            if (Camera == null)
            {
                return;
            }
            Vector3 home = CameraHome;
            Transform view = Camera.transform;
            view.localPosition = home;
            Vector3 focus = CreatorZoom.UpAndDownOnly(
                OnTheBodysPlane(Camera.ViewportPointToRay(new Vector3(0.5f, PaintingViewFocusHeight, 0.0f))), home);
            Vector3 painting = CreatorZoom.Kept(focus + (home - focus) * PaintingViewShare, home, MinimumCameraDistance, MaximumCameraDistance);
            view.localPosition = Vector3.Lerp(home, painting, Mathf.SmoothStep(0.0f, 1.0f, share));
        }

        /// <summary>
        /// A press at a screen point: the whole chain from a pixel to a
        /// painted texel.
        ///
        /// PUBLIC SO A TEST CAN DRIVE IT. Every step of this -- the raycast
        /// finding the collider, the hit becoming a triangle, the triangle
        /// becoming atlas coordinates -- fails by doing nothing visible, and
        /// reading the mouse device directly from Update leaves no way in.
        /// </summary>
        public void PressAt(Vector2 screenPosition)
        {
            if (!PaintEnabled)
            {
                _rotating = true;
                return;
            }
            if (!Raycast(screenPosition, out Vector3 local, out int triangle))
            {
                _rotating = true;
                return;
            }

            if (Current == Tool.Eyedropper)
            {
                // Not an edit: no snapshot and no _painting, so holding the
                // button afterward does not drag-paint or re-sample.
                Colour = _paint.SampleAt(local, triangle);
                Erasing = false;
                Current = Tool.Brush;
                return;
            }

            _paint.PushUndoSnapshot();

            if (Current == Tool.Bucket)
            {
                // A single click, not a stroke -- leaving _painting false stops
                // the drag afterwards re-flooding the same region every frame.
                Fill(local, triangle);
                PourTheBucket();   // its first share at once; Update pours the rest
                return;
            }

            _painting = true;
            _lastPaintScreenPosition = screenPosition;
            Stamp(local, triangle);
            _paint.Apply();
        }

        /// <summary>Whether a press finds the body at all. Off once it is the ragdoll's: its limbs would fall against their own click target.</summary>
        public void SetClickable(bool clickable)
        {
            if (BodyCollider != null) BodyCollider.enabled = clickable;
            if (HeadCollider != null) HeadCollider.enabled = clickable;
            if (!clickable && _ring != null)
            {
                _ring.enabled = false;
            }
        }

        /// <summary>
        /// Turns the body back toward the camera and the camera back to where it started, <paramref name="share"/> of the way (1: at
        /// once): the belt carries the body off facing front, however it was turned and zoomed to be painted.
        /// </summary>
        public void TurnToFront(float share)
        {
            share = Mathf.Clamp01(share);
            _yaw = Mathf.LerpAngle(_yaw, 0.0f, share);
            _pitch = Mathf.LerpAngle(_pitch, 0.0f, share);
            if (RotationPivot != null)
            {
                RotationPivot.localRotation = Quaternion.Euler(_pitch, _yaw, 0.0f);
            }
            if (Camera != null)
            {
                Camera.transform.localPosition = Vector3.Lerp(Camera.transform.localPosition, CameraHome, share);
            }
        }

        /// <summary>Continues whatever PressAt started. Public for the same reason.</summary>
        public void Drag(Vector2 screenPosition, Vector2 delta)
        {
            if (_painting)
            {
                PaintAlong(screenPosition);
                return;
            }
            if (!_rotating || RotationPivot == null)
            {
                return;
            }

            // Absolute accumulated angles, not incremental rotations. Composing
            // local rotations frame to frame drifts off-axis and introduces
            // roll over a long drag; setting both fresh keeps yaw and pitch
            // clean however much it gets spun.
            _yaw -= delta.x * RotateSensitivity;
            _pitch -= delta.y * RotateSensitivity;
            RotationPivot.localRotation = Quaternion.Euler(_pitch, _yaw, 0.0f);
        }

        /// <summary>
        /// Follows the screen path since the last event in small steps, finding the body under each, and lays stamps along the body between
        /// them at the brush's own spacing (BodyPaint.StrokeSpacingMetres): a smooth line at every size, not a stamp per mouse event.
        /// </summary>
        private void PaintAlong(Vector2 screenPosition)
        {
            float distance = Vector2.Distance(_lastPaintScreenPosition, screenPosition);
            int steps = Mathf.Max(1, Mathf.FloorToInt(distance / PaintStepPixels));
            float spacing = BodyPaint.StrokeSpacingMetres(BrushRadiusPixels);

            for (int i = 1; i <= steps; i++)
            {
                Vector2 at = Vector2.Lerp(_lastPaintScreenPosition, screenPosition, i / (float)steps);
                if (!Raycast(at, out Vector3 local, out int triangle))
                {
                    _hasLastStamp = false;
                    continue;
                }
                if (!_hasLastStamp)
                {
                    Stamp(local, triangle);
                    continue;
                }
                float apart = Vector3.Distance(_lastStampLocal, local);
                if (apart < spacing)
                {
                    continue;
                }
                if (apart > spacing + MaximumBridgeMetres)
                {
                    // Off one part and onto another behind it: a bridge would cut through the air between them.
                    Stamp(local, triangle);
                    continue;
                }
                Bridge(local, triangle);
            }

            _lastPaintScreenPosition = screenPosition;
            _paint.Apply();
        }

        /// <summary>The furthest two stamps of one stroke are joined across: past this, the cursor jumped from one part of the body to another.</summary>
        public const float MaximumBridgeMetres = 0.05f;

        private Vector3 _lastStampLocal;
        private int _lastStampTriangle;
        private bool _hasLastStamp;

        private Color ActiveColour => Erasing ? EraseColour : Colour;

        private void Stamp(Vector3 local, int triangle)
        {
            _paint.PaintAt(local, triangle, ActiveColour, BrushRadiusPixels);
            if (Mirroring)
            {
                _paint.PaintMirroredAt(local, triangle, ActiveColour, BrushRadiusPixels);
            }
            Remember(local, triangle);
        }

        private void Bridge(Vector3 local, int triangle)
        {
            _paint.PaintStroke(_lastStampLocal, _lastStampTriangle, local, triangle, ActiveColour, BrushRadiusPixels);
            if (Mirroring)
            {
                _paint.PaintStroke(BodyPaint.Mirror(_lastStampLocal), BodyPaint.Mirror(NormalOf(_lastStampTriangle)),
                    BodyPaint.Mirror(local), BodyPaint.Mirror(NormalOf(triangle)), ActiveColour, BrushRadiusPixels);
            }
            Remember(local, triangle);
        }

        private Vector3 NormalOf(int triangle)
        {
            return _paint.TryNormalOf(triangle, out Vector3 normal) ? normal : Vector3.forward;
        }

        private void Remember(Vector3 local, int triangle)
        {
            _lastStampLocal = local;
            _lastStampTriangle = triangle;
            _hasLastStamp = true;
        }

        public const int MinimumBrushSize = 1;
        public const int MaximumBrushSize = 15;

        public void SetBrushSize(int size)
        {
            BrushRadiusPixels = Mathf.Clamp(size, MinimumBrushSize, MaximumBrushSize);
        }

        // --- the brush ring: how big the brush is, drawn on the body under the cursor ---

        /// <summary>
        /// A ring on the body under the cursor, exactly the brush's size, so the size is seen before it is painted (every 3D paint tool has one;
        /// Meccha Chameleon's paint mode among them). Dark on light paint and light on dark, so it shows over any colour.
        /// </summary>
        public const int BrushRingSegments = 48;
        public const float BrushRingWidthMetres = 0.004f;
        public const float BrushRingLiftMetres = 0.004f;

        private LineRenderer _ring;

        public bool BrushRingVisible => _ring != null && _ring.enabled;

        /// <summary>Public so a test can place the ring without a mouse.</summary>
        public void ShowBrushRing(Vector2 screenPosition)
        {
            Vector3 at = Vector3.zero;
            int face = -1;
            Vector3 up = Vector3.up;
            bool show = PaintEnabled && Current == Tool.Brush && !_rotating && !InputBlocked && _paint != null && !OverInterface()
                        && Raycast(screenPosition, out at, out face)
                        && _paint.TryNormalOf(face, out up);
            if (!show)
            {
                if (_ring != null)
                {
                    _ring.enabled = false;
                }
                return;
            }
            LineRenderer ring = Ring();
            if (ring == null)
            {
                return;
            }

            Transform body = BodyCollider.transform;
            Vector3 centre = HoldingStill ? body.TransformPoint(at) : _hitNow;
            Vector3 worldUp = HoldingStill ? body.TransformDirection(up).normalized : _normalNow;
            float radius = BodyPaint.BrushRadiusMetres(BrushRadiusPixels) * body.lossyScale.x;
            Vector3 across = Vector3.Cross(worldUp, Mathf.Abs(worldUp.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 along = Vector3.Cross(worldUp, across);
            Vector3 lifted = centre + worldUp * BrushRingLiftMetres;
            for (int i = 0; i < BrushRingSegments; i++)
            {
                float angle = i * Mathf.PI * 2.0f / BrushRingSegments;
                ring.SetPosition(i, lifted + (across * Mathf.Cos(angle) + along * Mathf.Sin(angle)) * radius);
            }

            Color under = _paint.SampleAt(at, face);
            Color line = under.grayscale > 0.5f ? new Color(0.08f, 0.08f, 0.1f) : new Color(0.95f, 0.95f, 0.95f);
            ring.startColor = line;
            ring.endColor = line;
            ring.widthMultiplier = BrushRingWidthMetres * body.lossyScale.x;
            ring.enabled = true;
        }

        private LineRenderer Ring()
        {
            if (_ring != null)
            {
                return _ring;
            }
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }
            var holder = new GameObject("BrushRing");
            holder.transform.SetParent(transform, false);
            // On the body's own layer: the creator's camera sees nothing else (TitleScreenWiringTests.TheCreatorSeesNothingButItself).
            holder.layer = BodyCollider != null ? BodyCollider.gameObject.layer : gameObject.layer;
            _ring = holder.AddComponent<LineRenderer>();
            _ring.loop = true;
            _ring.useWorldSpace = true;
            _ring.positionCount = BrushRingSegments;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;
            _ring.sharedMaterial = new Material(shader) { name = "BrushRing", hideFlags = HideFlags.DontSave };
            _ring.enabled = false;
            return _ring;
        }

        /// <summary>Starts the fill at the hit (and its mirror's); it pours over the body a share a frame (Update).</summary>
        private void Fill(Vector3 local, int triangle)
        {
            _paint.BeginBucketFill(local, triangle, ActiveColour);
            if (!Mirroring)
            {
                return;
            }

            Vector3 mirrored = BodyPaint.Mirror(local);
            int face = _paint.FindTriangle(mirrored);
            if (face >= 0)
            {
                _paint.BeginBucketFill(mirrored, face, ActiveColour);
            }
        }

        /// <summary>
        /// Turns a screen position into a point and triangle on the body.
        ///
        /// Against the BODY'S COLLIDER SPECIFICALLY rather than whatever the
        /// ray hits first: the creator has a backdrop and UI behind it, and a
        /// stroke that lands on the floor would silently do nothing.
        /// </summary>
        private bool Raycast(Vector2 screenPosition, out Vector3 localPoint, out int triangle)
        {
            localPoint = Vector3.zero;
            triangle = -1;

            if (Camera == null || BodyCollider == null)
            {
                return false;
            }

            Ray ray = Camera.ScreenPointToRay(screenPosition);
            if (!HoldingStill)
            {
                return RaycastAsItStands(ray, out localPoint, out triangle);
            }

            bool onBody = BodyCollider.Raycast(ray, out RaycastHit body, RayLength);

            RaycastHit headHit = default;
            bool onHead = HeadCollider != null
                          && HeadCollider.Raycast(ray, out headHit, RayLength);

            if (!onBody && !onHead)
            {
                return false;
            }

            // WHICHEVER IS IN FRONT. The head and the body are two separate
            // meshes that overlap where the skull meets the shoulders, so a ray
            // through that region hits both, and taking either one by
            // preference paints the hidden surface behind the visible one.
            bool useHead = onHead && (!onBody || headHit.distance < body.distance);

            if (useHead)
            {
                // Into the body's space, because that is the space the head's
                // triangles were bound in -- see BodyPaint.Bind.
                localPoint = BodyCollider.transform.InverseTransformPoint(headHit.point);
                triangle = headHit.triangleIndex < 0
                    ? -1
                    : _paint.BodyTriangleCount + headHit.triangleIndex;
                return triangle >= 0;
            }

            localPoint = BodyCollider.transform.InverseTransformPoint(body.point);
            triangle = body.triangleIndex;
            return triangle >= 0;
        }

        public void Undo()
        {
            _paint.Undo();
        }

        public void Redo()
        {
            _paint.Redo();
        }

        /// <summary>
        /// Whether anything has been painted, so Back can leave without asking
        /// when there is nothing to lose.
        /// </summary>
        /// <summary>A stroke is being drawn: the button held down on the body.</summary>
        public bool IsPainting => _painting;

        public bool HasEdits => _paint != null && _paint.CanUndo;

        public bool CanUndo => _paint != null && _paint.CanUndo;

        public bool CanRedo => _paint != null && _paint.CanRedo;

        /// <summary>
        /// Godot's own VALID_HAIR_STYLES: "none" is always valid and is not a
        /// real asset; everything else names a mesh.
        /// </summary>
        public void SetHairStyle(string style)
        {
            if (Hair != null)
            {
                Hair.SetStyle(style);
            }
        }

        public string HairStyle => Hair != null ? Hair.Style : CharacterHair.None;

        /// <summary>Dyes the hair on the body being made. Kept while bald and worn by whichever style comes next.</summary>
        public void SetHairColour(Color colour)
        {
            if (Hair != null)
            {
                Hair.SetColour(colour);
            }
        }

        public Color HairColour => Hair != null ? Hair.Colour : CharacterHair.HairColour;
    }
}
