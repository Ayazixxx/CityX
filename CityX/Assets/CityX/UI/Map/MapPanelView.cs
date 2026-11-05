using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CityX.UI
{
    /// <summary>
    /// The bird's-eye city map, toggled with M. Ported from ui/map_panel.gd.
    ///
    /// NO TREE PAUSE, matching Godot's own stated design: unlike PauseMenuView
    /// this does not touch Time.timeScale, it only takes the controls away.
    /// Godot does that through player.gd's own set_ui_capturing counter; this
    /// port has no such counter yet and only one thing needs to borrow the
    /// controls so far, so this simply disables the two components that read
    /// them -- PlayerInputReader and ThirdPersonCamera -- for exactly as long
    /// as the map is open. A second overlay that needs the same thing is the
    /// point at which that should become a real shared gate instead.
    /// </summary>
    public sealed class MapPanelView : MonoBehaviour
    {
        [Tooltip("The whole overlay, hidden until M is pressed.")]
        public GameObject Root;

        public MapCanvas Canvas;

        [Tooltip("The corner minimap: hidden while the full map is open.")]
        public GameObject MiniMap;

        [Tooltip("Movement/combat input. Disabled while the map is open.")]
        public PlayerInputReader InputReader;

        [Tooltip("Mouse-look. Disabled while the map is open -- otherwise dragging the map also spins the camera behind it.")]
        public ThirdPersonCamera OrbitCamera;

        [Tooltip("Escape is ignored while this is open -- typing a command should not also open the map.")]
        public DevConsoleView Console;

        [Tooltip("M is ignored while the game is paused.")]
        public PauseMenuView Pause;

        [Tooltip("M is ignored while the C-menu is open -- opening a full panel on top of its own camera cut would strand its input/camera-disable state on close.")]
        public PlayerMenuView Menu;

        [Tooltip("M is ignored while chat is open -- typing a message with the letter M in it should not also open the map.")]
        public PlayerChat Chat;

        private bool _wasCursorCaptured;

        public bool IsOpen => Root != null && Root.activeSelf;

        /// <summary>
        /// M CYCLES THREE WAYS (owner, 2026-10-11: "regularly there is no map on screen but if you press M it opens the full map and if you press M
        /// again it turns into a minimap and if you press M again it goes away"): nothing, then the full map, then the corner minimap, then
        /// nothing again.
        /// </summary>
        public enum Mode
        {
            Hidden,
            Full,
            Mini,
        }

        public Mode Current { get; private set; } = Mode.Hidden;

        public bool MiniMapShowing => MiniMap != null && MiniMap.activeSelf;

        private void Awake()
        {
            Close();
            ShowMiniMap(false);
            Current = Mode.Hidden;
        }

        /// <summary>One press of M: nothing -> full map -> minimap -> nothing.</summary>
        public void Cycle()
        {
            switch (Current)
            {
                case Mode.Hidden:
                    Open();
                    break;
                case Mode.Full:
                    Close();
                    ShowMiniMap(true);
                    Current = Mode.Mini;
                    break;
                default:
                    ShowMiniMap(false);
                    Current = Mode.Hidden;
                    break;
            }
        }

        private void ShowMiniMap(bool show)
        {
            if (MiniMap != null) MiniMap.SetActive(show);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if ((Console != null && Console.IsOpen) || (Pause != null && Pause.IsOpen) ||
                (Menu != null && Menu.IsOpen) || (Chat != null && Chat.IsOpen))
            {
                return;
            }

            if (keyboard.mKey.wasPressedThisFrame)
            {
                Cycle();
                return;
            }

            // Redrawn every frame while open so the player's own marker
            // tracks their position and facing -- input is disabled while
            // open, but anything else that can still move them (a car they
            // are seated in, say) should still be reflected.
            if (IsOpen && Canvas != null)
            {
                Canvas.SetVerticesDirty();
            }
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }
            _wasCursorCaptured = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (InputReader != null) InputReader.Hold(this);
            if (OrbitCamera != null) OrbitCamera.enabled = false;

            // Recentred on the player every time it opens, so the map can
            // never come up showing empty space panned off to last time and
            // left there. The zoom is kept: that reads as a preference, a
            // stale pan reads as a bug.
            Canvas?.ResetView();

            if (Root != null) Root.SetActive(true);
            ShowMiniMap(false);
            Current = Mode.Full;
            Canvas?.SetVerticesDirty();
        }

        /// <summary>Closes the full map and gives the controls back. Leaves nothing on screen (M's cycle shows the minimap itself).</summary>
        public void Close()
        {
            if (Root != null) Root.SetActive(false);
            if (Current == Mode.Full)
            {
                Current = Mode.Hidden;
            }

            if (InputReader != null) InputReader.Release(this);
            if (OrbitCamera != null) OrbitCamera.enabled = true;

            if (_wasCursorCaptured)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
