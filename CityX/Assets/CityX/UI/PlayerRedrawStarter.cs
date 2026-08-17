using CityX.Character;
using CityX.Core;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The Player submenu's ReDraw button: opens the in-world character
    /// creator on a clone of this body, seeded from the same CharacterSave
    /// PlaySession.Character already points at, and writes the edited
    /// paint/hair back to it on SAVE. See RedrawCreatorRuntime's own doc
    /// comment for why the creator itself is built at runtime rather than
    /// by CharacterCreatorBuilder (editor-only, cannot run during play).
    ///
    /// NEVER TOUCHES THE LIVE BODY DIRECTLY. Everything happens on a clone;
    /// the live body only changes once, on confirm, through the exact same
    /// CharacterIdentity.Apply(character) every other spawn/redraw path
    /// already uses -- so a cancelled session leaves nothing behind to
    /// clean up beyond destroying the clone itself.
    /// </summary>
    [RequireComponent(typeof(CharacterIdentity))]
    public sealed class PlayerRedrawStarter : MonoBehaviour
    {
        [Tooltip("Movement/combat input. Disabled while redrawing.")]
        public PlayerInputReader InputReader;

        [Tooltip("Mouse-look. Disabled while redrawing.")]
        public ThirdPersonCamera OrbitCamera;

        [Tooltip("embermane.obj -- wired once at edit time. A runtime script cannot load project assets by path the way CharacterCreatorBuilder's own AddHair does.")]
        public Mesh HairMesh;

        public Material HairMaterial;

        private CharacterIdentity _identity;
        private GameObject _overlay;
        private bool _wasCursorCaptured;

        public bool IsActive => _overlay != null;

        private void Awake()
        {
            _identity = GetComponent<CharacterIdentity>();
        }

        public void Begin()
        {
            if (IsActive)
            {
                return;
            }
            CharacterSave character = _identity != null ? _identity.Character : null;
            if (character == null)
            {
                // Play entered straight into the massing scene, skipping the
                // menu -- there is no save to redraw. The same "works with
                // no character at all" reasoning CharacterIdentity's own doc
                // comment gives for its Awake().
                return;
            }

            _wasCursorCaptured = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (InputReader != null) InputReader.enabled = false;
            if (OrbitCamera != null) OrbitCamera.enabled = false;

            _overlay = RedrawCreatorRuntime.Build(gameObject, HairMesh, HairMaterial, OnConfirm, OnCancel);
            Seed(character);
        }

        /// <summary>
        /// Paint/hair are read from CharacterSave, not off the live
        /// renderer -- the save is the single source of truth
        /// CharacterIdentity itself always writes to and reads from.
        /// </summary>
        private void Seed(CharacterSave character)
        {
            CreatorOverlays overlays = _overlay != null ? _overlay.GetComponent<CreatorOverlays>() : null;
            CharacterCreator creator = overlays != null ? overlays.Creator : null;
            if (creator == null || creator.Paint == null)
            {
                return;
            }

            byte[] paint = character.GetPaint();
            if (paint != null)
            {
                creator.Paint.Decode(paint);
            }
            else
            {
                // No paint saved (a legacy or Godot-imported character) --
                // start from their flat skin colour rather than the blank
                // white a fresh creator otherwise opens on, matching
                // CharacterIdentity.Apply's own Recolour fallback.
                creator.Paint.Clear(character.Skin);
            }
            creator.SetHairStyle(character.HairStyle);
        }

        private void OnConfirm(CharacterCreator creator)
        {
            CharacterSave character = _identity != null ? _identity.Character : null;
            if (character != null && creator != null)
            {
                character.SetPaint(creator.Paint.Encode());
                character.HairStyle = creator.HairStyle;
                _identity.Apply(character);
            }
            End();
        }

        private void OnCancel()
        {
            End();
        }

        private void End()
        {
            if (_overlay != null)
            {
                Destroy(_overlay);
                _overlay = null;
            }
            if (InputReader != null) InputReader.enabled = true;
            if (OrbitCamera != null) OrbitCamera.enabled = true;
            if (_wasCursorCaptured)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
