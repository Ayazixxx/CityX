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
    public sealed class CharacterCreator : MonoBehaviour
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

        [Tooltip("The hairstyle on the body being made. Set by the Hair buttons.")]
        public CharacterHair Hair;

        [Header("Tools")]
        public Tool Current = Tool.Brush;

        public Color Colour = new Color(0.2f, 0.2f, 0.25f);

        public bool Erasing;

        public bool Mirroring;

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

        public const float MinimumCameraDistance = 1.5f;

        public const float MaximumCameraDistance = 8.0f;

        public const float ZoomStep = 0.35f;

        private BodyPaint _paint;
        private bool _painting;
        private bool _rotating;
        private float _yaw;
        private float _pitch;
        private Vector2 _lastPaintScreenPosition;

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
                _paint.Bind(BodyRenderer.sharedMesh);
                ApplyToRenderer();
            }
        }

        /// <summary>Points the body's material at the paint texture.</summary>
        public void ApplyToRenderer()
        {
            if (BodyRenderer == null)
            {
                return;
            }
            Material instance = BodyRenderer.material;
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

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Camera == null || InputBlocked)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                Zoom(scroll > 0.0f ? -ZoomStep : ZoomStep);
            }

            Vector2 position = mouse.position.ReadValue();

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
                // Except the one a click is tested against.
                if (collider != BodyCollider)
                {
                    Remove(collider);
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

        public void Zoom(float step)
        {
            if (Camera == null)
            {
                return;
            }
            Vector3 local = Camera.transform.localPosition;
            local.z = Mathf.Clamp(
                local.z + (local.z < 0.0f ? -step : step),
                -MaximumCameraDistance, MaximumCameraDistance);

            // Held off the body by at least the near limit whichever side it
            // sits on, so zooming in cannot end up inside the head.
            if (Mathf.Abs(local.z) < MinimumCameraDistance)
            {
                local.z = Mathf.Sign(local.z == 0.0f ? -1.0f : local.z) * MinimumCameraDistance;
            }
            Camera.transform.localPosition = local;
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
                _paint.Apply();
                return;
            }

            _painting = true;
            _lastPaintScreenPosition = screenPosition;
            Stamp(local, triangle);
            _paint.Apply();
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
        /// Stamps at even steps along the screen path since the last event,
        /// rather than only where the mouse now is.
        /// </summary>
        private void PaintAlong(Vector2 screenPosition)
        {
            float distance = Vector2.Distance(_lastPaintScreenPosition, screenPosition);
            int steps = Mathf.Max(1, Mathf.FloorToInt(distance / PaintStepPixels));

            for (int i = 1; i <= steps; i++)
            {
                Vector2 at = Vector2.Lerp(_lastPaintScreenPosition, screenPosition, i / (float)steps);
                if (Raycast(at, out Vector3 local, out int triangle))
                {
                    Stamp(local, triangle);
                }
            }

            _lastPaintScreenPosition = screenPosition;
            _paint.Apply();
        }

        private Color ActiveColour => Erasing ? EraseColour : Colour;

        private void Stamp(Vector3 local, int triangle)
        {
            _paint.PaintAt(local, triangle, ActiveColour, BrushRadiusPixels);
            if (!Mirroring)
            {
                return;
            }

            Vector3 mirrored = BodyPaint.Mirror(local);
            int face = _paint.FindTriangle(mirrored);
            if (face >= 0)
            {
                _paint.PaintAt(mirrored, face, ActiveColour, BrushRadiusPixels);
            }
        }

        private void Fill(Vector3 local, int triangle)
        {
            _paint.BucketFillAt(local, triangle, ActiveColour);
            if (!Mirroring)
            {
                return;
            }

            Vector3 mirrored = BodyPaint.Mirror(local);
            int face = _paint.FindTriangle(mirrored);
            if (face >= 0)
            {
                _paint.BucketFillAt(mirrored, face, ActiveColour);
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
            if (!BodyCollider.Raycast(ray, out RaycastHit hit, RayLength))
            {
                return false;
            }

            localPoint = BodyCollider.transform.InverseTransformPoint(hit.point);
            triangle = hit.triangleIndex;
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
    }
}
