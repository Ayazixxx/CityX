using System.Collections.Generic;
using CityX.Core;
using CityX.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CityX.UI
{
    /// <summary>
    /// The settings' graphics page (docs/GRAPHICS_SETTINGS.md), on the title screen and in the pause menu alike: one row a
    /// <see cref="GraphicsMenu"/> row, each stepped with its arrows, put into force at once (<see cref="GraphicsQuality"/>) and saved. A line
    /// under the rows says what the row last pointed at or changed does and costs. It lies over the settings panel it belongs to: opened, it
    /// hides the panel's own controls; BACK shows them again. Built by GraphicsPanelBuilder.
    /// </summary>
    public sealed class GraphicsSettingsPanel : MonoBehaviour
    {
        public Text[] Values;
        public Button[] PreviousButtons;
        public Button[] NextButtons;
        public Text Description;
        public Button BackButton;

        private readonly List<GameObject> _hidden = new List<GameObject>();
        private GraphicsOptions _options = new GraphicsOptions();

        /// <summary>The options the page shows.</summary>
        public GraphicsOptions Options => _options;

        private void Awake()
        {
            for (int row = 0; row < Values.Length; row++)
            {
                int stepped = row;
                if (PreviousButtons[row] != null)
                {
                    PreviousButtons[row].onClick.AddListener(() => Step(stepped, -1));
                    Describes(PreviousButtons[row].gameObject, stepped);
                }
                if (NextButtons[row] != null)
                {
                    NextButtons[row].onClick.AddListener(() => Step(stepped, 1));
                    Describes(NextButtons[row].gameObject, stepped);
                }
            }
            if (BackButton != null)
            {
                BackButton.onClick.AddListener(Close);
            }
        }

        /// <summary>Shows the page over its settings panel, with the options saved.</summary>
        public void Open()
        {
            _options = GameSettings.Load().Graphics;
            _hidden.Clear();
            foreach (Transform sibling in transform.parent)
            {
                if (sibling != transform && sibling.gameObject.activeSelf)
                {
                    sibling.gameObject.SetActive(false);
                    _hidden.Add(sibling.gameObject);
                }
            }
            gameObject.SetActive(true);
            Show();
            Describe(GraphicsMenu.PresetRow);
        }

        /// <summary>Back to the settings panel's own controls (nothing when the page is not open).</summary>
        public void Close()
        {
            foreach (GameObject hidden in _hidden)
            {
                if (hidden != null)
                {
                    hidden.SetActive(true);
                }
            }
            _hidden.Clear();
            gameObject.SetActive(false);
        }

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>The row's choice one step along, put into force and saved.</summary>
        public void Step(int row, int direction)
        {
            GraphicsMenu.Step(_options, row, direction);
            GraphicsQuality.Apply(_options);
            GameSettingsData data = GameSettings.Load();
            data.Graphics = _options.Clean();
            GameSettings.Save(data);
            Show();
            Describe(row);
        }

        public void Describe(int row)
        {
            if (Description != null && row >= 0 && row < GraphicsMenu.Rows.Length)
            {
                Description.text = GraphicsMenu.Rows[row].Description;
            }
        }

        private void Show()
        {
            for (int row = 0; row < Values.Length && row < GraphicsMenu.Rows.Length; row++)
            {
                if (Values[row] != null)
                {
                    Values[row].text = GraphicsMenu.ValueOf(_options, row);
                }
            }
        }

        /// <summary>Pointing at (or selecting) <paramref name="target"/> describes <paramref name="row"/>.</summary>
        private void Describes(GameObject target, int row)
        {
            var trigger = target.AddComponent<EventTrigger>();
            foreach (EventTriggerType type in new[] { EventTriggerType.PointerEnter, EventTriggerType.Select })
            {
                var entry = new EventTrigger.Entry { eventID = type };
                entry.callback.AddListener(_ => Describe(row));
                trigger.triggers.Add(entry);
            }
        }
    }
}
