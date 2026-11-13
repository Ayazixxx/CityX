using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// The combat contract MeleeAttacker and PlayerInputReader talk to.
    /// Neither ever references a concrete weapon class -- a future sword or
    /// gun costs nothing extra here, it just implements this.
    /// </summary>
    public interface IEquippableWeapon
    {
        float GetMeleeDamage();
        float MeleeCooldownSeconds { get; }
        void RegisterLandedMeleeHit();
        bool CanThrow();
        float ThrowSpeed { get; }
        void Throw(GameObject player, Vector3 origin, Vector3 direction);

        /// <summary>
        /// The ComboChain key this weapon swings through (see
        /// <see cref="ComboChain.For"/>), or null/empty for the older flat
        /// single-swing path -- one cooldown, one (silent) swing, no stages,
        /// no window, GetMeleeDamage()/MeleeCooldownSeconds applied as-is.
        /// The bottle returns null, unchanged from before this existed; a
        /// sword returns ComboChain.Sword to get a real multi-stage chain
        /// with combo windows and a finisher, the same machinery unarmed
        /// combat already uses, instead of Godot's own bolted-on two-hit
        /// flag (_sword_swing_in_progress) that had to be manually cleared
        /// from six different call sites and still had open bugs about it.
        /// </summary>
        string MovesetId { get; }

        /// <summary>
        /// The stage a HELD attack plays for this weapon, or null if this
        /// weapon has no heavy variant -- the bottle returns null (holding
        /// to swing a bottle does exactly what tapping does today; that is
        /// this interface addition's whole "did I just change existing
        /// behaviour" check, and the answer is no). Not a member of
        /// MovesetId's own chain, same reasoning CombatTuning.HeavyPunch's
        /// own doc comment gives for the unarmed haymaker: it hits harder,
        /// takes longer, and starts the chain over rather than advancing
        /// it, so holding is a commitment rather than a free upgrade to
        /// whichever swing was coming next.
        /// </summary>
        ComboStage? HeavyStage { get; }

        /// <summary>A blade: its killing blows cut a part off rather than bursting it (docs/DISMEMBERMENT.md). False for anything blunt.</summary>
        bool IsEdged => false;

        /// <summary>
        /// False while it is away from the body (a thrown boomerang in the air): the hands are bare and a click punches (owner, 2026-11-12: "after
        /// throwing the boomerang ... make it so you can punch when its mid air", "just make that apart of the base game").
        /// </summary>
        bool InHand => true;
    }
}
