using System.Collections.Generic;
using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// A ROCKET LAUNCHER'S SHOT (docs/RPG.md): its loaded round leaves the muzzle as a rocket (FlyingRocket) toward the crosshair's point, through
    /// the body's RocketLauncher so other players see it fly and burst, and the back-blast roars out of the blast cone behind: anyone close
    /// behind is hurt and thrown (RocketRules), dust kicked up off the ground there. Another player's copy shows the round gone and the
    /// back-blast, hurting no one.
    /// </summary>
    public sealed partial class PistolWeapon
    {
        private static readonly Collider[] BackblastOverlaps = new Collider[32];
        private static readonly HashSet<IDamageable> BackblastStruck = new HashSet<IDamageable>();

        /// <summary>The round flies from where it sits at the muzzle toward what the crosshair is on (or far along its ray). Returns that point.</summary>
        private Vector3 LaunchRocket(Vector3 origin, Vector3 direction, GameObject wielder)
        {
            ShotTrace trace = ResolveTrace(origin, direction, wielder);
            Vector3 aimPoint = trace.Hit ? trace.HitInfo.point : origin + direction * Profile.Range;
            Vector3 from = MagazinePart != null ? MagazinePart.position : MuzzlePoint;
            Vector3 way = aimPoint - from;
            if (way.sqrMagnitude < 1.0f || Vector3.Dot(way, direction) <= 0.0f)
            {
                way = direction;   // the point is at the muzzle or behind it: straight down the crosshair's ray
            }
            GameObject look = MagazinePart != null ? MagazinePart.gameObject : null;
            RocketLauncher launcher = wielder != null ? wielder.GetComponent<RocketLauncher>() : null;
            if (launcher != null)
            {
                launcher.Launch(look, from, way.normalized);
            }
            else
            {
                FlyingRocket.Launch(look, from, way.normalized, wielder, false);
            }
            if (MagazinePart != null)
            {
                MagazinePart.gameObject.SetActive(false);
            }
            Backblast(wielder);
            return aimPoint;
        }

        /// <summary>The back-blast: everyone close behind the launcher (not its wielder) hurt and thrown, more the closer and the straighter behind.</summary>
        private void Backblast(GameObject wielder)
        {
            Vector3 rear = transform.TransformPoint(Profile.BackblastLocal);
            Vector3 forward = transform.forward;
            BackblastStruck.Clear();
            int count = Physics.OverlapSphereNonAlloc(rear, RocketRules.BackblastMetres, BackblastOverlaps, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                IDamageable target = BackblastOverlaps[i] != null ? BackblastOverlaps[i].GetComponentInParent<IDamageable>() : null;
                if (target == null || !target.IsAlive || !(target is Component body)
                    || (wielder != null && body.transform.IsChildOf(wielder.transform)) || !BackblastStruck.Add(target))
                {
                    continue;
                }
                Vector3 chest = body.transform.position + Vector3.up * BackblastChestHeight;
                float share = RocketRules.BackblastShare(chest - rear, forward);
                if (share <= 0.0f)
                {
                    continue;
                }
                HitResponse response = DamageContext.Strike(target, RocketRules.BackblastDamage * share, rear, 0.4f, DamageKind.Physical, 0.0f, chest, wielder);
                if (response == HitResponse.Landed && target.IsAlive)
                {
                    Vector3 away = chest - rear;
                    away.y = 0.0f;
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : -forward;
                    Throws.Limp(target, (away * RocketRules.BackblastThrowSpeed + Vector3.up * BackblastLift) * share);
                }
            }
            ShowBackblast(rear, -forward);
        }

        /// <summary>A person's chest above their feet (the 2.6 m body), where the back-blast is measured to.</summary>
        private const float BackblastChestHeight = 1.2f;

        /// <summary>How much the back-blast lifts a body it throws, m/s at full share.</summary>
        private const float BackblastLift = 3.0f;

        /// <summary>On another player's copy: the round gone from the muzzle, and the back-blast's dust and roar.</summary>
        private void ShowRocketLeaving()
        {
            if (MagazinePart != null)
            {
                MagazinePart.gameObject.SetActive(false);
            }
            ShowBackblast(transform.TransformPoint(Profile.BackblastLocal), -transform.forward);
        }

        private static void ShowBackblast(Vector3 rear, Vector3 back)
        {
            if (Physics.Raycast(rear, Vector3.down, out RaycastHit ground, 3.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                DustBurst.Roll(ground.point + new Vector3(back.x, 0.0f, back.z).normalized * 1.2f, 2.2f, 1.0f);
            }
            if (Application.isPlaying)
            {
                AudioSource.PlayClipAtPoint(RocketSounds.Whoosh, rear, 0.9f);
            }
        }
    }
}
