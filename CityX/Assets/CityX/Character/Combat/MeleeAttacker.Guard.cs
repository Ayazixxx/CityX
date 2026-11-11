using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE BARE-HANDED GUARD's rules (<see cref="FistGuard"/>): held with right-click and no weapon in hand, it blocks as the sword's held
    /// guard does (PlayerVitals: a quarter of the blow through, stamina for the rest, a guard it cannot afford broken open), its first
    /// <see cref="FistGuard.ParrySeconds"/> a parry. A punch drops it for as long as the punch lasts. What it looks like is the animation
    /// driver's, told by name, so other players' copies raise, lower and are knocked about the same.
    /// </summary>
    public sealed partial class MeleeAttacker
    {
        private bool _guarding;
        private int _guardAskedFrame = -1;
        private int _parriedFrame = -1;
        private PlayerAnimationDriver _guardDriver;

        /// <summary>The bare-handed guard is up.</summary>
        public bool IsGuarding => _guarding;

        /// <summary>Called every frame the fists are free, with whether the guard's button is down.</summary>
        public void SetGuardHeld(bool held)
        {
            _guardAskedFrame = Time.frameCount;
            bool can = held && EquippedWeapon == null && !IsSwinging && _vitals != null && !_vitals.IsDead && !_vitals.IsStunned
                && !_vitals.IsExhausted && _vitals.Stamina > 0.0f;
            if (!can)
            {
                LowerGuard();
                return;
            }
            if (!_guarding)
            {
                _guarding = true;
                _vitals.BlockDrainsStamina = true;
                _vitals.BlockStaminaPerDamage = FistGuard.StaminaPerDamage;
                _vitals.GuardBreakStunSeconds = FistGuard.BreakStunSeconds;
                _vitals.BeginPerfectGuard(FistGuard.ParrySeconds);
                _vitals.Parried += OnGuardParried;
                _vitals.GuardStruck += OnGuardSoaked;
                TellTheGuard(FistGuard.RaiseName);
            }
            else if (_guardDriver != null && _guardDriver.FistGuardPose != null && !_guardDriver.FistGuardPose.IsUp)
            {
                TellTheGuard(FistGuard.RaiseName);   // something took the arms (a clip told by name): up again
            }
            // Raised again every frame: the guard lasts exactly as long as the button does.
            _vitals.StartBlock();
        }

        private void LowerGuard()
        {
            if (!_guarding)
            {
                return;
            }
            _guarding = false;
            _vitals.Parried -= OnGuardParried;
            _vitals.GuardStruck -= OnGuardSoaked;
            _vitals.EndBlock();
            _vitals.BlockDrainsStamina = false;
            TellTheGuard(FistGuard.LowerName);
        }

        /// <summary>A guard nobody asked for this frame (the body stunned, dead, in a menu, a weapon drawn) comes down.</summary>
        private void TickGuard()
        {
            if (_guarding && Time.frameCount - _guardAskedFrame > 1)
            {
                LowerGuard();
            }
        }

        private void OnGuardParried(Vector3 from)
        {
            _parriedFrame = Time.frameCount;
            TellTheGuard(FistGuard.ParryName);
        }

        /// <summary>A blow met the guard (said after a parry too: the swat is the answer to that one).</summary>
        private void OnGuardSoaked(Vector3 from)
        {
            if (_parriedFrame != Time.frameCount)
            {
                TellTheGuard(FistGuard.HitName);
            }
        }

        private void TellTheGuard(string name)
        {
            if (_guardDriver == null)
            {
                _guardDriver = GetComponent<PlayerAnimationDriver>();
            }
            if (_guardDriver != null)
            {
                _guardDriver.PlayLocalState(name);
            }
        }
    }
}
