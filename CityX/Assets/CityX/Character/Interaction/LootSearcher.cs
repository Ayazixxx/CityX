using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// LOOKING INTO A CONTAINER (docs/LOOTING.md): a tap of E on the drawer, cupboard or lid the player looks at opens it and shows what is inside
    /// (the loot window, UI's LootWindowView, reads <see cref="Current"/> and <see cref="List"/>). While it is shown the controls are the window's,
    /// as Skyrim's container menu takes them: W and S (or the arrows) pick, E takes, R takes everything, Tab or Space closes. An item lying in the
    /// open that is looked at more directly than a container is the pickup's to take (DroppedItemPicker), and the other way round. It acts only for
    /// the body this machine's player drives.
    /// </summary>
    public sealed class LootSearcher : MonoBehaviour
    {
        /// <summary>From the feet: an upper cabinet's door is well over head height on the 2.6 m body.</summary>
        public const float ReachMetres = 3.2f;

        /// <summary>The look cone's cosine: a container is a big target, so a narrower cone than a pickup's.</summary>
        public const float LookDot = 0.8f;

        public LootContainer Current { get; private set; }

        public LootList List { get; private set; }

        private PlayerInputReader _reader;
        private ThirdPersonCamera _camera;
        private bool _cameraWasEnabled;
        private CursorLockMode _cursorWas;

        private void Awake()
        {
            _reader = GetComponent<PlayerInputReader>();
        }

        private void OnDisable()
        {
            CloseWindow();
        }

        private void Update()
        {
            if (Current != null)
            {
                TickWindow();
                return;
            }
            if (_reader == null || !_reader.TakesKeys || !InteractKey.Tapped)
            {
                return;
            }
            LootContainer container = InReach(out float dot);
            var picker = GetComponent<DroppedItemPicker>();
            if (container != null && (picker == null || picker.InReach(out float itemDot) == null || itemDot < dot)
                && (HoverTarget.Best(transform, out float targetDot) == null || targetDot <= dot))   // a lever looked at more directly is E's (ItemInspector)
            {
                Search(container);
            }
        }

        /// <summary>The container the player would open now (the one looked at most directly in reach and in sight), or null, with how directly.</summary>
        public LootContainer InReach(out float bestDot)
        {
            LootContainer best = null;
            bestDot = float.MinValue;
            foreach (LootContainer container in LootContainer.All)
            {
                if (container != null && InteractionReach.CanReach(container.Centre, transform, ReachMetres, LookDot, out float dot) && dot > bestDot && InSight(container))
                {
                    best = container;
                    bestDot = dot;
                }
            }
            return best;
        }

        /// <summary>Eye height on the body, as a share of its standing height.</summary>
        public const float EyeShare = 0.85f;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        /// <summary>
        /// Nothing but the container itself between the body's eyes and its front: a drawer on the far side of a wall is not in reach because the
        /// camera happens to look its way. From the eyes, not the camera, which sits behind the body.
        /// </summary>
        private bool InSight(LootContainer container)
        {
            var locomotion = GetComponent<PlayerLocomotion>();
            Vector3 eyes = transform.position + Vector3.up * ((locomotion != null ? locomotion.StandingHeight : 2.6f) * EyeShare);
            Vector3 toFront = container.Centre - eyes;
            float distance = toFront.magnitude;
            if (distance < 1e-3f)
            {
                return true;
            }
            int count = Physics.RaycastNonAlloc(eyes, toFront / distance, Hits, distance + 0.05f, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Collider first = null;
            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i].collider;
                if (hit != null && !hit.transform.IsChildOf(transform) && Hits[i].distance < nearest)
                {
                    nearest = Hits[i].distance;
                    first = hit;
                }
            }
            return first == null || first.transform.IsChildOf(container.transform);
        }

        /// <summary>Opens <paramref name="container"/> and shows what is in it, taking the controls for the window. Public so a test can open one.</summary>
        public void Search(LootContainer container)
        {
            CloseWindow();
            container.Open();
            Current = container;
            List = new LootList(container.Items);
            if (_reader != null) _reader.Hold(this);
            _camera = null;
            foreach (ThirdPersonCamera camera in FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None))
            {
                if (camera.Target == transform) _camera = camera;
            }
            _cameraWasEnabled = _camera != null && _camera.enabled;
            if (_camera != null) _camera.enabled = false;
            _cursorWas = Cursor.lockState;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Closes the window (the container stays open, searched) and gives the controls back.</summary>
        public void CloseWindow()
        {
            if (Current == null)
            {
                return;
            }
            Current = null;
            List = null;
            if (_reader != null) _reader.Release(this);
            if (_camera != null) _camera.enabled = _cameraWasEnabled;
            Cursor.lockState = _cursorWas;
            Cursor.visible = _cursorWas != CursorLockMode.Locked;
        }

        public bool TakeSelected() => List != null && List.TakeSelected(Give);

        public int TakeAll() => List != null ? List.TakeAll(Give) : 0;

        /// <summary>Takes the item at <paramref name="index"/> (a click on its row).</summary>
        public bool TakeAt(int index)
        {
            if (List == null || index < 0 || index >= List.Items.Count)
            {
                return false;
            }
            List.Move(index - List.Selected);
            return TakeSelected();
        }

        private bool Give(string itemId)
        {
            var inventory = GetComponent<Inventory>();
            CorpseLoot body = Current != null ? Current.GetComponentInParent<CorpseLoot>() : null;
            bool given = body != null ? body.Give(itemId, inventory) : inventory != null && inventory.CanPickUp(itemId) && inventory.PickUp(itemId);
            if (given)
            {
                Current.NotifyTaken(itemId);
            }
            return given;
        }

        private void TickWindow()
        {
            if (Current == null || !Current.isActiveAndEnabled || (Current.Centre - transform.position).sqrMagnitude > ReachMetres * ReachMetres * 2.0f)
            {
                CloseWindow();
                return;
            }
            GameControls controls = GameControls.Now;
            if (controls.Forward.Pressed || controls.NavUp.Pressed) List.Move(-1);
            if (controls.Back.Pressed || controls.NavDown.Pressed) List.Move(1);
            if (controls.Reload.Pressed) TakeAll();   // R, or the controller's X held
            if (controls.LockOn.Pressed || controls.Jump.Pressed || controls.PadCancel.Pressed)
            {
                CloseWindow();
                return;
            }
            if (InteractKey.Tapped)
            {
                if (List.IsEmpty)
                {
                    CloseWindow();
                }
                else
                {
                    TakeSelected();
                }
            }
        }
    }
}
