using CityX.Character;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The two things that can sit on top of the creator: the name prompt, and
    /// the "you painted something, are you sure" check on the way out.
    /// Ported from character_creator.gd.
    ///
    /// ENTER THE CITY DOES NOT FINISH CREATION DIRECTLY. It opens the name
    /// prompt, and the character is only made once a name has actually been
    /// confirmed -- which is also the only place the paint is handed over, so
    /// backing out of the prompt cannot leave a half-made character behind.
    ///
    /// Nothing is ever auto-saved from the creator, so leaving after painting
    /// would silently throw the work away. Having painted at all is what gates
    /// the confirmation -- CanUndo is true once any edit has landed, which is
    /// the same thing as being dirty without a second flag to keep in step.
    /// </summary>
    public sealed class CreatorOverlays : MonoBehaviour
    {
        public CharacterCreator Creator;

        public TitleScreen Title;

        public GameObject NamePanel;

        public InputField NameField;

        public GameObject ConfirmPanel;

        [Tooltip("Set instead of Title/NamePanel by ReDraw -- see RedrawCreatorRuntime. Fired by the SAVE button.")]
        public System.Action<CharacterCreator> OnConfirmRedraw;

        [Tooltip("Fired instead of Title.Show(CharacterSelect) by Discard() when Title is null -- i.e. when this overlay is a ReDraw session rather than the title-screen creator.")]
        public System.Action OnCancelRedraw;

        /// <summary>Whether something is on top of the creator right now.</summary>
        public bool Blocking =>
            (NamePanel != null && NamePanel.activeSelf)
            || (ConfirmPanel != null && ConfirmPanel.activeSelf);

        private void OnEnable()
        {
            HideAll();
        }

        private void Update()
        {
            if (Creator != null)
            {
                Creator.InputBlocked = Blocking;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleEscape();
            }
            // Enter submits the name, the way typing one and pressing return
            // should. InputField's own onSubmit is a listener, and a listener
            // added by the builder is not serialized -- see ButtonAction.
            else if (NamePanel != null && NamePanel.activeSelf
                     && (keyboard.enterKey.wasPressedThisFrame
                         || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                ConfirmName();
            }
        }

        /// <summary>
        /// What Escape means depends on what is already on top: dismiss the
        /// discard prompt, or the name prompt (back to painting, not all the
        /// way out), and only otherwise treat it as pressing Back.
        /// </summary>
        public void HandleEscape()
        {
            if (ConfirmPanel != null && ConfirmPanel.activeSelf)
            {
                KeepEditing();
            }
            else if (NamePanel != null && NamePanel.activeSelf)
            {
                NamePanel.SetActive(false);
            }
            else
            {
                TryGoBack();
            }
        }

        public void AskName()
        {
            if (NamePanel == null)
            {
                return;
            }
            if (NameField != null)
            {
                NameField.text = string.Empty;
                NameField.ActivateInputField();
            }
            NamePanel.SetActive(true);
        }

        public void ConfirmName()
        {
            string typed = NameField != null ? NameField.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(typed))
            {
                if (NameField != null)
                {
                    NameField.ActivateInputField();
                }
                return;
            }

            if (Title == null || Title.Select == null || Creator == null)
            {
                return;
            }

            // Through CharacterSelect, which owns making and saving characters.
            // The creator knowing how to write a save file as well would be two
            // places to keep in step over what a new character starts as.
            Title.Select.PendingPaint = Creator.Paint.Encode();
            Title.Select.PendingHair = Creator.HairStyle;
            if (Title.Select.NameField != null)
            {
                Title.Select.NameField.text = typed;
            }
            HideAll();
            Title.Select.ConfirmCreate();
        }

        public void TryGoBack()
        {
            if (Creator != null && Creator.HasEdits && ConfirmPanel != null)
            {
                ConfirmPanel.SetActive(true);
                return;
            }
            Discard();
        }

        public void Discard()
        {
            HideAll();
            if (Title != null)
            {
                Title.Show(TitleScreen.Panel.CharacterSelect);
            }
            else
            {
                // No title screen to go back to -- this is a ReDraw session
                // walking away from its edits instead.
                OnCancelRedraw?.Invoke();
            }
        }

        /// <summary>
        /// ReDraw's own SAVE button. Unlike ConfirmName, there is no name to
        /// type and nothing routes through CharacterSelect -- the paint is
        /// handed straight to whoever started the session.
        /// </summary>
        public void SaveRedraw()
        {
            if (Creator != null)
            {
                OnConfirmRedraw?.Invoke(Creator);
            }
        }

        public void KeepEditing()
        {
            if (ConfirmPanel != null)
            {
                ConfirmPanel.SetActive(false);
            }
        }

        private void HideAll()
        {
            if (NamePanel != null) NamePanel.SetActive(false);
            if (ConfirmPanel != null) ConfirmPanel.SetActive(false);
        }
    }
}
