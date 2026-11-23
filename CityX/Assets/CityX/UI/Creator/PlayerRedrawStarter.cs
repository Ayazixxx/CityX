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

        [Tooltip("Every hairstyle after none, in CharacterHair.ValidStyles order -- wired once at edit time. A runtime script cannot load project assets by path the way CharacterCreatorBuilder's own AddHair does.")]
        public Mesh[] HairMeshes;

        public Material HairMaterial;

        private CharacterIdentity _identity;
        private GameObject _overlay;
        private readonly MenuFocus _focus = new MenuFocus();

        public bool IsActive => _overlay != null;

        /// <summary>The session open on this machine, if one is: the HUD steps aside for it (MenuHudFade).</summary>
        public static PlayerRedrawStarter Active { get; private set; }

        /// <summary>The body ReDraw repaints: the one this sits on, or the clone a joining player plays (LocalBodyBinder).</summary>
        public GameObject Body;

        private void Awake()
        {
            if (Body == null)
            {
                Body = gameObject;
            }
        }

        public void Begin()
        {
            if (IsActive)
            {
                return;
            }
            _identity = Body != null ? Body.GetComponent<CharacterIdentity>() : null;
            CharacterSave character = _identity != null ? _identity.Character : null;
            if (character == null)
            {
                // Play entered straight into the massing scene, skipping the
                // menu -- there is no save to redraw. The same "works with
                // no character at all" reasoning CharacterIdentity's own doc
                // comment gives for its Awake().
                return;
            }

            _focus.Take(this, InputReader, OrbitCamera);

            _overlay = RedrawCreatorRuntime.Build(Body, HairMeshes, HairMaterial, OnConfirm, OnCancel);
            Active = this;
            RedrawCreatorRuntime.Seed(_overlay, character);
        }

        private void OnConfirm(CharacterCreator creator)
        {
            CharacterSave character = _identity != null ? _identity.Character : null;
            if (character != null && creator != null)
            {
                character.SetPaint(creator.Paint.Encode());
                character.HairStyle = creator.HairStyle;
                character.HairColour = creator.HairColour;
                character.Beard = creator.Beard;
                // The look alone, and saved at once. Apply would also put the save's level, points and skills back on the body (as old as
                // the last autosave) and refill its bars: a redraw in a fight was a full heal, and undid what had been earned since.
                _identity.ApplyLook(character);
                _identity.Write();
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
            if (Active == this)
            {
                Active = null;
            }
            _focus.GiveBack(this, InputReader, OrbitCamera);
        }
    }
}
