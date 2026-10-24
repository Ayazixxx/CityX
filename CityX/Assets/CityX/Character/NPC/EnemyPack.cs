using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE ENEMIES ON THIS MACHINE, AS ONE PACK (owner, 2026-10-24: "make mobs that are hostile to you work together ... like they are aware of
    /// eachothers existance and dont just both push you at the same time completely ignoring eachother and hitting eachother with their attacks
    /// meant for you"). For now every enemy is hostile only to the players (owner: enemies hostile to each other "we will work that out later"),
    /// so the pack is simply every enemy. What the pack gives each of its members:
    ///  - TURNS (<see cref="Turns"/>): one swings at you at a time, two shoot; the rest circle further out or aim, and wait.
    ///  - ROOM: each keeps a step from the others (<see cref="Separation"/>), and circles away from the nearest (<see cref="CrowdSide"/>), so
    ///    they spread round you instead of stacking on one side.
    ///  - A CLEAR LINE: none swings or shoots while a packmate is in the way (<see cref="AllyInTheWay"/>), as the GTA police mods and Shadow of
    ///    War's captains hold fire or step aside.
    ///  - NO FRIENDLY FIRE: a blow one member strikes on another does nothing (DamageContext.Strike asks <see cref="AreAllies"/>), so even a
    ///    swing caught by a packmate who stepped in is harmless. Minecraft's mobs turn on a skeleton whose stray arrow hit them; here they
    ///    stay on your side.
    /// Members join when they wake and leave when they go (SwordsmanBrain, ShootaBrain, CrimsonClaw).
    /// </summary>
    public static class EnemyPack
    {
        /// <summary>Packmates keep about this far apart, metres.</summary>
        public const float PersonalSpaceMetres = 1.8f;

        /// <summary>A packmate this close to the line of a swing or a shot, metres, is in the way.</summary>
        public const float LineClearanceMetres = 0.6f;

        /// <summary>The packmate nearer than this decides which way one circles, metres.</summary>
        public const float CrowdMetres = 5.0f;

        public static readonly AttackTurns Turns = new AttackTurns();

        private static readonly List<Transform> _members = new List<Transform>();

        public static IReadOnlyList<Transform> Members => _members;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ForgetEveryone()
        {
            _members.Clear();
            Turns.Clear();
        }

        public static void Join(Transform body)
        {
            if (body != null && !_members.Contains(body))
            {
                _members.Add(body);
            }
        }

        public static void Leave(Transform body)
        {
            _members.Remove(body);
            Turns.Leave(body);
        }

        /// <summary>The member <paramref name="part"/> is (or is a part of), or null.</summary>
        public static Transform MemberOf(Component part)
        {
            if (part == null)
            {
                return null;
            }
            Transform at = part.transform;
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                Transform member = _members[i];
                if (member == null)
                {
                    _members.RemoveAt(i);
                    continue;
                }
                if (at == member || at.IsChildOf(member))
                {
                    return member;
                }
            }
            return null;
        }

        /// <summary>A blow by <paramref name="striker"/> on <paramref name="target"/> is one packmate's on another (or on itself).</summary>
        public static bool AreAllies(GameObject striker, IDamageable target)
        {
            return striker != null && target is Component hit && MemberOf(striker.transform) != null && MemberOf(hit) != null;
        }

        /// <summary>The push away from packmates closer than <see cref="PersonalSpaceMetres"/>: flat, stronger the closer, up to about 1.</summary>
        public static Vector3 Separation(Transform self)
        {
            Vector3 push = Vector3.zero;
            foreach (Transform member in _members)
            {
                if (member == null || member == self)
                {
                    continue;
                }
                Vector3 away = self.position - member.position;
                away.y = 0.0f;
                float distance = away.magnitude;
                if (distance >= PersonalSpaceMetres)
                {
                    continue;
                }
                Vector3 direction = distance > 1e-3f ? away / distance : Quaternion.Euler(0.0f, member.GetInstanceID() % 360, 0.0f) * Vector3.forward;
                push += direction * (1.0f - distance / PersonalSpaceMetres);
            }
            return Vector3.ClampMagnitude(push, 1.0f);
        }

        /// <summary>
        /// <see cref="Separation"/> as a move for a body facing <paramref name="yawDegrees"/> (LocomotionInput.Move's frame: x to its right, y
        /// ahead).
        /// </summary>
        public static Vector2 SeparationMove(Transform self, float yawDegrees)
        {
            Vector3 push = Separation(self);
            Quaternion facing = Quaternion.Euler(0.0f, yawDegrees, 0.0f);
            return new Vector2(Vector3.Dot(push, facing * Vector3.right), Vector3.Dot(push, facing * Vector3.forward));
        }

        /// <summary>
        /// Which side of <paramref name="self"/> (as it faces <paramref name="target"/>) its nearest packmate is: +1 right, -1 left, 0 none within
        /// <see cref="CrowdMetres"/>. Circling the other way spreads the pack round the target.
        /// </summary>
        public static float CrowdSide(Transform self, Vector3 target)
        {
            Vector3 facing = target - self.position;
            facing.y = 0.0f;
            if (facing.sqrMagnitude < 1e-4f)
            {
                return 0.0f;
            }
            Vector3 right = Vector3.Cross(Vector3.up, facing.normalized);
            float nearest = CrowdMetres;
            float side = 0.0f;
            foreach (Transform member in _members)
            {
                if (member == null || member == self)
                {
                    continue;
                }
                Vector3 toward = member.position - self.position;
                toward.y = 0.0f;
                float distance = toward.magnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    side = Vector3.Dot(toward, right) >= 0.0f ? 1.0f : -1.0f;
                }
            }
            return side;
        }

        /// <summary>A packmate stands across the line from <paramref name="from"/> to <paramref name="to"/> (a swing's or a shot's), not counting <paramref name="self"/>.</summary>
        public static bool AllyInTheWay(Transform self, Vector3 from, Vector3 to)
        {
            Vector3 line = to - from;
            line.y = 0.0f;
            float length = line.magnitude;
            if (length < 1e-3f)
            {
                return false;
            }
            Vector3 along = line / length;
            foreach (Transform member in _members)
            {
                if (member == null || member == self)
                {
                    continue;
                }
                Vector3 offset = member.position - from;
                offset.y = 0.0f;
                float t = Vector3.Dot(offset, along);
                if (t <= 0.2f || t >= length - 0.3f)
                {
                    continue;   // behind the attacker, or past the target
                }
                if ((offset - along * t).magnitude < LineClearanceMetres)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
