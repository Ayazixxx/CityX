using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.Character
{
    /// <summary>
    /// LOOKING AT AN ITEM AND INSPECTING IT (docs/ITEM_INSPECT.md; the owner: "like skyrim when you are hovering over an item and you can either
    /// press E to pickup, or F to inspect"). Each frame this finds what this machine's player is looking at in reach -- a dropped item
    /// (<see cref="Hovered"/>, DroppedItemPicker's rule) or a drawer or cupboard (<see cref="HoveredContainer"/>, LootSearcher's) -- for the
    /// prompt under the crosshair (UI's ItemPromptView). F on an item opens its appraisal card beside it (UI's ItemCardView reads
    /// <see cref="Shot"/>) and glides the camera in to it over <see cref="MoveSeconds"/> (InspectShot), never a cut; while it is open the
    /// controls are the card's: E takes the item, F or Tab closes it, and the camera glides back. It acts only for the body this machine's
    /// player drives, as the pickup and the loot window do.
    /// </summary>
    public sealed class ItemInspector : MonoBehaviour
    {
        /// <summary>How long the camera takes to glide in to the card, and back out.</summary>
        public const float MoveSeconds = 0.5f;

        /// <summary>The camera's own size when it is pulled in from a wall behind its place at the card.</summary>
        public const float WallRadius = 0.25f;

        /// <summary>The item being inspected (null when none; a Unity null once it is taken by someone else while the card is open).</summary>
        public DroppedItem Current { get; private set; }

        /// <summary>The inspected item's id and the id of what the player holds, kept for the card after the item itself is gone.</summary>
        public string ItemId { get; private set; }

        public string HeldItemId { get; private set; }

        /// <summary>The card is up: opening, open, or gliding closed.</summary>
        public bool IsOpen { get; private set; }

        public bool Closing { get; private set; }

        /// <summary>How far along the glide is, 0 at the camera's own place to 1 at the card; <see cref="Eased"/> is the smoothed share.</summary>
        public float Progress { get; private set; }

        public float Eased => InspectShot.Ease(Progress);

        public InspectShot Shot { get; private set; }

        /// <summary>What the player is looking at in reach this frame, when the card is closed: an item to take or inspect, or a container to search.</summary>
        public DroppedItem Hovered { get; private set; }

        public LootContainer HoveredContainer { get; private set; }

        /// <summary>Or a thing that names itself and is used with E (HoverTarget: a lever), when it is the most directly looked at.</summary>
        public HoverTarget HoveredTarget { get; private set; }

        /// <summary>F would open a card now: whatever else F does (a flashlight's switch) stands aside.</summary>
        public bool ClaimsF => !IsOpen && Hovered != null;

        public Transform CameraTransform => _camera != null ? _camera.transform : null;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private PlayerInputReader _reader;
        private DroppedItemPicker _picker;
        private LootSearcher _searcher;
        private PlayerVitals _vitals;
        private Inventory _inventory;
        private ThirdPersonCamera _camera;
        private bool _cameraWasEnabled;
        private Vector3 _fromPosition;
        private Quaternion _fromRotation;

        private void Awake()
        {
            _reader = GetComponent<PlayerInputReader>();
            _picker = GetComponent<DroppedItemPicker>();
            _searcher = GetComponent<LootSearcher>();
            _vitals = GetComponent<PlayerVitals>();
            _inventory = GetComponent<Inventory>();
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                Finish();
            }
        }

        private void Update()
        {
            if (IsOpen)
            {
                Tick();
                return;
            }
            Hovered = null;
            HoveredContainer = null;
            HoveredTarget = null;
            if (_reader == null || !_reader.TakesKeys || _picker == null)
            {
                return;
            }
            Look();
            Keyboard keyboard = Keyboard.current;
            if (Hovered != null && keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                Open(Hovered);
            }
            else if (HoveredTarget != null && InteractKey.Tapped)
            {
                HoveredTarget.Use(gameObject);
            }
        }

        /// <summary>
        /// The item, the container or the other thing looked at most directly in reach: the same tie as DroppedItemPicker's, the item winning
        /// a draw with a container, and either winning a draw with a lever (the pickup and the searcher act on E by themselves and yield to a
        /// lever looked at more directly, as this acts for it).
        /// </summary>
        private void Look()
        {
            DroppedItem item = _picker.InReach(out float itemDot);
            LootContainer container = null;
            float containerDot = float.MinValue;
            if (_searcher != null && _searcher.Current == null)
            {
                container = _searcher.InReach(out containerDot);
            }
            HoverTarget target = HoverTarget.Best(transform, out float targetDot);
            float best = Mathf.Max(item != null ? itemDot : float.MinValue, container != null ? containerDot : float.MinValue);
            if (target != null && targetDot > best)
            {
                HoveredTarget = target;
            }
            else if (item != null && (container == null || itemDot >= containerDot))
            {
                Hovered = item;
            }
            else
            {
                HoveredContainer = container;
            }
        }

        /// <summary>Opens <paramref name="item"/>'s card and starts the camera's glide. Public so a test can open one without a keyboard.</summary>
        public void Open(DroppedItem item)
        {
            if (item == null || IsOpen)
            {
                return;
            }
            _camera = null;
            foreach (ThirdPersonCamera camera in FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None))
            {
                if (camera.Target == transform) _camera = camera;
            }
            if (_inventory == null) _inventory = GetComponent<Inventory>();   // the scene's player is given its inventory after it wakes
            Current = item;
            ItemId = item.ItemId;
            HeldItemId = _inventory != null ? _inventory.EquippedItemId(Inventory.PrimarySlot) : string.Empty;
            IsOpen = true;
            Closing = false;
            Progress = 0.0f;
            if (_reader != null) _reader.Hold(this);
            if (_camera != null)
            {
                _cameraWasEnabled = _camera.enabled;
                _camera.enabled = false;
                _fromPosition = _camera.transform.position;
                _fromRotation = _camera.transform.rotation;
            }
            else
            {
                _fromPosition = transform.position + Vector3.up * 2.0f - transform.forward * 3.0f;
                _fromRotation = transform.rotation;
            }
            Shot = ShotFor(item);
        }

        private static readonly Collider[] Overlaps = new Collider[8];

        /// <summary>
        /// The shot of the item: the card on the viewer's right unless a wall is there, then on the left, else over the item (the owner,
        /// 2026-10-31: "the inspect thing is going through walls at certain angles"); and the camera pulled in when a wall stands between the
        /// item and its place.
        /// </summary>
        private InspectShot ShotFor(DroppedItem item)
        {
            InspectShot shot = InspectShot.For(item.Centre, _fromPosition);
            foreach (CardSide side in new[] { CardSide.Right, CardSide.Left, CardSide.Over })
            {
                shot = InspectShot.For(item.Centre, _fromPosition, InspectShot.Distance, side);
                if (!CardMeetsAWall(shot, item))
                {
                    break;
                }
            }
            Vector3 back = shot.CameraPosition - shot.Focus;
            float distance = back.magnitude;
            int count = Physics.SphereCastNonAlloc(shot.Focus, WallRadius, back / distance, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            for (int i = 0; i < count; i++)
            {
                Transform hit = Hits[i].collider.transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(item.transform))
                {
                    continue;
                }
                nearest = Mathf.Min(nearest, Hits[i].distance);
            }
            return nearest < distance ? InspectShot.For(item.Centre, _fromPosition, nearest - 0.1f, shot.Side) : shot;
        }

        /// <summary>Whether anything solid but the item itself or this body stands where the card would (a wall, a cupboard, a window).</summary>
        private bool CardMeetsAWall(InspectShot shot, DroppedItem item)
        {
            int count = Physics.OverlapBoxNonAlloc(shot.CardPosition, InspectShot.CardHalfExtents, Overlaps, shot.CardRotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform hit = Overlaps[i].transform;
                if (!hit.IsChildOf(transform) && !hit.IsChildOf(item.transform))
                {
                    return true;
                }
            }
            return false;
        }

        private void Tick()
        {
            float step = Time.deltaTime / MoveSeconds;
            Progress = Closing ? Mathf.Max(0.0f, Progress - step) : Mathf.Min(1.0f, Progress + step);
            Place();
            if (Closing)
            {
                if (Progress <= 0.0f)
                {
                    Finish();
                }
                return;
            }
            if (Current == null || (_vitals != null && _vitals.IsDead))
            {
                Close();
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.fKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame))
            {
                Close();
                return;
            }
            if (InteractKey.Tapped)
            {
                Take();
            }
        }

        private void Place()
        {
            if (_camera == null)
            {
                return;
            }
            InspectShot.Blend(_fromPosition, _fromRotation, Shot.CameraPosition, Shot.CameraRotation, Progress, out Vector3 position, out Quaternion rotation);
            _camera.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>Takes the inspected item into the inventory (E), and the card closes.</summary>
        public bool Take()
        {
            if (!IsOpen || Closing || Current == null || _inventory == null || !_picker.Record.Take(Current.Id, _inventory))
            {
                return false;
            }
            Close();
            return true;
        }

        /// <summary>Starts the card's close: it shrinks away and the camera glides back to where it was.</summary>
        public void Close()
        {
            if (IsOpen)
            {
                Closing = true;
            }
        }

        private void Finish()
        {
            IsOpen = false;
            Closing = false;
            Current = null;
            Progress = 0.0f;
            if (_reader != null) _reader.Release(this);
            if (_camera != null) _camera.enabled = _cameraWasEnabled;
        }
    }
}
