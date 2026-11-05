using CityX.World;
using CityX.World.Plan;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The hardware of a junction (docs/TRAFFIC_CONTROL.md): the STOP sign, and a traffic light's pole, mast arm, heads and walk signals, drawn in boxes as the city's
    /// other street furniture is. Sizes are the American standard's (MUTCD) at the city's 1.3 times life.
    /// </summary>
    public static class TrafficProps
    {
        /// <summary>A STOP sign across its flats: 30 in, and 36 in facing more than one lane (MUTCD 2B.03).</summary>
        public const float StopSignSize = 1.0f;

        public const float WideStopSignSize = 1.2f;

        /// <summary>The bottom of a sign over the pavement: 7 ft where people walk (MUTCD 2A.18).</summary>
        public const float SignBottom = 2.8f;

        /// <summary>A light's head: three 12 in lenses one over another (MUTCD 4D.07), red on top.</summary>
        public const float HeadWidth = 0.46f, HeadHeight = 1.4f, HeadDepth = 0.3f;

        public const float LensRadius = 0.18f;

        /// <summary>The mast arm's height: its heads hang with their bottoms 15 ft or more over the road (MUTCD 4D.15).</summary>
        public const float ArmHeight = 7.6f;

        public const float ArmedPoleHeight = 8.0f, PoleHeight = 5.9f;

        /// <summary>The bottom of a head on the pole's side: 8 to 19 ft (MUTCD 4D.16); and of a walk signal: 7 to 10 ft (4E.05).</summary>
        public const float SideHeadBottom = 4.2f, WalkHeadBottom = 3.0f;

        public const float WalkHeadSize = 0.52f;

        private const float PoleHalf = 0.12f;
        private const float GlowRadius = 0.6f;

        /// <summary>The look of every pole, post and arm, and of a STOP sign's face: what the tests find them by.</summary>
        public static readonly Color32 Galvanised = SurfaceLook.Pack(new Color(0.56f, 0.58f, 0.60f), (int)WallMaterial.Metal, 0.25f);

        public static readonly Color32 StopRed = SurfaceLook.Pack(new Color(0.93f, 0.09f, 0.07f), (int)WallMaterial.Plaster, 0.0f);
        private static readonly Color32 Housing = SurfaceLook.Pack(new Color(0.09f, 0.10f, 0.10f), (int)WallMaterial.Metal, 0.2f);
        private static readonly Color32 SignBack = SurfaceLook.Pack(new Color(0.62f, 0.64f, 0.66f), (int)WallMaterial.Metal, 0.2f);

        private static readonly Color32 Red = new Color32(255, 45, 35, 255);
        private static readonly Color32 Yellow = new Color32(255, 190, 25, 255);
        private static readonly Color32 Green = new Color32(45, 255, 150, 255);
        private static readonly Color32 HandOrange = new Color32(255, 125, 25, 255);
        private static readonly Color32 WalkWhite = new Color32(235, 245, 255, 255);

        /// <summary>
        /// A frame standing at a point and facing a way along the ground: what is built in it is given by how far to the right of someone looking at its face, how
        /// far up, and how far out toward them. (A MeshBuffer's own frame has its +x on that viewer's LEFT: a sign lettered in it reads mirrored.)
        /// </summary>
        private readonly struct Facing
        {
            private readonly Vector3 _at;
            private readonly float _cos, _sin;

            public Facing(Vector3 at, Vec2 toward)
            {
                _at = at;
                _cos = (float)toward.Y;
                _sin = (float)-toward.X;
            }

            public void Set(MeshBuffer b) => b.SetFrame(_at, _cos, _sin);

            /// <summary>The same point as the buffer's frame makes of (-right, up, out).</summary>
            public Vector3 Point(float right, float up, float toward)
            {
                float x = -right;
                return new Vector3(x * _cos - toward * _sin + _at.x, up + _at.y, x * _sin + toward * _cos + _at.z);
            }
        }

        /// <summary>A box in the set frame, by its extents to the viewer's right, up and toward them.</summary>
        private static void Box(MeshBuffer b, float right0, float right1, float up0, float up1, float out0, float out1)
        {
            b.AddBox(new Vector3(-right1, up0, out0), new Vector3(-right0, up1, out1), true);
        }

        /// <summary>A flat rectangle facing the viewer, in the set frame.</summary>
        private static void Plate(MeshBuffer b, float right0, float up0, float right1, float up1, float toward)
        {
            b.AddQuad(new Vector3(-right0, up0, toward), new Vector3(-right1, up0, toward), new Vector3(-right1, up1, toward), new Vector3(-right0, up1, toward),
                Vector3.forward, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        }

        // ---------------------------------------------------------------- the STOP sign

        /// <summary>A STOP sign on its post at <paramref name="at"/> (the pavement), its face toward <paramref name="facing"/>; an ALL WAY plate under it at an all-way stop.</summary>
        public static void AddStopSign(MeshBuffer b, Vector3 at, Vec2 facing, float size, bool allWay)
        {
            Color32 saved = b.Look;
            new Facing(at, facing).Set(b);
            float middle = SignBottom + size * 0.5f;

            b.Look = Galvanised;
            Box(b, -0.04f, 0.04f, -0.3f, SignBottom + size * 0.92f, -0.09f, -0.01f);
            b.Look = SignBack;
            Octagon(b, size * 0.5f, middle, -0.008f, false);
            b.Look = CityProps.White;
            Octagon(b, size * 0.5f, middle, 0.008f, true);
            b.Look = StopRed;
            Octagon(b, size * 0.5f * 0.92f, middle, 0.016f, true);
            b.Look = CityProps.White;
            Letters(b, middle, size, 0.024f);

            if (allWay)
            {
                // The ALL WAY plate (R1-3P): a red plate, white-edged, under the sign.
                float half = size * 0.3f, top = SignBottom - 0.06f, bottom = top - size * 0.2f;
                b.Look = SignBack;
                b.AddQuad(new Vector3(-half, bottom, -0.008f), new Vector3(half, bottom, -0.008f), new Vector3(half, top, -0.008f), new Vector3(-half, top, -0.008f),
                    Vector3.back, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                b.Look = CityProps.White;
                Plate(b, -half, bottom, half, top, 0.008f);
                b.Look = StopRed;
                Plate(b, -half + 0.03f, bottom + 0.03f, half - 0.03f, top - 0.03f, 0.016f);
                b.Look = CityProps.White;
                Plate(b, -half * 0.7f, (top + bottom) * 0.5f - 0.02f, half * 0.7f, (top + bottom) * 0.5f + 0.02f, 0.024f);
            }
            b.Look = saved;
            b.ClearFrame();
        }

        /// <summary>An octagon <paramref name="apothem"/> from its middle to each flat, facing the viewer or away.</summary>
        private static void Octagon(MeshBuffer b, float apothem, float middle, float toward, bool front)
        {
            float radius = apothem / Mathf.Cos(Mathf.PI / 8.0f);
            var centre = new Vector3(0.0f, middle, toward);
            Vector3 normal = front ? Vector3.forward : Vector3.back;
            for (int k = 0; k < 8; k++)
            {
                float a0 = Mathf.PI / 8.0f + k * Mathf.PI / 4.0f, a1 = a0 + Mathf.PI / 4.0f;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, middle + Mathf.Sin(a0) * radius, toward);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, middle + Mathf.Sin(a1) * radius, toward);
                b.AddTriangle(centre, p0, p1, normal, Vector2.zero, Vector2.zero, Vector2.zero);
            }
        }

        /// <summary>STOP, in strokes: a third of the sign tall, as the standard's lettering is.</summary>
        private static void Letters(MeshBuffer b, float middle, float size, float toward)
        {
            float h = size * 0.34f, t = h * 0.2f, w = h * 0.56f, gap = h * 0.2f;
            float left = -(4.0f * w + 3.0f * gap) * 0.5f, bottom = middle - h * 0.5f;
            void Stroke(int letter, float x0, float y0, float x1, float y1)
            {
                float x = left + letter * (w + gap);
                Plate(b, x + x0, bottom + y0, x + x1, bottom + y1, toward);
            }
            // S
            Stroke(0, 0.0f, h - t, w, h);
            Stroke(0, 0.0f, (h - t) * 0.5f, w, (h + t) * 0.5f);
            Stroke(0, 0.0f, 0.0f, w, t);
            Stroke(0, 0.0f, h * 0.5f, t, h);
            Stroke(0, w - t, 0.0f, w, h * 0.5f);
            // T
            Stroke(1, 0.0f, h - t, w, h);
            Stroke(1, (w - t) * 0.5f, 0.0f, (w + t) * 0.5f, h);
            // O
            Stroke(2, 0.0f, 0.0f, t, h);
            Stroke(2, w - t, 0.0f, w, h);
            Stroke(2, 0.0f, h - t, w, h);
            Stroke(2, 0.0f, 0.0f, w, t);
            // P
            Stroke(3, 0.0f, 0.0f, t, h);
            Stroke(3, 0.0f, h - t, w, h);
            Stroke(3, 0.0f, (h - t) * 0.5f, w, (h + t) * 0.5f);
            Stroke(3, w - t, h * 0.5f, w, h);
        }

        // ---------------------------------------------------------------- the lights

        /// <summary>A light's pole on its footing, at <paramref name="at"/> (the pavement).</summary>
        public static void AddPole(MeshBuffer b, Vector3 at, bool armed)
        {
            Color32 saved = b.Look;
            b.Look = CityProps.Concrete;
            b.AddBox(at + new Vector3(-0.3f, -0.3f, -0.3f), at + new Vector3(0.3f, 0.12f, 0.3f));
            b.Look = Galvanised;
            b.AddBox(at + new Vector3(-PoleHalf, 0.12f, -PoleHalf), at + new Vector3(PoleHalf, armed ? ArmedPoleHeight : PoleHeight, PoleHalf));
            b.Look = saved;
        }

        /// <summary>The mast arm from a pole out over the road along <paramref name="along"/>.</summary>
        public static void AddMastArm(MeshBuffer b, Vector3 pole, Vec2 along, float length)
        {
            Color32 saved = b.Look;
            b.Look = Galvanised;
            b.SetFrame(pole, along.X, along.Y);
            b.AddBox(new Vector3(0.0f, ArmHeight - 0.09f, -0.09f), new Vector3(length, ArmHeight + 0.09f, 0.09f), true);
            // The brace from lower on the pole up to the arm.
            b.AddBox(new Vector3(0.0f, ArmHeight - 0.75f, -0.05f), new Vector3(0.9f, ArmHeight - 0.65f, 0.05f), true);
            b.Look = saved;
            b.ClearFrame();
        }

        /// <summary>
        /// A head, its bottom's middle at <paramref name="at"/>, its lenses toward <paramref name="facing"/>: red, yellow and green, each lit when the cycle says so
        /// for the main street's traffic or the side street's. A head over the road has a backplate.
        /// </summary>
        public static void AddHead(MeshBuffer b, SignalLampBuffer lamps, Vector3 at, Vec2 facing, bool backplate, SignalTiming timing, bool major)
        {
            Color32 saved = b.Look;
            var frame = new Facing(at, facing);
            frame.Set(b);
            b.Look = Housing;
            Box(b, -HeadWidth * 0.5f, HeadWidth * 0.5f, 0.0f, HeadHeight, -HeadDepth, 0.0f);
            if (backplate)
            {
                Box(b, -HeadWidth * 0.5f - 0.2f, HeadWidth * 0.5f + 0.2f, -0.16f, HeadHeight + 0.16f, -HeadDepth * 0.6f, -HeadDepth * 0.6f + 0.03f);
            }
            float section = HeadHeight / 3.0f;
            for (int i = 0; i < 3; i++)
            {
                var lamp = (SignalLamp)i;   // red on top
                float middle = HeadHeight - section * (i + 0.5f);
                // The visor over the lens.
                Box(b, -LensRadius - 0.03f, LensRadius + 0.03f, middle + LensRadius, middle + LensRadius + 0.03f, 0.0f, 0.22f);

                int flags = 0;
                if (timing.NightFlash)
                {
                    flags |= SignalLampBuffer.NightFlashJunction;
                    if (lamp == (major ? SignalLamp.Yellow : SignalLamp.Red))
                    {
                        flags |= SignalLampBuffer.FlashesAtNight;
                    }
                }
                Color32 colour = lamp == SignalLamp.Red ? Red : lamp == SignalLamp.Yellow ? Yellow : Green;
                SignalWindow window = timing.Vehicle(major, lamp);
                Disc(lamps, frame, middle, LensRadius, 0.012f, colour, timing, window, flags);
                Disc(lamps, frame, middle, GlowRadius, 0.03f, colour, timing, window, flags | SignalLampBuffer.Glow);
            }
            b.Look = saved;
            b.ClearFrame();
        }

        private static void Disc(SignalLampBuffer lamps, in Facing frame, float middle, float radius, float toward, Color32 colour, SignalTiming timing,
            SignalWindow window, int flags)
        {
            lamps.AddQuad(frame.Point(-radius, middle - radius, toward), frame.Point(radius, middle - radius, toward),
                frame.Point(radius, middle + radius, toward), frame.Point(-radius, middle + radius, toward), colour, timing, window, flags);
        }

        /// <summary>
        /// A walk signal, its bottom's middle at <paramref name="at"/>, its face toward <paramref name="facing"/>: the white walking figure, and the orange hand that
        /// flashes while there is still time to finish crossing and then holds. For those crossing the main street, or the side street.
        /// </summary>
        public static void AddWalkHead(MeshBuffer b, SignalLampBuffer lamps, Vector3 at, Vec2 facing, SignalTiming timing, bool crossesMain)
        {
            Color32 saved = b.Look;
            var frame = new Facing(at, facing);
            frame.Set(b);
            b.Look = Housing;
            float half = WalkHeadSize * 0.5f;
            Box(b, -half, half, 0.0f, WalkHeadSize, -0.22f, 0.0f);
            Box(b, -half, half, WalkHeadSize, WalkHeadSize + 0.03f, 0.0f, 0.16f);
            b.Look = saved;
            b.ClearFrame();

            int flags = SignalLampBuffer.Square | SignalLampBuffer.HiddenWhenOff | (timing.NightFlash ? SignalLampBuffer.NightFlashJunction : 0);
            float unit = WalkHeadSize * 0.36f;
            Hand(lamps, frame, half, unit, timing, timing.Pedestrian(crossesMain, WalkLamp.Hand), flags);
            Hand(lamps, frame, half, unit, timing, timing.Pedestrian(crossesMain, WalkLamp.FlashingHand), flags | SignalLampBuffer.FlashesInWindow);
            Walker(lamps, frame, half, unit, timing, timing.Pedestrian(crossesMain, WalkLamp.Walk), flags);
        }

        private const float FigureOut = 0.012f;

        /// <summary>The upraised hand: a palm, four fingers and a thumb.</summary>
        private static void Hand(SignalLampBuffer lamps, Facing frame, float middle, float unit, SignalTiming timing, SignalWindow window, int flags)
        {
            void Part(float x0, float y0, float x1, float y1)
            {
                lamps.AddQuad(frame.Point(x0 * unit, middle + y0 * unit, FigureOut), frame.Point(x1 * unit, middle + y0 * unit, FigureOut),
                    frame.Point(x1 * unit, middle + y1 * unit, FigureOut), frame.Point(x0 * unit, middle + y1 * unit, FigureOut), HandOrange, timing, window, flags);
            }
            Part(-0.45f, -0.9f, 0.45f, -0.05f);
            Part(-0.45f, -0.05f, -0.27f, 0.62f);
            Part(-0.21f, -0.05f, -0.03f, 0.85f);
            Part(0.03f, -0.05f, 0.21f, 0.9f);
            Part(0.27f, -0.05f, 0.45f, 0.72f);
            Part(-0.8f, -0.6f, -0.52f, -0.1f);
        }

        /// <summary>The walking figure: a head, a leaning body, an arm ahead and two legs astride.</summary>
        private static void Walker(SignalLampBuffer lamps, Facing frame, float middle, float unit, SignalTiming timing, SignalWindow window, int flags)
        {
            void Limb(float fromX, float fromY, float toX, float toY, float width)
            {
                float halfWidth = width * 0.5f;
                lamps.AddQuad(frame.Point((fromX - halfWidth) * unit, middle + fromY * unit, FigureOut), frame.Point((fromX + halfWidth) * unit, middle + fromY * unit, FigureOut),
                    frame.Point((toX + halfWidth) * unit, middle + toY * unit, FigureOut), frame.Point((toX - halfWidth) * unit, middle + toY * unit, FigureOut),
                    WalkWhite, timing, window, flags);
            }
            Limb(0.12f, 0.58f, 0.12f, 0.9f, 0.3f);      // the head
            Limb(-0.05f, -0.12f, 0.1f, 0.5f, 0.34f);    // the body
            Limb(-0.02f, -0.1f, 0.42f, -0.9f, 0.2f);    // the leg ahead
            Limb(-0.08f, -0.1f, -0.5f, -0.9f, 0.2f);    // the leg behind
            Limb(0.16f, 0.42f, 0.55f, 0.02f, 0.15f);    // the arm ahead
        }
    }
}
