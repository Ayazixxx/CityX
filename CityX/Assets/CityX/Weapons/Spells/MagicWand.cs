using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE MAGIC WAND (owner, 2026-10-30: "make a magic wand maybe look at hogward legacy or something for inspiration though it doesnt really need
    /// to do much. its just a little stick you hold that when you use hotbar spells will be casted from the wand ... a basic non elemental magic
    /// wand that just gives a slight boost to spells casted while holding it, maybe 5% more damage 10% less mana usage"; docs/MAGIC_WAND.md).
    /// Held in the right hand as a staff is, but across the fist on a slant as a pen is, and pointed rather than stood upright (PlayerLocomotion.Wand.cs). Its
    /// click is a speck of dust (MagicSpeckCaster.Wand); held, it is the body's spell focus (<see cref="ISpellFocus"/>): every spell cast then
    /// leaves its tip (PlayerInputReader), hurts a twentieth more and pays a tenth less mana (PlayerVitals). Another player's copy holds the model.
    /// </summary>
    public sealed class MagicWand : StaffItem, ISpellFocus
    {
        public float SpellPowerBonus => MagicSpeckRules.WandSpellPowerBonus;

        public float ManaCostFactor => MagicSpeckRules.WandManaCostFactor;

        /// <summary>Across the fist on a slant toward the knuckles, as a pen is held (PlayerLocomotion.WandGripTiltDegrees).</summary>
        protected override Quaternion HeldRotation => Quaternion.AngleAxis(PlayerLocomotion.WandGripTiltDegrees, Vector3.up) * GripRotation;

        protected override IStaffCaster CasterOn(GameObject body)
        {
            var caster = body != null ? body.GetComponent<MagicSpeckCaster>() : null;
            return caster != null ? caster.Wand : null;
        }

        public override void OnEquip(GameObject player, ItemInstanceData instanceData)
        {
            base.OnEquip(player, instanceData);
            var vitals = player.GetComponent<PlayerVitals>();
            if (vitals != null)
            {
                vitals.SpellFocus = this;
            }
        }

        public override void OnUnequip(GameObject player)
        {
            var vitals = player != null ? player.GetComponent<PlayerVitals>() : null;
            if (vitals != null && ReferenceEquals(vitals.SpellFocus, this))
            {
                vitals.SpellFocus = null;
            }
            base.OnUnequip(player);
        }

        /// <summary>The body points a wand (PlayerLocomotion.Wand.cs) where it stands a staff upright.</summary>
        protected override void PoseOn(PlayerLocomotion locomotion, bool held)
        {
            if (held)
            {
                locomotion.Wand = transform;
                locomotion.WandTip = Tip;
            }
            else if (locomotion.Wand == transform)
            {
                locomotion.Wand = null;
                locomotion.WandTip = null;
            }
        }
    }
}
