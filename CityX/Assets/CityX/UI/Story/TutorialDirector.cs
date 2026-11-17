using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.View;
using CityX.Weapons;
using CityX.World;
using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// Runs the tutorial on this machine (docs/TUTORIAL.md): finds Old Guy, watches the world for what his steps ask (in the house, a weapon
    /// in hand, the swordsman down, the power on, the points spent), puts the step up on the HUD (ObjectiveView), moves him into his den once
    /// the house is clear, gives the level-up before the stat points, and plays the ending (TutorialDirector.Ending.cs). Sits on the HUD,
    /// which knows this machine's player.
    /// </summary>
    public sealed partial class TutorialDirector : MonoBehaviour
    {
        /// <summary>How often Old Guy, the house and the swordsman are looked for, seconds.</summary>
        public const float SearchSeconds = 1.0f;

        /// <summary>How far inside the walls counts as in the house, metres.</summary>
        public const float InsideMargin = 0.3f;

        /// <summary>How long after he is put down a swordsman not found counts as beaten: in a session he arrives from the server.</summary>
        public const float SwordsmanArrivalSeconds = 3.0f;

        /// <summary>The starter weapons lying in the house (OldGuysHouse.Items, and the bat in its crate): holding any of them is armed.</summary>
        public static readonly string[] StarterWeapons =
        {
            ItemRegistry.KitchenKnife, ItemRegistry.ButterKnife, ItemRegistry.Slingshot, ItemRegistry.MagicWand, ItemRegistry.BaseballBat,
        };

        private PlayerHud _hud;
        private ObjectiveView _objective;
        private OldGuy _oldGuy;
        private OldGuysHouseSite _house;
        private float _nextSearch;
        private float _swordsmanPutDownAt = -1.0f;
        private bool _intruderDown;
        private bool _wantsTheDen;
        private readonly List<LootContainer> _boxesOpenBeforeTheWarning = new List<LootContainer>();

        /// <summary>The Old Guy whose tutorial this is; null before he is put down, and once the ending is over.</summary>
        public OldGuy OldGuy => _oldGuy;

        private void Awake()
        {
            _hud = GetComponent<PlayerHud>();
            _objective = GetComponent<ObjectiveView>();
        }

        private void OnDestroy()
        {
            Follow(null);
        }

        private void Update()
        {
            if (Time.time >= _nextSearch)
            {
                _nextSearch = Time.time + SearchSeconds;
                Search();
            }
            if (_oldGuy == null)
            {
                return;
            }
            TutorialProgress progress = _oldGuy.Progress;
            if (progress.Running && progress.Step != TutorialStep.Ambush)
            {
                progress.Observe(Facts());
            }
            if (_wantsTheDen && _house != null
                && _oldGuy.TryMoveUnseen(_house.transform.TransformPoint(OldGuysHouse.OldGuyInTheDen), DenFacing(), Camera.main))
            {
                _wantsTheDen = false;
            }
            WatchTheBoxes(progress);
        }

        private void Search()
        {
            if (_house == null)
            {
                _house = FindFirstObjectByType<OldGuysHouseSite>();
            }
            if (_oldGuy == null)
            {
                Follow(FindFirstObjectByType<OldGuy>());
            }
            _intruderDown = _oldGuy != null && _oldGuy.Progress.Running && _oldGuy.Progress.Step <= TutorialStep.Intruder && IntruderDown();
        }

        private void Follow(OldGuy oldGuy)
        {
            if (_oldGuy != null)
            {
                _oldGuy.Progress.StepChanged -= OnStepChanged;
            }
            _oldGuy = oldGuy;
            if (_oldGuy != null)
            {
                _oldGuy.Progress.StepChanged += OnStepChanged;
                ShowObjective(_oldGuy.Progress.Step);
            }
        }

        private void OnStepChanged(TutorialStep step)
        {
            ShowObjective(step);
            switch (step)
            {
                case TutorialStep.ReportBack:
                    _wantsTheDen = true;
                    break;
                case TutorialStep.Power:
                    RememberTheOpenBoxes();
                    break;
                case TutorialStep.Stats:
                    LevelUp();
                    break;
                case TutorialStep.Ambush:
                    StartCoroutine(Ending());
                    break;
            }
        }

        private void ShowObjective(TutorialStep step)
        {
            if (_objective != null)
            {
                _objective.Show(TutorialScript.Objective(step), TutorialScript.Hint(step));
            }
        }

        private Quaternion DenFacing() => _house.transform.rotation * Quaternion.Euler(0.0f, OldGuysHouse.OldGuyInTheDenYaw, 0.0f);

        // ---- what the world shows ----

        private PlayerVitals _player;

        /// <summary>This machine's player: the HUD's, or one a test sets.</summary>
        public PlayerVitals Player
        {
            get => _player != null ? _player : (_hud != null ? _hud.Target : null);
            set => _player = value;
        }

        /// <summary>The world as the tutorial's steps ask about it, this frame.</summary>
        public TutorialFacts Facts()
        {
            PlayerVitals player = Player;
            if (player == null)
            {
                return default;
            }
            return new TutorialFacts
            {
                Inside = _house != null && OldGuysHouse.WithinWalls(_house.transform.InverseTransformPoint(player.transform.position), -InsideMargin),
                HasWeapon = Armed(player.GetComponent<Inventory>()),
                IntruderDown = _intruderDown,
                PowerOn = TutorialHousePower.Shared.IsOn,
                StatPointsLeft = player.StatPoints > 0,
                SkillPointsLeft = player.SkillPoints > 0,
            };
        }

        private static bool Armed(Inventory inventory)
        {
            if (inventory == null)
            {
                return false;
            }
            foreach (string weapon in StarterWeapons)
            {
                if (inventory.HasItem(weapon) || inventory.EquippedItemId(Inventory.PrimarySlot) == weapon
                    || inventory.EquippedItemId(Inventory.SecondarySlot) == weapon)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How far from where he was put down a session's swordsman is looked for, metres (his notice: he may chase the player out).</summary>
        public const float SessionSwordsmanMetres = SwordsmanBrain.NoticeMetres;

        /// <summary>
        /// The swordsman upstairs has been put down and beaten. Alone he is the house's own; in a session the server's, which gives no object
        /// back, so there it is that no swordsman stands alive near where he was put down.
        /// </summary>
        private bool IntruderDown()
        {
            if (_house == null || !_house.SwordsmanPutDown)
            {
                _swordsmanPutDownAt = -1.0f;
                return false;
            }
            if (_swordsmanPutDownAt < 0.0f)
            {
                _swordsmanPutDownAt = Time.time;
            }
            if (_house.Swordsman != null)
            {
                PlayerVitals his = _house.Swordsman.GetComponent<PlayerVitals>();
                return his == null || his.IsDead;
            }
            if (Time.time - _swordsmanPutDownAt < SwordsmanArrivalSeconds)
            {
                return false;
            }
            Vector3 spot = _house.transform.TransformPoint(OldGuysHouse.SwordsmanSpot);
            foreach (SwordsmanBrain swordsman in FindObjectsByType<SwordsmanBrain>(FindObjectsSortMode.None))
            {
                PlayerVitals vitals = swordsman.GetComponent<PlayerVitals>();
                if (vitals != null && !vitals.IsDead && Vector3.Distance(swordsman.transform.position, spot) <= SessionSwordsmanMetres)
                {
                    return false;
                }
            }
            return true;
        }

        private void LevelUp()
        {
            PlayerVitals player = Player;
            if (player != null && player.StatPoints == 0)
            {
                player.AddXp(Mathf.Max(1, player.ExperienceToNextLevel - player.Experience));
            }
        }

        // ---- the boxes in the cellar (a karma choice) ----

        private void RememberTheOpenBoxes()
        {
            _boxesOpenBeforeTheWarning.Clear();
            foreach (LootContainer box in LootContainer.All)
            {
                if (box.IsOpen && InTheCellar(box))
                {
                    _boxesOpenBeforeTheWarning.Add(box);
                }
            }
        }

        /// <summary>After Old Guy said to stay out of them, a box in the cellar opened.</summary>
        private void WatchTheBoxes(TutorialProgress progress)
        {
            if (progress.OpenedTheBoxes || progress.Step < TutorialStep.Power || progress.Step >= TutorialStep.Ambush)
            {
                return;
            }
            foreach (LootContainer box in LootContainer.All)
            {
                if (box.IsOpen && !_boxesOpenBeforeTheWarning.Contains(box) && InTheCellar(box))
                {
                    progress.OpenedTheBoxes = true;
                    return;
                }
            }
        }

        private bool InTheCellar(LootContainer box)
        {
            if (_house == null || box == null)
            {
                return false;
            }
            Vector3 local = _house.transform.InverseTransformPoint(box.Centre);
            OldGuysHouse.Room cellar = OldGuysHouse.RoomNamed("Cellar");
            return cellar != null && local.y < OldGuysHouse.LevelY(1) && cellar.Contains(local.x, local.z);
        }

        // ---- the console ----

        /// <summary>Puts the tutorial straight onto <paramref name="step"/> (the console's /tutorial). Null when done, else why not.</summary>
        public string JumpTo(TutorialStep step)
        {
            if (_oldGuy == null)
            {
                Search();
            }
            if (_oldGuy == null || _oldGuy.Collapsed)
            {
                return "Old Guy is not here (the tutorial is over for this character, or he has not been put down yet)";
            }
            _oldGuy.Progress.JumpTo(step);
            if (step >= TutorialStep.ReportBack && _house != null)
            {
                _oldGuy.TryMoveUnseen(_house.transform.TransformPoint(OldGuysHouse.OldGuyInTheDen), DenFacing(), null);
                _wantsTheDen = false;
            }
            return null;
        }
    }
}
