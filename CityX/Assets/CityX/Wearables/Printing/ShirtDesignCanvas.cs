using System.Collections.Generic;
using UnityEngine;

namespace CityX.Wearables
{
    /// <summary>
    /// THE DESIGN BEING MADE AT THE HEAT PRESS (docs/HEAT_PRESS.md; owner, 2026-11-24: "it should also have an option where you can just draw the
    /// design", "make it use images off your computer"): a front and a back, each <see cref="ShirtDesignArea.Width"/> by
    /// <see cref="ShirtDesignArea.Height"/> pixels (rows bottom up, as Texture2D keeps them). Each side has two layers: what is printed on the
    /// shirt already (<see cref="SetPrinted"/>), which stays (a print is for good: the owner's "the design stays in the game forever"), and the
    /// new ink over it, which the brush, the eraser and placed pictures change and undo takes back. Pure, so it is tested without a screen.
    /// </summary>
    public sealed class ShirtDesignCanvas
    {
        public enum Side { Front, Back }

        public const int Width = ShirtDesignArea.Width;
        public const int Height = ShirtDesignArea.Height;

        /// <summary>How many steps back undo goes.</summary>
        public const int UndoSteps = 30;

        /// <summary>The brush's soft edge, as a share of its radius.</summary>
        public const float SoftEdge = 0.25f;

        private readonly Color32[][] _printed = { new Color32[Width * Height], new Color32[Width * Height] };
        private readonly Color32[][] _ink = { new Color32[Width * Height], new Color32[Width * Height] };
        private readonly List<(Side side, Color32[] before)> _undo = new List<(Side, Color32[])>();
        private readonly List<(Side side, Color32[] before)> _redo = new List<(Side, Color32[])>();

        /// <summary>Goes up with every change, so a screen knows to show it again.</summary>
        public int Version { get; private set; }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Whether any new ink is on either side.</summary>
        public bool HasInk => !IsClear(_ink[0]) || !IsClear(_ink[1]);

        /// <summary>Whether a side shows anything, printed or new.</summary>
        public bool Shows(Side side) => !IsClear(_printed[(int)side]) || !IsClear(_ink[(int)side]);

        /// <summary>Starts again: what is printed on the shirt now (null or empty: nothing), no new ink, no history.</summary>
        public void Begin(Color32[] printedFront, Color32[] printedBack)
        {
            Load(_printed[0], printedFront);
            Load(_printed[1], printedBack);
            System.Array.Clear(_ink[0], 0, _ink[0].Length);
            System.Array.Clear(_ink[1], 0, _ink[1].Length);
            _undo.Clear();
            _redo.Clear();
            Version++;
        }

        private static void Load(Color32[] into, Color32[] from)
        {
            if (from != null && from.Length == into.Length) System.Array.Copy(from, into, into.Length);
            else System.Array.Clear(into, 0, into.Length);
        }

        /// <summary>Remembers a side as it is, before a stroke or a placed picture changes it.</summary>
        public void BeginChange(Side side)
        {
            _undo.Add((side, (Color32[])_ink[(int)side].Clone()));
            if (_undo.Count > UndoSteps) _undo.RemoveAt(0);
            _redo.Clear();
        }

        public void Undo() => Step(_undo, _redo);

        public void Redo() => Step(_redo, _undo);

        private void Step(List<(Side side, Color32[] before)> from, List<(Side side, Color32[] before)> to)
        {
            if (from.Count == 0)
            {
                return;
            }
            (Side side, Color32[] before) = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add((side, (Color32[])_ink[(int)side].Clone()));
            _ink[(int)side] = before;
            Version++;
        }

        /// <summary>Wipes the new ink off a side (the print already on the shirt stays). Undoable.</summary>
        public void ClearInk(Side side)
        {
            BeginChange(side);
            System.Array.Clear(_ink[(int)side], 0, Width * Height);
            Version++;
        }

        /// <summary>
        /// A brush stroke from <paramref name="from"/> to <paramref name="to"/> (pixels) <paramref name="radius"/> pixels round, in
        /// <paramref name="colour"/>; with <paramref name="erase"/>, it takes new ink away instead. Round dabs close enough to make a solid line.
        /// </summary>
        public void Stroke(Side side, Vector2 from, Vector2 to, float radius, Color colour, bool erase)
        {
            radius = Mathf.Max(0.5f, radius);
            float spacing = Mathf.Max(0.5f, radius * 0.3f);
            int dabs = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / spacing));
            for (int i = 0; i <= dabs; i++)
            {
                Dab(_ink[(int)side], Vector2.Lerp(from, to, i / (float)dabs), radius, colour, erase);
            }
            Version++;
        }

        private static void Dab(Color32[] ink, Vector2 at, float radius, Color colour, bool erase)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(at.x - radius)), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(at.x + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(at.y - radius)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(at.y + radius));
            float soft = Mathf.Max(0.5f, radius * SoftEdge);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), at);
                    float cover = Mathf.Clamp01((radius - distance) / soft);
                    if (cover <= 0.0f)
                    {
                        continue;
                    }
                    int i = y * Width + x;
                    if (erase)
                    {
                        Color32 c = ink[i];
                        c.a = (byte)Mathf.RoundToInt(c.a * (1.0f - cover));
                        ink[i] = c;
                    }
                    else
                    {
                        ink[i] = Over(new Color(colour.r, colour.g, colour.b, cover * colour.a), ink[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Lays a picture (rows bottom up) onto a side's new ink, its middle at <paramref name="centre"/> (pixels), <paramref name="width"/>
        /// pixels wide (its height in proportion), turned <paramref name="degrees"/> anticlockwise; its transparency kept. Undoable.
        /// </summary>
        public void Place(Side side, Color32[] picture, int pictureWidth, int pictureHeight, Vector2 centre, float width, float degrees)
        {
            if (picture == null || pictureWidth < 2 || pictureHeight < 2 || width < 1.0f)
            {
                return;
            }
            BeginChange(side);
            Color32[] ink = _ink[(int)side];
            float height = width * pictureHeight / pictureWidth;
            float radians = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            float reach = 0.5f * Mathf.Sqrt(width * width + height * height);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(centre.x - reach)), x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(centre.x + reach));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(centre.y - reach)), y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(centre.y + reach));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - centre.x, dy = y + 0.5f - centre.y;
                    float across = (dx * cos + dy * sin) / width + 0.5f;   // turned back into the picture's own frame
                    float up = (-dx * sin + dy * cos) / height + 0.5f;
                    if (across < 0.0f || across > 1.0f || up < 0.0f || up > 1.0f)
                    {
                        continue;
                    }
                    Color ink1 = ShirtPrintLayout.Sample(picture, pictureWidth, pictureHeight, across, up);
                    if (ink1.a > 0.0f)
                    {
                        ink[y * Width + x] = Over(ink1, ink[y * Width + x]);
                    }
                }
            }
            Version++;
        }

        /// <summary>A side as it will be printed: the new ink over what is printed already.</summary>
        public Color32[] Composite(Side side) => Composite(side, new Color32[Width * Height]);

        /// <summary>The same into <paramref name="shown"/> (a screen showing it as it is drawn makes no garbage).</summary>
        public Color32[] Composite(Side side, Color32[] shown)
        {
            Color32[] printed = _printed[(int)side], ink = _ink[(int)side];
            for (int i = 0; i < shown.Length; i++)
            {
                shown[i] = ink[i].a == 0 ? printed[i] : Over(ink[i], printed[i]);
            }
            return shown;
        }

        /// <summary>The colour shown at a pixel, for the eyedropper.</summary>
        public Color32 ColourAt(Side side, int x, int y)
        {
            int i = Mathf.Clamp(y, 0, Height - 1) * Width + Mathf.Clamp(x, 0, Width - 1);
            Color32 ink = _ink[(int)side][i];
            return ink.a == 0 ? _printed[(int)side][i] : Over(ink, _printed[(int)side][i]);
        }

        /// <summary><paramref name="top"/> laid over <paramref name="under"/> by its transparency (both may be see-through).</summary>
        public static Color32 Over(Color top, Color under)
        {
            float a = top.a + under.a * (1.0f - top.a);
            if (a <= 0.0f)
            {
                return new Color32(0, 0, 0, 0);
            }
            Color mixed = (top * top.a + under * under.a * (1.0f - top.a)) / a;
            mixed.a = a;
            return mixed;
        }

        private static bool IsClear(Color32[] layer)
        {
            foreach (Color32 c in layer)
            {
                if (c.a != 0) return false;
            }
            return true;
        }
    }
}
