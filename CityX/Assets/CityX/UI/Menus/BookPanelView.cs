using CityX.Character;
using CityX.View;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The on-screen page a Book (CityX.View) opens on E -- Requested:
    /// "add books and when you press E on them it opens a page on screen
    /// with text ive prepared but just make the system for now." Implements
    /// CityX.View.IBookPanel rather than Book holding a direct reference,
    /// since CityX.View cannot depend on CityX.UI (see IBookPanel's own doc
    /// comment) -- Book finds this at runtime the same registry-free way
    /// OldGuy finds a door.
    ///
    /// ONE SHARED INSTANCE, RE-TITLED PER BOOK, same shape as
    /// PlaceholderPanelView covering Quests/Social/Skills with one panel --
    /// this file is in fact PlaceholderPanelView's own structure almost
    /// verbatim (Root/Title/Body/Close, Escape-to-close, cursor capture and
    /// PlayerInputReader/ThirdPersonCamera disable-then-restore), just
    /// sized and laid out for a page of body text rather than one short
    /// placeholder line.
    /// </summary>
    public sealed class BookPanelView : MonoBehaviour, IBookPanel
    {
        [Tooltip("The whole overlay, hidden until Show() is called.")]
        public GameObject Root;

        public Text TitleLabel;
        public Text BodyLabel;
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
            if (keyboard != null && GameControls.Now.Cancel.Pressed)
            {
                Close();
            }
        }

        public void Show(string title, string body)
        {
            if (TitleLabel != null) TitleLabel.text = title;
            if (BodyLabel != null) BodyLabel.text = body;

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
