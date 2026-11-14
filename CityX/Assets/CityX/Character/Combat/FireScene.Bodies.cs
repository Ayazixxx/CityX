using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHO THE FIRE BURNS: every <see cref="FireRules.BodyTickSeconds"/> the bodies over the burning ground this machine lit are found, and
    /// whoever has their feet in a burning cell catches fire -- <see cref="FireRules.DamagePerSecond"/> in ticks while they stand in it
    /// (a spell with no stagger and no blood, credited to whoever lit the cell), and <see cref="FireRules.AfterburnSeconds"/> of weaker
    /// burning after they leave, flames on the torso. A burning blow sets a body alight the same way (<see cref="SetAlight"/>: the Fire
    /// Enhancement); another player's, only to look at. Water puts a burning body out: standing in it, the water staff's jet
    /// (<see cref="DouseBody"/>), rain over a couple of seconds. A burning body lights the gasoline it walks through.
    /// </summary>
    public sealed partial class FireScene
    {
        private sealed class BurningBody
        {
            public IDamageable Target;
            public Component Body;
            public GameObject Lighter;
            public float SecondsLeft;
            public float NextTick;
            public bool InFire;

            /// <summary>Burning only to look at: another player's blow set it alight, and their machine deals the burning.</summary>
            public bool Cosmetic;
        }

        private static readonly Collider[] Overlaps = new Collider[256];
        private readonly Dictionary<IDamageable, BurningBody> _burning = new Dictionary<IDamageable, BurningBody>();
        private readonly List<IDamageable> _out = new List<IDamageable>();
        private float _bodyTickDue;

        public static bool IsBurning(IDamageable target)
        {
            return Instance != null && target != null && Instance._burning.ContainsKey(target);
        }

        /// <summary>
        /// Sets a body alight for <paramref name="seconds"/> as if it had just stepped out of the fire (a burning blow: the Fire Enhancement),
        /// its burning credited to <paramref name="lighter"/>; <paramref name="cosmetic"/>, it only burns to look at (another player's blow, whose
        /// machine deals the burning). A body already burning burns on for whichever is longer.
        /// </summary>
        public static void SetAlight(IDamageable target, GameObject lighter, float seconds, bool cosmetic = false)
        {
            if (target == null || !target.IsAlive || !(target is Component body))
            {
                return;
            }
            FireScene scene = Ensure();
            if (!scene._burning.TryGetValue(target, out BurningBody burning))
            {
                burning = new BurningBody { Target = target, Body = body, NextTick = Time.time + FireRules.BodyTickSeconds, Cosmetic = cosmetic };
                scene._burning[target] = burning;
            }
            burning.Cosmetic &= cosmetic;
            burning.SecondsLeft = Mathf.Max(burning.SecondsLeft, seconds);
            if (!cosmetic && lighter != null)
            {
                burning.Lighter = lighter;
            }
        }

        /// <summary>Water on a body: its fire goes out.</summary>
        public static void DouseBody(IDamageable target)
        {
            if (Instance != null && target != null)
            {
                Instance._burning.Remove(target);
            }
        }

        private void TickBodies(float deltaTime)
        {
            _bodyTickDue -= deltaTime;
            if (_bodyTickDue <= 0.0f)
            {
                _bodyTickDue = FireRules.BodyTickSeconds;
                CatchBodies();
                BurnBodies();
            }
            _out.Clear();
            foreach (KeyValuePair<IDamageable, BurningBody> entry in _burning)
            {
                BurningBody burning = entry.Value;
                if (burning.Body == null || !burning.Target.IsAlive)
                {
                    _out.Add(entry.Key);
                    continue;
                }
                Vector3 feet = burning.Body.transform.position;
                if (CurrentWater.DepthAt(feet) >= FireRules.DouseDepthMetres)
                {
                    _out.Add(entry.Key);   // in the water: out
                    continue;
                }
                float rain = CurrentWeather.QuenchingOn(feet + Vector3.up * 1.5f);
                burning.SecondsLeft -= deltaTime * (1.0f + rain * (FireRules.AfterburnSeconds / FireRules.RainDouseSeconds));
                if (burning.SecondsLeft <= 0.0f)
                {
                    _out.Add(entry.Key);
                    continue;
                }
                float strength = burning.InFire ? 1.0f : Mathf.Clamp01(burning.SecondsLeft / FireRules.AfterburnSeconds) * 0.8f + 0.2f;
                _look.Burn(feet + Vector3.up * 1.2f, strength, deltaTime);
            }
            foreach (IDamageable gone in _out)
            {
                _burning.Remove(gone);
            }
        }

        /// <summary>Everyone with their feet in a burning cell of this machine's own fire catches, or keeps burning.</summary>
        private void CatchBodies()
        {
            foreach (BurningBody burning in _burning.Values)
            {
                burning.InFire = false;
            }
            if (!_field.TryBurningBounds(out Bounds bounds, false))
            {
                return;
            }
            Vector3 half = bounds.extents + new Vector3(FireRules.CellMetres, 2.5f, FireRules.CellMetres);
            int count = Physics.OverlapBoxNonAlloc(bounds.center + Vector3.up * 1.2f, half, Overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                IDamageable target = Overlaps[i] != null ? Overlaps[i].GetComponentInParent<IDamageable>() : null;
                if (target == null || !target.IsAlive || !(target is Component body))
                {
                    continue;
                }
                Vector3 feet = body.transform.position;
                if (!_field.TryBurningUnder(feet, out FireCell cell) || cell.Cosmetic)
                {
                    continue;
                }
                if (!_burning.TryGetValue(target, out BurningBody burning))
                {
                    burning = new BurningBody { Target = target, Body = body, NextTick = Time.time };
                    _burning[target] = burning;
                }
                burning.InFire = true;
                burning.Cosmetic = false;   // standing in this machine's own fire: burnt here
                burning.SecondsLeft = FireRules.AfterburnSeconds;
                burning.Lighter = LighterOf(cell.Lighter) ?? burning.Lighter;
            }
        }

        /// <summary>A tick of damage to every burning body whose tick is due, and the gasoline under a burning body lit.</summary>
        private void BurnBodies()
        {
            float now = Time.time;
            foreach (BurningBody burning in _burning.Values)
            {
                if (burning.Body == null || burning.Cosmetic || now < burning.NextTick)
                {
                    continue;
                }
                burning.NextTick = now + FireRules.BodyTickSeconds;
                float perSecond = burning.InFire ? FireRules.DamagePerSecond : FireRules.AfterburnDamagePerSecond;
                Vector3 feet = burning.Body.transform.position;
                using (DamageContext.WithoutBlood())   // fire does not cut
                {
                    DamageContext.Strike(burning.Target, perSecond * FireRules.BodyTickSeconds, feet, 0.0f, DamageKind.Magic, 0.0f,
                        feet + Vector3.up * 1.2f, burning.Lighter);
                }
                Ignite(feet, FireRules.SparkIgniteRadius, true, burning.Lighter);
            }
        }
    }
}
