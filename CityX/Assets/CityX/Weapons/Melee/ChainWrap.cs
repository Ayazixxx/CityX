using CityX.Character;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE CHAIN'S LINKS AND WHERE THEY LIE WOUND ON THE ARM (docs/CHAIN.md). Owner, 2026-11-22: "i like the way [Delsin's] chain deploys from
    /// being wrapped around his wrist and i want to do something similar in our game but have it also wrap around down to around the palm when
    /// he is just chilling", with a sketch: bands round the forearm above the wrist, a loose end ring at the top, and bands on down over the
    /// hand, the thumb and the fingers free. And for the links, two photographs of plain steel chain: "make it something like those two".
    ///
    /// TWO COILS. The WRIST'S, round the forearm just above the wrist, is always there: it is what holds the chain on, swung or not. The HAND'S,
    /// round the palm, is the far end of the free length: wound on it at rest, off it and out through the fist to swing. The hand's turns slant,
    /// above the thumb's root on its side and down toward the knuckles on the little finger's, so the thumb comes out under them as in the sketch.
    ///
    /// Pure: each link's place and turn in its bone's own frame, worked out once. The right arm's (the game's T-pose skeleton: a bone's axes are
    /// the body's own with the arms out, so +X runs down the arm to the hand; in the hand's frame +Y is the back of the hand and +Z the thumb's
    /// side, FusedBodyShape.Digits).
    /// </summary>
    public static class ChainWrap
    {
        // ---- the link: a long oval of round bar, each turned a quarter turn from the last ----

        public const float LinkLength = 0.10f;
        public const float LinkWidth = 0.058f;
        public const float BarRadius = 0.0085f;

        /// <summary>From one link's middle to the next with the chain pulled tight, metres: a link's length less the two bars that cross inside it at each end.</summary>
        public const float Pitch = LinkLength - 4.0f * BarRadius;

        /// <summary>The free length's links: <see cref="ChainRules.SwungLength"/> of them.</summary>
        public static readonly int FreeLinks = Mathf.RoundToInt(ChainRules.SwungLength / Pitch);

        /// <summary>The wrist's coil: two and a half turns.</summary>
        public const int WristLinks = 21;

        /// <summary>How many of the free length's links are wound round the hand at rest (its far end). The rest are in the fist.</summary>
        public static int PalmLinks
        {
            get
            {
                Build();
                return _palmAt.Length;
            }
        }

        public static int TotalLinks => WristLinks + FreeLinks;

        /// <summary>A wound link's middle stands this far off the skin, metres: half of them lie flat on it, half stand on edge.</summary>
        public const float OffSkin = 0.016f;

        /// <summary>Where the fist holds the free length, in the hand's frame: the middle of the closed fingers, under the palm.</summary>
        public static readonly Vector3 GripInHand = new Vector3(0.085f, -0.065f, 0.02f);

        // ---- the wrist's coil, in the forearm's frame, from the wrist joint ----

        private const float WristCoilRadius = FusedBodyShape.WristRadius + 0.002f + OffSkin;
        private const float WristTurnRise = 0.055f;
        private const float WristCoilEndsAbove = 0.03f;

        // ---- the hand's coil, in the hand's frame ----

        // First the slanted turns, down from the wrist: above the thumb's root on its side, toward the knuckles on the little finger's. Then
        // STRAIGHT ROUND THE PALM under the thumb, through the web between it and the first finger (owner, 2026-11-22, with a line drawn
        // across the palm: "can we have it do one or maybe two loops around the palm like the red line"). The chain goes from the one to the
        // other on the little finger's side, where both are at the same height.
        private const float PalmSlantedTurns = 1.5f;

        /// <summary>How many times it goes straight round the palm: once (owner, 2026-11-22, shown one and two: "two is too much just do the 1 loops around the palm").</summary>
        public const float PalmLoops = 1.0f;

        private const float PalmStartsAt = -0.015f;
        private const float PalmTurnRise = 0.012f;
        private const float PalmSlant = 0.075f;
        private const float PalmSlantGrowth = 0.012f;
        private const float PalmLoopsAt = 0.116f;
        private const float PalmLoopRise = 0.014f;
        private const float PalmLoopsTakenUpOver = 0.7f;
        private const float PalmFromWristOver = 0.05f;
        private const float PalmSquareness = 3.2f;


        private static Vector3[] _wristAt, _palmAt;
        private static Quaternion[] _wristTurn, _palmTurn;

        /// <summary>Wrist-coil link <paramref name="index"/> (0 the loose end up the forearm) in the forearm's frame, <paramref name="wrist"/> being the wrist joint there.</summary>
        public static void WristLink(int index, Vector3 wrist, out Vector3 position, out Quaternion rotation)
        {
            Build();
            position = wrist + _wristAt[index];
            rotation = _wristTurn[index];
        }

        /// <summary>Hand-coil link <paramref name="slot"/> (0 next to the wrist's coil, the last the chain's far end) in the hand's frame.</summary>
        public static void PalmLink(int slot, out Vector3 position, out Quaternion rotation)
        {
            Build();
            position = _palmAt[slot];
            rotation = _palmTurn[slot];
        }

        /// <summary>
        /// How much hand-coil link <paramref name="slot"/> is still wound on with the coil <paramref name="wound"/> of the way on (1 all on, 0
        /// all off), 0 to 1: the far end comes off first and goes on last, link after link.
        /// </summary>
        public static float Wound(float wound, int slot)
        {
            float along = slot / (float)(PalmLinks - 1);
            return Mathf.Clamp01(wound * (1.0f + UnwindSpread) - UnwindSpread * along);
        }

        /// <summary>How much of the winding is spent on the links going on one after another rather than together.</summary>
        public const float UnwindSpread = 1.5f;

        /// <summary>A link's turn: its length along <paramref name="along"/>, and lying flat on <paramref name="outward"/> or, every other one, standing on edge.</summary>
        public static Quaternion LinkTurn(Vector3 along, Vector3 outward, int index)
        {
            Vector3 flat = outward - along * Vector3.Dot(outward, along);
            if (flat.sqrMagnitude < 1e-8f)
            {
                flat = Vector3.Cross(along, Mathf.Abs(along.y) < 0.9f ? Vector3.up : Vector3.right);
            }
            flat.Normalize();
            return Quaternion.LookRotation(along, (index & 1) == 0 ? flat : Vector3.Cross(along, flat));
        }

        private static void Build()
        {
            if (_wristAt != null)
            {
                return;
            }
            var wristAt = new Vector3[WristLinks];
            var wristTurn = new Quaternion[WristLinks];
            float around = 2.0f * Mathf.PI * WristCoilRadius;
            float turns = (WristLinks - 1) * Pitch / around;
            float lastAngle = 0.0f;
            for (int i = 0; i < WristLinks; i++)
            {
                float turn = i * Pitch / around;
                float angle = turn * 2.0f * Mathf.PI;
                // A hand-wound coil is not a spring: each link sits a little in or out and up or down of the perfect turn. The loose end stands off.
                float radius = WristCoilRadius + (Wobble(i, 1) - 0.5f) * 0.008f + (i == 0 ? 0.03f : i == 1 ? 0.012f : 0.0f);
                float x = -WristCoilEndsAbove - (turns - turn) * WristTurnRise + (Wobble(i, 2) - 0.5f) * 0.012f;
                var outward = new Vector3(0.0f, Mathf.Cos(angle), Mathf.Sin(angle));
                var along = new Vector3(WristTurnRise / around, -Mathf.Sin(angle), Mathf.Cos(angle)).normalized;
                wristAt[i] = new Vector3(x, 0.0f, 0.0f) + outward * radius;
                wristTurn[i] = LinkTurn(along, outward, i);
                lastAngle = angle;
            }

            // The hand's coil carries on round the same way from where the wrist's ends, marched a link's pitch at a time round the hand's
            // own section: round at the wrist, a wide flat box at the palm.
            // The slanted turns end, and the straight ones begin, on the little finger's side (sin = -1) of the turn after PalmSlantedTurns.
            const float littleFingerSide = 1.5f * Mathf.PI;
            float loopsFrom = littleFingerSide + 2.0f * Mathf.PI * Mathf.Ceil((lastAngle + 2.0f * Mathf.PI * PalmSlantedTurns - littleFingerSide) / (2.0f * Mathf.PI));
            float endsAt = loopsFrom + 2.0f * Mathf.PI * PalmLoops;
            var palmAt = new System.Collections.Generic.List<Vector3>();
            var palmTurn = new System.Collections.Generic.List<Quaternion>();
            float angleNow = lastAngle;
            Vector3 last = PalmPoint(angleNow, lastAngle, loopsFrom, out _);
            while (angleNow < endsAt && palmAt.Count < FreeLinks)
            {
                Vector3 point = last;
                Vector3 outward = Vector3.up;
                float gone = 0.0f;
                while (gone < Pitch)
                {
                    angleNow += 0.004f;
                    Vector3 next = PalmPoint(angleNow, lastAngle, loopsFrom, out outward);
                    gone += Vector3.Distance(point, next);
                    point = next;
                }
                palmTurn.Add(LinkTurn((point - last).normalized, outward, palmAt.Count + WristLinks));
                palmAt.Add(point);
                last = point;
            }
            _wristTurn = wristTurn;
            _palmAt = palmAt.ToArray();
            _palmTurn = palmTurn.ToArray();
            _wristAt = wristAt;
        }

        /// <summary>
        /// A point of the hand's coil at <paramref name="angle"/> round the hand (from the back of the hand toward the thumb's side), and the way
        /// out from the hand there; the coil starts at <paramref name="startAngle"/> and goes straight round the palm from <paramref name="loopsFrom"/>.
        /// </summary>
        private static Vector3 PalmPoint(float angle, float startAngle, float loopsFrom, out Vector3 outward)
        {
            float turn = Mathf.Max(0.0f, (angle - startAngle) / (2.0f * Mathf.PI));
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            // Above the thumb's root on the thumb's side (s = 1), slanting down toward the knuckles on the little finger's.
            float slanted = PalmStartsAt + turn * PalmTurnRise + (PalmSlant + PalmSlantGrowth * turn) * (1.0f - s) * 0.5f;
            float straight = PalmLoopsAt + PalmLoopRise * Mathf.Max(0.0f, (angle - loopsFrom) / (2.0f * Mathf.PI));
            float x = Mathf.Lerp(slanted, straight, Mathf.SmoothStep(0.0f, 1.0f, (angle - (loopsFrom - PalmLoopsTakenUpOver)) / (2.0f * PalmLoopsTakenUpOver)));
            float palm = Mathf.SmoothStep(0.0f, 1.0f, x / PalmFromWristOver);
            float halfThick = Mathf.Lerp(FusedBodyShape.WristRadius, FusedBodyShape.PalmHalfThickness, palm) + OffSkin;
            float halfWide = Mathf.Lerp(FusedBodyShape.WristRadius, FusedBodyShape.PalmHalfWidth, palm) + OffSkin;
            float power = 2.0f / Mathf.Lerp(2.0f, PalmSquareness, palm);
            float y = halfThick * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), power);
            float z = halfWide * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), power);
            outward = new Vector3(0.0f, y / (halfThick * halfThick), z / (halfWide * halfWide)).normalized;
            return new Vector3(x, y, z);
        }

        /// <summary>A number from 0 to 1 that is always the same for the same link: the coil's unevenness.</summary>
        private static float Wobble(int index, int salt)
        {
            float v = Mathf.Sin(index * 12.9898f + salt * 78.233f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }
    }
}
