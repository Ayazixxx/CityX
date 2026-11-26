using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// HIS WEAPON PUT AWAY TILL A FIGHT (docs/HOBO.md; owner, 2026-11-24: "make it so homeless guys dont usually walk around with weapon in hand
    /// maybe 20 percent of them do. they usually only pull it out when attacked"). With <see cref="CalmArmedShare"/> under 1, he is spawned with
    /// nothing in his hand; that share of them takes it out at once, the rest when hurt or when he takes someone on. Other players' copies start
    /// empty-handed too and take it out when told (<see cref="PlayerAnimationDriver.WeaponDrawnName"/>), told again every few seconds while it is
    /// out, so a player who comes near or joins later sees it in his hand.
    /// </summary>
    public sealed partial class SwordsmanBrain
    {
        [Tooltip("The share of these spawned with the weapon already in hand (a hobo: 0.2); the rest take it out when the fight starts. 1: always in hand.")]
        [Range(0.0f, 1.0f)]
        public float CalmArmedShare = 1.0f;

        /// <summary>How often a drawn weapon is told to other players again, seconds.</summary>
        public const float DrawnRepeatSeconds = 4.0f;

        /// <summary>Whether his weapon has been taken out (it stays taken out: a bottle broken to nothing is not drawn again).</summary>
        public bool WeaponDrawn { get; private set; }

        private bool _armedRolled;
        private bool _armedFromSpawn;
        private float _sinceDrawnTold;

        /// <summary>He starts with it in hand when he always carries it so; otherwise it waits for the roll or the fight.</summary>
        private void ArmAtSpawn()
        {
            if (CalmArmedShare >= 1.0f)
            {
                _armedFromSpawn = true;   // so on every machine: nothing to tell
                DrawWeapon();
            }
            if (_driver != null)
            {
                _driver.RemoteWeaponDrawn += DrawWeapon;   // on another player's copy of him
            }
        }

        private void DrawWeapon()
        {
            if (WeaponDrawn || (_vitals != null && _vitals.IsDead))
            {
                return;
            }
            WeaponDrawn = true;
            EquipSword();
            _sinceDrawnTold = DrawnRepeatSeconds;   // told at once
        }

        /// <summary>The machine that runs him: the spawn's roll, out for a fight, and told to the others while it is out.</summary>
        private void TickSheathing(float deltaTime)
        {
            if (!_armedRolled)
            {
                _armedRolled = true;
                if (Random.value < CalmArmedShare)
                {
                    DrawWeapon();
                }
            }
            if (!WeaponDrawn && (_provoked || Opponents.Alive(_target)))
            {
                DrawWeapon();
            }
            if (!WeaponDrawn || _armedFromSpawn || _driver == null)
            {
                return;   // one always armed is armed on every machine from the start
            }
            if (_bladeBody == null)
            {
                return;   // gone from his hand (a bottle broken to nothing): a late arrival would be shown a fresh one (bug sweep 2026-11-25)
            }
            _sinceDrawnTold += deltaTime;
            if (_sinceDrawnTold >= DrawnRepeatSeconds)
            {
                _sinceDrawnTold = 0.0f;
                _driver.AnnounceState(PlayerAnimationDriver.WeaponDrawnName);
            }
        }
    }
}
