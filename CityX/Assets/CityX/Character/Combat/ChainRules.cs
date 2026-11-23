using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE CHAIN'S NUMBERS (docs/CHAIN.md; owner, 2026-11-22: a chain "like delsin's", swung in one hand, worn wrapped round the wrist and down
    /// round the palm "when he is just chilling", and "the chain can appear shorter [in] your hand and then extend really longer for the attacks
    /// it doesnt have to be realistic its just meant to be a cool unique weapon"). All mine.
    /// </summary>
    public static class ChainRules
    {
        /// <summary>A blow's damage before its attack's own scale: a sword blow's (CombatTuning.PunchDamage). It reaches further and takes in more than a blade, so it does not hit harder.</summary>
        public const float BaseDamage = CombatTuning.PunchDamage;

        /// <summary>How far ahead a chain catches a target, as a share of a sword's reach (SoulsSwordCombat.ReachMetres): the longest of the melee weapons.</summary>
        public const float ReachShare = 1.6f;

        /// <summary>The chain swung out, from the fist to its last link, metres: a little longer than the body is tall.</summary>
        public const float SwungLength = 3.0f;

        // ---- on someone down --------------------------------------------------------------------------------------------------------------

        /// <summary>A body lying this near ahead (and nobody standing there) turns the next swing into the downward one, metres.</summary>
        public const float DownedReachMetres = 3.2f;

        /// <summary>To either side of where the swing is aimed, metres.</summary>
        public const float DownedHalfWidth = 1.1f;

        /// <summary>
        /// Whether a swing aimed along <paramref name="yawDegrees"/> from a body at <paramref name="feet"/> should be the downward one: somebody
        /// lies on the ground ahead within reach (limp, or dead with a body still to hit), and nobody there is on their feet (a standing enemy is
        /// swung at as ever, whoever lies at their feet). Owner, 2026-11-22: "similar to how in gta 5 all the melee weapons have their normal
        /// swinging animations but when their is a corpse next to you it automates to the a corpse attack version where it goes downward".
        /// </summary>
        public static bool SomeoneDownAhead(Vector3 feet, float yawDegrees, float bodyHeight, Transform self, LayerMask layers, Collider[] scratch)
        {
            Quaternion facing = Quaternion.Euler(0.0f, yawDegrees, 0.0f);
            Vector3 centre = feet + Vector3.up * (bodyHeight * 0.5f) + facing * Vector3.forward * (DownedReachMetres * 0.5f + 0.15f);
            var half = new Vector3(DownedHalfWidth, bodyHeight * 0.5f, DownedReachMetres * 0.5f);
            int count = Physics.OverlapBoxNonAlloc(centre, half, scratch, facing, layers, QueryTriggerInteraction.Ignore);
            bool down = false;
            for (int i = 0; i < count; i++)
            {
                Collider collider = scratch[i];
                if (collider == null || collider.transform.IsChildOf(self))
                {
                    continue;
                }
                var target = collider.GetComponentInParent<IDamageable>();
                if (!(target is Component body) || body == null)
                {
                    continue;
                }
                if (IsDown(target, body))
                {
                    down = true;
                }
                else if (target.IsAlive)
                {
                    return false;
                }
            }
            return down;
        }

        /// <summary>Lying on the ground: limp, or dead with a body that still takes blows.</summary>
        public static bool IsDown(IDamageable target, Component body)
        {
            if (!target.IsAlive)
            {
                return Dismemberer.TakesCorpseHits(target);
            }
            var ragdoll = body.GetComponent<Ragdoll>();
            return ragdoll != null && ragdoll.IsLimp;
        }
    }
}
