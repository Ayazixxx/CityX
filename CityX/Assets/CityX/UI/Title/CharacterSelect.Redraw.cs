using CityX.Character;
using CityX.Core;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Redrawing a character from the list (owner, 2026-11-15: "add a pencil icon next to the trash icon to redraw"): the pencil on the chosen
    /// character's card opens it in the redraw creator, the same one the Player menu's ReDraw opens in the world (RedrawCreatorRuntime), on a copy of
    /// its figure. SAVE writes the new paint and hair to the server played on and the figure on the belt is made again with them; CANCEL leaves it
    /// as it was. Either way the card is back up.
    /// </summary>
    public sealed partial class CharacterSelect
    {
        private GameObject _redraw;
        private int _redrawSlot = -1;
        private int _redrawEndedOnFrame = -1;

        /// <summary>The redraw creator, while it is open (for tests).</summary>
        public GameObject RedrawOverlay => _redraw;

        /// <summary>Opens the chosen character in the redraw creator, from its card.</summary>
        public void BeginRedrawSelected()
        {
            CharacterSave character = Selected;
            GameObject figure = Lineup != null ? Lineup.FigureAt(_selectedIndex) : null;
            if (_redraw != null || character == null || figure == null || (_mode != Mode.Inspecting && _mode != Mode.List))
            {
                return;
            }
            _redrawSlot = _selectedIndex;
            _redraw = RedrawCreatorRuntime.Build(figure, Lineup.HairStyleMeshes, Lineup.HairMaterialAsset, OnRedrawSaved, CloseTheRedraw);
            Show(Mode.Redrawing);
            RedrawCreatorRuntime.Seed(_redraw, character);
        }

        private void OnRedrawSaved(CharacterCreator creator)
        {
            CharacterSave character = _redrawSlot >= 0 && _redrawSlot < _characters.Count ? _characters[_redrawSlot] : null;
            if (character != null && creator != null)
            {
                character.SetPaint(creator.Paint.Encode());
                character.HairStyle = creator.HairStyle;
                character.HairColour = creator.HairColour;
                // Saved as the character stands in the list: nothing but its look is different, so the server takes the look and keeps the rest.
                CharacterSources.Current.Save(character, adminEdited: false);
                if (Lineup != null)
                {
                    Lineup.Remake(_redrawSlot, character);
                }
            }
            CloseTheRedraw();
        }

        /// <summary>Closes the redraw creator, if it is open, and puts the card back up.</summary>
        private void CloseTheRedraw()
        {
            if (_redraw != null)
            {
                Destroy(_redraw);
                _redraw = null;
                _redrawEndedOnFrame = Time.frameCount;
            }
            _redrawSlot = -1;
            if (_mode == Mode.Redrawing)
            {
                Show(Selected != null ? Mode.Inspecting : Mode.List);
            }
        }
    }
}
