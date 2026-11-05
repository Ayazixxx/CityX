using System.Collections.Generic;
using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// The lamps of one chunk's traffic lights (docs/TRAFFIC_CONTROL.md): one mesh, one draw. Each lamp carries when in its junction's cycle it is lit, and the shader
    /// (CityX/SignalLamp) lights it from the shared clock, so a thousand lights cost the CPU nothing and every machine shows the same one. Plain lists, made on a
    /// worker thread as the chunk's other buffers are.
    /// </summary>
    public sealed class SignalLampBuffer
    {
        /// <summary>What a lamp does besides shine in its window (packed into uv1.z; the shader reads the same bits).</summary>
        public const int FlashesInWindow = 1;

        /// <summary>In late-night flashing operation this lamp is the one that flashes.</summary>
        public const int FlashesAtNight = 2;

        /// <summary>Its junction goes to flashing operation late at night, when every lamp of it but the flashing ones is dark.</summary>
        public const int NightFlashJunction = 4;

        /// <summary>A flat shape (a walk signal's figure), not a round lens.</summary>
        public const int Square = 8;

        /// <summary>Not drawn at all when off, as a walk signal's figures are.</summary>
        public const int HiddenWhenOff = 16;

        /// <summary>The soft glow round a lit lens.</summary>
        public const int Glow = 32;

        public readonly List<Vector3> Vertices = new List<Vector3>();

        /// <summary>uv0: where on the lens (x, y, each 0 to 1), the junction's cycle and its offset, seconds.</summary>
        public readonly List<Vector4> Cycle = new List<Vector4>();

        /// <summary>uv1: when in the cycle the lamp is lit (start, length, seconds), and its flags.</summary>
        public readonly List<Vector4> Window = new List<Vector4>();

        public readonly List<Color32> Colors = new List<Color32>();
        public readonly List<int> Triangles = new List<int>();

        public bool IsEmpty => Triangles.Count == 0;

        public void Reset()
        {
            Vertices.Clear();
            Cycle.Clear();
            Window.Clear();
            Colors.Clear();
            Triangles.Clear();
        }

        /// <summary>One lamp: a quad with its corners in order round it, starting at its lens's (0, 0).</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 colour, SignalTiming timing, SignalWindow window, int flags)
        {
            int start = Vertices.Count;
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
            Vertices.Add(d);
            float cycle = (float)timing.Cycle, offset = (float)timing.Offset;
            Cycle.Add(new Vector4(0.0f, 0.0f, cycle, offset));
            Cycle.Add(new Vector4(1.0f, 0.0f, cycle, offset));
            Cycle.Add(new Vector4(1.0f, 1.0f, cycle, offset));
            Cycle.Add(new Vector4(0.0f, 1.0f, cycle, offset));
            var lit = new Vector4((float)window.Start, (float)window.Length, flags, 0.0f);
            for (int i = 0; i < 4; i++)
            {
                Window.Add(lit);
                Colors.Add(colour);
            }
            Triangles.Add(start);
            Triangles.Add(start + 1);
            Triangles.Add(start + 2);
            Triangles.Add(start);
            Triangles.Add(start + 2);
            Triangles.Add(start + 3);
        }
    }
}
