using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.View
{
    /// <summary>
    /// The shared reading panel, implemented on the UI side (BookPanelView,
    /// CityX.UI) -- lives here, in CityX.View, and not on the panel itself,
    /// for the same reason IOpenableDoor/IDialogueBox live on the low side
    /// of their own dependency: CityX.UI already references CityX.View, so
    /// CityX.View can never reference a concrete CityX.UI type back. Book
    /// finds the one live implementation at real runtime (see FindPanel),
    /// the same registry-free FindObjectsByType-then-filter idiom
    /// OldGuy.FindDoorNear already established for IOpenableDoor.
    /// </summary>
    public interface IBookPanel
    {
        void Show(string title, string body);
    }

    /// <summary>
    /// A book you can walk up to and press E to read. EXPLICIT ASK: "add
    /// books and when you press E on them it opens a page on screen with
    /// text ive prepared but just make the system for now" -- this is that
    /// system. Title/Body are plain public fields rather than a registry or
    /// ScriptableObject, so wiring in the user's own real text later is a
    /// one-line assignment per spawned book; DefaultTitle/DefaultBody below
    /// are an obvious placeholder, not invented "prepared" content.
    ///
    /// OWNS ITS OWN PROXIMITY, same shape as BenchSeat/Radio/BuildingDoor --
    /// see Radio's own doc comment on why there is no shared "nearby
    /// interactable" registry in this project.
    ///
    /// UNLIKE BENCHSEAT, reading a book never touches the CharacterController
    /// or PlayerLocomotion at all -- the panel itself (BookPanelView.Show,
    /// mirroring PlaceholderPanelView) is what disables PlayerInputReader/
    /// the orbit camera and frees the cursor, and closes itself on Escape.
    /// Book's own job ends the instant it calls Show.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Book : MonoBehaviour
    {
        public const float InteractRange = 2.0f;

        public const float StandWidth = 0.34f;
        public const float StandDepth = 0.28f;
        public const float StandHeight = 0.9f;
        public const float StandLegThickness = 0.06f;
        public const float CoverWidth = 0.30f;
        public const float CoverDepth = 0.22f;
        public const float CoverThickness = 0.04f;
        public const float ReadingTiltDegrees = 35.0f;

        public const string DefaultTitle = "An Old Book";

        public const string DefaultBody =
            "Placeholder text -- the real page hasn't been written yet.\n\n" +
            "This is where the book's own prepared writing goes once it's " +
            "supplied; for now this proves the system: walk up, press E, " +
            "read, press Escape to close.";

        [Tooltip("Shown as the panel's own title. Defaults to a placeholder -- set per-instance once real text exists.")]
        public string Title = DefaultTitle;

        [TextArea(3, 12)]
        [Tooltip("Shown as the panel's own body text. Defaults to a placeholder -- set per-instance once real text exists.")]
        public string Body = DefaultBody;

        private PlayerInputReader _nearby;
        private IBookPanel _panel;

        /// <summary>For tests, same purpose as BenchSeat's own identically-shaped property.</summary>
        public bool HasNearbyPlayer => _nearby != null;

        /// <summary>
        /// Builds the visible book -- a small reading stand (matches a
        /// lectern more than a bench, so it reads as "a thing to read"
        /// rather than "a thing to sit on") with a closed book propped at a
        /// legible angle on top -- and the interact trigger. Local space:
        /// the stand's own base sits at local origin, +Z is the direction a
        /// reader stands to look at it.
        /// </summary>
        public void Build(Material material)
        {
            GameObject standTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            standTop.name = "StandTop";
            standTop.transform.SetParent(transform, false);
            standTop.transform.localPosition = new Vector3(0.0f, StandHeight, 0.0f);
            standTop.transform.localScale = new Vector3(StandWidth, StandLegThickness, StandDepth);
            if (material != null)
            {
                standTop.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Leg";
                leg.transform.SetParent(transform, false);
                leg.transform.localPosition = new Vector3(
                    side * (StandWidth * 0.5f - StandLegThickness * 0.5f), StandHeight * 0.5f, 0.0f);
                leg.transform.localScale = new Vector3(StandLegThickness, StandHeight, StandLegThickness);
                if (material != null)
                {
                    leg.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
            }

            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "Cover";
            cover.transform.SetParent(transform, false);
            cover.transform.localPosition = new Vector3(
                0.0f, StandHeight + StandLegThickness * 0.5f + CoverThickness * 0.4f, 0.0f);
            cover.transform.localRotation = Quaternion.Euler(ReadingTiltDegrees, 0.0f, 0.0f);
            cover.transform.localScale = new Vector3(CoverWidth, CoverThickness, CoverDepth);
            if (material != null)
            {
                cover.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            SphereCollider trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = InteractRange;
            trigger.center = new Vector3(0.0f, StandHeight, 0.0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            var reader = other.GetComponentInParent<PlayerInputReader>();
            if (reader != null)
            {
                _nearby = reader;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (_nearby != null && other.transform.IsChildOf(_nearby.transform))
            {
                _nearby = null;
            }
        }

        private void Update()
        {
            // Same staleness guard BenchSeat/Radio already use: a collider
            // disabled while still inside the trigger never fires
            // OnTriggerExit (e.g. Ragdoll.Activate turning off the
            // CharacterController on death while merely standing near a book).
            if (_nearby != null &&
                Vector3.Distance(_nearby.transform.position, transform.position) > InteractRange)
            {
                _nearby = null;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _nearby == null || !keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }

            IBookPanel panel = FindPanel();
            panel?.Show(Title, Body);
        }

        /// <summary>
        /// One scene-wide reading panel, found the first time any book is
        /// read and cached from then on -- same registry-free
        /// FindObjectsByType-then-filter idiom OldGuy.FindDoorNear already
        /// established for IOpenableDoor, since CityX.View has no reference
        /// to CityX.UI's concrete BookPanelView type to hold directly.
        /// </summary>
        private IBookPanel FindPanel()
        {
            if (_panel != null)
            {
                return _panel;
            }

            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is IBookPanel panel)
                {
                    _panel = panel;
                    break;
                }
            }
            return _panel;
        }
    }
}
