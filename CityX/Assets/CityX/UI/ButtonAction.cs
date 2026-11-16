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

            SettingsResolutionPrevious,
            SettingsResolutionNext,
            SettingsInputDevicePrevious,
            SettingsInputDeviceNext,
            SettingsOutputDevicePrevious,
            SettingsOutputDeviceNext,

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

            // ADDED AT THE END, NOT ALPHABETICALLY/THEMATICALLY GROUPED WITH
            // ShowMultiplayer ABOVE -- Bug, caught by
            // TitleScreenWiringTests.EveryButtonActionHasWhateverItActsOn:
            // Title.unity is a SAVED scene, and Unity serializes an enum
            // field by its raw ordinal, not its name. Inserting new members
            // in the MIDDLE of this enum (tried first, reads more naturally
            // next to ShowMultiplayer) shifted every later member's ordinal
            // by three, so the already-saved scene's own stored integers for
            // SelectBeginCreate/SelectChooseSkin/etc. silently deserialized
            // as whichever member now happened to occupy that same number --
            // every button past the insertion point came back wired to the
            // wrong kind of view. New members must only ever be appended
            // here, never inserted, until the whole enum is retired.
            MultiplayerHost,
            MultiplayerJoin,
            MultiplayerBack,

            /// <summary>A swatch in the hair colour card: dyes the hair Colour. Needs HairControls.</summary>
            CreatorHairColour,

            /// <summary>The settings screen's frame-rate cap: the previous / next choice. Appended, so no saved value moves.</summary>
            SettingsFpsPrevious,
            SettingsFpsNext,

            /// <summary>One of the creator's PAINT / HAIR / FACE tabs: puts page Value up. Needs Tabs. Appended, so no saved value moves.</summary>
            CreatorTab,

            /// <summary>The creator's T-POSE / RELAX button: lifts the body's arms to be painted, or lets them down (BeltIntro). Needs Title. Appended.</summary>
            CreatorTogglePose,

            /// <summary>The character list's CREATE NEW: the belt rolls a blank body in behind the characters on it (CharacterSelect.CreateNew). Needs Select. Appended.</summary>
            SelectCreateNew,

            /// <summary>The pencil on the chosen character's card: redraws its look (CharacterSelect.BeginRedrawSelected). Needs Select. Appended.</summary>
            SelectBeginRedrawSelected,
        }

        public Kind Action;

        public TitleScreen Title;

        public CharacterSelect Select;

        public CharacterCreator Creator;

        public TitleSettingsView Settings;

        public TitleMultiplayerView Multiplayer;

        [Tooltip("So a palette press moves the colour wheel with it.")]
        public CreatorControls Controls;

        [Tooltip("So a hair colour swatch moves the hair colour picker with it.")]
        public HairColourControls HairControls;

        [Tooltip("The creator's tabs, for a tab button.")]
        public CreatorTabs Tabs;

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
                case Kind.ShowCreator:
                    if (Title != null) Title.BeginCreator();
                    break;
                case Kind.ShowMultiplayer: Show(TitleScreen.Panel.Multiplayer); break;
                case Kind.ShowSettings: Show(TitleScreen.Panel.Settings); break;

                case Kind.TitleBack:
                    if (Title != null) Title.Back();
                    break;

                case Kind.MultiplayerHost:
                    if (Multiplayer != null) Multiplayer.Host();
                    break;
                case Kind.MultiplayerJoin:
                    if (Multiplayer != null) Multiplayer.Join();
                    break;
                case Kind.MultiplayerBack:
                    if (Multiplayer != null) Multiplayer.Back();
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
                case Kind.SelectBeginRedrawSelected:
                    if (Select != null) Select.BeginRedrawSelected();
                    break;

                case Kind.SettingsResolutionPrevious:
                    if (Settings != null) Settings.Previous();
                    break;
                case Kind.SettingsResolutionNext:
                    if (Settings != null) Settings.Next();
                    break;
                case Kind.SettingsInputDevicePrevious:
                    if (Settings != null) Settings.PreviousInputDevice();
                    break;
                case Kind.SettingsInputDeviceNext:
                    if (Settings != null) Settings.NextInputDevice();
                    break;
                case Kind.SettingsOutputDevicePrevious:
                    if (Settings != null) Settings.PreviousOutputDevice();
                    break;
                case Kind.SettingsOutputDeviceNext:
                    if (Settings != null) Settings.NextOutputDevice();
                    break;
                case Kind.SettingsFpsPrevious:
                    if (Settings != null) Settings.PreviousFps();
                    break;
                case Kind.SettingsFpsNext:
                    if (Settings != null) Settings.NextFps();
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

                case Kind.CreatorHairColour:
                    if (HairControls != null)
                    {
                        // Through the picker, so its square, strip and preview move to the swatch.
                        HairControls.Adopt(Colour);
                    }
                    else if (Creator != null)
                    {
                        Creator.SetHairColour(Colour);
                    }
                    break;

                case Kind.CreatorTab:
                    if (Tabs != null)
                    {
                        Tabs.Show(Value);
                    }
                    break;

                case Kind.CreatorTogglePose:
                    if (Title != null && Title.CreatorPanel != null && Title.CreatorPanel.TryGetComponent(out BeltIntro belt))
                    {
                        belt.TogglePose();
                    }
                    break;

                case Kind.SelectCreateNew:
                    if (Select != null) Select.CreateNew();
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
