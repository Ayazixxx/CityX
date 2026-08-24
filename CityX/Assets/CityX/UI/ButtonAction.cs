using CityX.Character;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// What a button does, as serialized data rather than as a callback.
    ///
    /// THIS EXISTS BECAUSE onClick.AddListener DOES NOT SURVIVE BEING SAVED.
    /// The builders author these screens in code, and wiring a button there
    /// with a C# lambda adds a NON-PERSISTENT listener: it lives in memory, the
    /// scene is written to disk without it, and every button is dead the next
    /// time the scene is opened. Nothing errors -- the buttons highlight, they
    /// press, they do nothing at all. That is exactly what happened: pressing
    /// SINGLEPLAYER went nowhere, and neither did anything else on the screen.
    ///
    /// A component with a field for what to do is serialized like any other
    /// component, and wires itself in Awake, where the listener is being added
    /// at runtime and is therefore real.
    ///
    /// Rows in the character list are exempt -- they are instantiated at
    /// runtime and never saved, so AddListener is fine there.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonAction : MonoBehaviour
    {
        public enum Kind
        {
            /// <summary>
            /// Wired somewhere else. The character list's own PLAY and DELETE
            /// are bound per row at RUNTIME, where a listener is real and does
            /// survive, because those rows are cloned rather than saved.
            /// </summary>
            None,

            ShowMenu,
            ShowCharacterSelect,
            ShowCreator,
            ShowMultiplayer,
            ShowSettings,
            TitleBack,

            SelectBeginCreate,
            SelectConfirmCreate,
            SelectChooseSkin,
            SelectConfirmDelete,
            SelectCancelDelete,
            SelectNext,
            SelectPrevious,
            SelectPlaySelected,
            SelectBeginDeleteSelected,

            CreatorTool,
            CreatorColour,
            CreatorBrushSize,
            CreatorToggleMirror,
            CreatorToggleEraser,
            CreatorUndo,
            CreatorRedo,
            CreatorFinish,

            /// <summary>Value indexes CharacterHair.ValidStyles.</summary>
            CreatorHair,

            CreatorAskName,
            CreatorConfirmName,
            CreatorTryBack,
            CreatorDiscard,
            CreatorKeepEditing,

            /// <summary>ReDraw's own SAVE button -- see CreatorOverlays.SaveRedraw.</summary>
            CreatorRedrawSave,
        }

        public Kind Action;

        public TitleScreen Title;

        public CharacterSelect Select;

        public CharacterCreator Creator;

        [Tooltip("So a palette press moves the colour wheel with it.")]
        public CreatorControls Controls;

        [Tooltip("The name prompt and the discard check, for the buttons that raise them.")]
        public CreatorOverlays Overlays;

        [Tooltip("Skin index, tool index or brush size, depending on the action.")]
        public int Value;

        public Color Colour = Color.white;

        private void Awake()
        {
            if (Action == Kind.None)
            {
                return;
            }
            GetComponent<Button>().onClick.AddListener(Invoke);
        }

        public void Invoke()
        {
            switch (Action)
            {
                case Kind.ShowMenu: Show(TitleScreen.Panel.Menu); break;
                case Kind.ShowCharacterSelect: Show(TitleScreen.Panel.CharacterSelect); break;
                case Kind.ShowCreator: Show(TitleScreen.Panel.Creator); break;
                case Kind.ShowMultiplayer: Show(TitleScreen.Panel.Multiplayer); break;
                case Kind.ShowSettings: Show(TitleScreen.Panel.Settings); break;

                case Kind.TitleBack:
                    if (Title != null) Title.Back();
                    break;

                case Kind.SelectBeginCreate:
                    if (Select != null) Select.BeginCreate();
                    break;
                case Kind.SelectConfirmCreate:
                    if (Select != null) Select.ConfirmCreate();
                    break;
                case Kind.SelectChooseSkin:
                    if (Select != null) Select.ChooseSkin(Value);
                    break;
                case Kind.SelectConfirmDelete:
                    if (Select != null) Select.ConfirmDelete();
                    break;
                case Kind.SelectCancelDelete:
                    if (Select != null) Select.CancelDelete();
                    break;
                case Kind.SelectNext:
                    if (Select != null) Select.Next();
                    break;
                case Kind.SelectPrevious:
                    if (Select != null) Select.Previous();
                    break;
                case Kind.SelectPlaySelected:
                    if (Select != null) Select.PlaySelected();
                    break;
                case Kind.SelectBeginDeleteSelected:
                    if (Select != null) Select.BeginDeleteSelected();
                    break;

                case Kind.CreatorTool:
                    if (Creator != null) Creator.Current = (CharacterCreator.Tool)Value;
                    break;
                case Kind.CreatorColour:
                    if (Controls != null)
                    {
                        // Through the wheel, so it and the preview move to the
                        // swatch instead of still showing the last pick.
                        Controls.Adopt(Colour);
                    }
                    else if (Creator != null)
                    {
                        Creator.Colour = Colour;
                        // Choosing a colour clearly means you want to paint with
                        // it; leaving the eraser silently on would force every
                        // stroke white and make the palette look stuck.
                        Creator.Erasing = false;
                    }
                    break;
                case Kind.CreatorBrushSize:
                    if (Creator != null) Creator.BrushRadiusPixels = Value;
                    break;
                case Kind.CreatorToggleMirror:
                    if (Creator != null) Creator.Mirroring = !Creator.Mirroring;
                    break;
                case Kind.CreatorToggleEraser:
                    if (Creator != null) Creator.Erasing = !Creator.Erasing;
                    break;
                case Kind.CreatorUndo:
                    if (Creator != null) Creator.Undo();
                    break;
                case Kind.CreatorRedo:
                    if (Creator != null) Creator.Redo();
                    break;

                case Kind.CreatorFinish:
                    if (Creator != null && Title != null && Title.Select != null)
                    {
                        Title.Select.PendingPaint = Creator.Paint.Encode();
                        Title.Show(TitleScreen.Panel.CharacterSelect);
                        Title.Select.BeginCreate();
                    }
                    break;

                case Kind.CreatorHair:
                    if (Creator != null && Value >= 0
                        && Value < CharacterHair.ValidStyles.Length)
                    {
                        Creator.SetHairStyle(CharacterHair.ValidStyles[Value]);
                    }
                    break;

                case Kind.CreatorAskName:
                    if (Overlays != null) Overlays.AskName();
                    break;
                case Kind.CreatorConfirmName:
                    if (Overlays != null) Overlays.ConfirmName();
                    break;
                case Kind.CreatorTryBack:
                    if (Overlays != null) Overlays.TryGoBack();
                    break;
                case Kind.CreatorDiscard:
                    if (Overlays != null) Overlays.Discard();
                    break;
                case Kind.CreatorKeepEditing:
                    if (Overlays != null) Overlays.KeepEditing();
                    break;
                case Kind.CreatorRedrawSave:
                    if (Overlays != null) Overlays.SaveRedraw();
                    break;
            }
        }

        private void Show(TitleScreen.Panel panel)
        {
            if (Title != null)
            {
                Title.Show(panel);
            }
        }
    }
}
