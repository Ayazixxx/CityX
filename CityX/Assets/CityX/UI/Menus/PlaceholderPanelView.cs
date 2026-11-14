using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// A small "Coming soon" overlay, shared by every C-menu destination
    /// with no system behind it yet (Quests, Social) -- and by Skills,
    /// whose real skill tree exists in the Godot source but was explicitly
    /// scoped out of this pass (see PlayerSubmenuView's own doc comment).
    ///
    /// ONE INSTANCE, RE-TITLED PER USE rather than a separate panel per
    /// destination -- matches the Godot original's own precedent for an
    /// unbuilt tab (InventoryPanelView's doc comment: "Skills was already
    /// just a 'Coming soon' placeholder even in the Godot original").
    ///
    /// SAME OPEN/CLOSE SHAPE AS EVERY OTHER PANEL. Unlike PlayerMenuView it
    /// does not touch the camera -- whatever called Show() already closed
    /// the C-menu first, so ThirdPersonCamera resumes exactly where it was
    /// before the cut, and this only needs to disable input while it is up.
    /// </summary>
    public sealed class PlaceholderPanelView : MonoBehaviour
    {
        [Tooltip("The whole overlay, hidden until Show() is called.")]
        public GameObject Root;

        public Text TitleLabel;
        public Text MessageLabel;
        public Button CloseButton;

        public PlayerInputReader InputReader;
        public ThirdPersonCamera OrbitCamera;

        private readonly MenuFocus _focus = new MenuFocus();
        private bool _opening;

        public bool IsOpen => Root != null && Root.activeSelf;

        private void Awake()
        {
            if (CloseButton != null) CloseButton.onClick.AddListener(Close);
            // The page wakes the first time it is shown (it sits on its own hidden object): closing it then would undo that very Show.
            if (!_opening)
            {
                Close();
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
        }

        public void Show(string title, string message)
        {
            if (TitleLabel != null) TitleLabel.text = title;
            if (MessageLabel != null) MessageLabel.text = message;

            if (IsOpen)
            {
                return;
            }
            _focus.Take(this, InputReader, OrbitCamera);

            _opening = true;
            if (Root != null) Root.SetActive(true);
            _opening = false;
        }

        public void Close()
        {
            if (Root != null) Root.SetActive(false);

            _focus.GiveBack(this, InputReader, OrbitCamera);
        }
    }
}
