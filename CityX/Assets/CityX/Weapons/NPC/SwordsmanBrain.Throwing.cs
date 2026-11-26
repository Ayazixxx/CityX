using CityX.Character;
using CityX.Items;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A HOBO THROWS HIS SPARE BOTTLES (docs/HOBO.md; owner, 2026-11-24: "make it so they throw a bottle at you if you are distanced from them and
    /// they have a bottle in hand. if they spawn with the broken bottle make it so they have 3 not broken but empty glass bottles in their
    /// inventory they can throw and are lootable upon death"). With a bottle in his hand and whole ones carried (CarriedLoot), whoever he fights
    /// standing off at a distance gets one thrown at them: he stops, the arm goes back with a whole bottle in the hand (the broken one he keeps
    /// to fight with is out of sight meanwhile) and it is lobbed to land on them. Close in, he fights with the broken one. Other players see the
    /// throw, the whole bottle in the hand and the bottle flying (BottleThrower), and their copy of him carries one fewer.
    /// </summary>
    public sealed partial class SwordsmanBrain
    {
        /// <summary>He throws at someone at least this far off, metres (nearer, he comes in with the bottle in his hand).</summary>
        public const float ThrowFromMetres = 6.0f;

        /// <summary>...and no further than this, metres.</summary>
        public const float ThrowToMetres = 24.0f;

        /// <summary>The least time between two throws, seconds.</summary>
        public const float SecondsBetweenThrows = 2.5f;

        /// <summary>He throws only once he faces them this nearly, degrees.</summary>
        public const float ThrowFacingDegrees = 20.0f;

        /// <summary>A throw the arm never finished (he was knocked down) is given up after this, seconds.</summary>
        public const float ThrowGivenUpSeconds = 2.0f;

        /// <summary>How many bottles he has thrown (for the tests).</summary>
        public int BottlesThrown { get; private set; }

        private BottleThrower _thrower;
        private CarriedLoot _carried;
        private bool _lookedForBottles;
        private BottleWeapon _throwingWith;
        private string _spare;
        private float _throwStartedAt;
        private float _nextThrow;

        /// <summary>Whether he is throwing this frame: a throw going on, or one he begins now.</summary>
        private bool ThrowsABottle(DuelistMind.Senses senses)
        {
            if (_throwingWith != null)
            {
                if (Time.time - _throwStartedAt > ThrowGivenUpSeconds)
                {
                    StopThrowing();
                    return false;
                }
                return true;
            }
            if (!_lookedForBottles)
            {
                _lookedForBottles = true;
                _thrower = GetComponent<BottleThrower>();
                _carried = GetComponent<CarriedLoot>();
            }
            if (_thrower == null || _carried == null || !senses.HasTarget || senses.Stunned || senses.Committed || Time.time < _nextThrow
                || senses.Distance < ThrowFromMetres || senses.Distance > ThrowToMetres || _locomotion.IsDashing
                || !(_bladeBody is BottleWeapon held) || held == null)
            {
                return false;
            }
            string spare = SpareBottle();
            if (spare == null)
            {
                return false;
            }
            Vector3 apart = _target.transform.position - transform.position;
            apart.y = 0.0f;
            Vector3 facing = transform.forward;
            facing.y = 0.0f;
            if (Vector3.Angle(facing, apart) > ThrowFacingDegrees || !InSight(_target.transform))
            {
                return false;
            }
            _throwingWith = held;
            _spare = spare;
            _throwStartedAt = Time.time;
            held.ShowSpare(true);
            _thrower.Driver.Released += LetTheSpareGo;
            if (!_thrower.BeginThrow(spare: true))
            {
                StopThrowing();
                return false;
            }
            return true;
        }

        /// <summary>A whole glass bottle he carries, or null.</summary>
        private string SpareBottle()
        {
            foreach (string id in _carried.Items)
            {
                if (GlassBottleKinds.IsBottle(id) && System.Array.IndexOf(BottleThrower.Throwables, id) >= 0)
                {
                    return id;
                }
            }
            return null;
        }

        /// <summary>Nothing solid between his eyes and their chest.</summary>
        private bool InSight(Transform someone)
        {
            float height = BodyRig.StandingHeight;
            return !Sight.SolidBetween(transform.position + Vector3.up * (height * 0.92f), someone.position + Vector3.up * (height * 0.62f), transform, someone);
        }

        /// <summary>The arm's release: the spare leaves the hand, lobbed to come down on whoever he fights, and he carries one fewer.</summary>
        private void LetTheSpareGo()
        {
            BottleWeapon held = _throwingWith;
            string spare = _spare;
            StopThrowing(cutShort: false);   // it leaves now: the Thrown event tells other screens
            _nextThrow = Time.time + SecondsBetweenThrows;
            if (held == null || !_carried.Spend(spare))
            {
                return;
            }
            int index = System.Array.IndexOf(BottleThrower.Throwables, spare);
            GameObject prefab = index >= 0 && index < _thrower.ThrownPrefabs.Length ? _thrower.ThrownPrefabs[index] : null;
            Vector3 from = held.BladeTipWorld * 0.5f + held.BladeBaseWorld * 0.5f;
            Vector3 at = Opponents.Alive(_target)
                ? _target.transform.position + Vector3.up * (BodyRig.StandingHeight * 0.6f)
                : from + transform.forward * ThrowFromMetres;
            Vector3 velocity = BottleLob.Velocity(from, at, BottleWeapon.ThrowSpeedConst, -Physics.gravity.y);
            _thrower.Throw(prefab, spare, BottleWeapon.DurabilityState.Intact, from, held.transform.rotation, velocity, BottleWeapon.IntactThrowDamage);
            BottlesThrown++;
        }

        /// <summary>A throw going on is over (thrown, or <paramref name="cutShort"/>: stopped before it left): the hand shows its own bottle again.</summary>
        private void StopThrowing(bool cutShort = true)
        {
            if (_thrower != null)
            {
                _thrower.Driver.Released -= LetTheSpareGo;
            }
            if (_throwingWith != null)
            {
                if (cutShort && _throwingWith.ShowsSpare && _thrower != null)
                {
                    _thrower.TellThrowCutShort();   // the spare never left: other screens' copies show his own bottle again too
                }
                _throwingWith.ShowSpare(false);
            }
            _throwingWith = null;
        }
    }
}
