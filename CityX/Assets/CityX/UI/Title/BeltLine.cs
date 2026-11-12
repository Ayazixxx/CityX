using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The characters standing in a row on the conveyor belt, and the belt running under them (docs/CHARACTER_SELECT.md). Slot <c>i</c> stands
    /// <see cref="Spacing"/> metres right of slot <c>i - 1</c>; <see cref="Scroll"/> is where the middle of the screen is along the row, in
    /// slots. Pushed, the belt runs freely at the first boot's speed and pick-up (<see cref="BeltRide"/>), and slows to a stop wherever it is
    /// let go (owner, 2026-11-11: "make the scroll go the same speed as the first boot screen scroll, instead of it locking onto characters
    /// make it so you can freely scroll across"); it never runs past the first or the last. Rolled to a slot (a click, Enter), it glides
    /// that slot to the middle and sets it down there exactly. Pure: the scene reads it (CharacterSelect).
    /// </summary>
    public sealed class BeltLine
    {
        /// <summary>How far apart the characters stand on a 16:9 screen, metres (CharacterSelect.RowSpacing fits it to the screen's shape).</summary>
        public const float WideSpacing = 4.7f;

        /// <summary>Pushed: the first boot's belt (BeltRide).</summary>
        public const float FreeSpeed = BeltRide.Speed;

        public const float FreeAcceleration = BeltRide.Acceleration;

        /// <summary>Gliding one character to the middle: the top speed, m/s, and how fast it gets there and stops, m/s².</summary>
        public const float TopSpeed = 7.0f;

        public const float Acceleration = 16.0f;

        /// <summary>This near its slot the belt has set it down, metres.</summary>
        private const float Arrived = 0.002f;

        private float _push;
        private bool _gliding;

        public BeltLine(int count, int middle, float spacing = WideSpacing)
        {
            Count = Mathf.Max(0, count);
            Target = Mathf.Clamp(middle, 0, Mathf.Max(0, Count - 1));
            Scroll = Target;
            Spacing = Mathf.Max(spacing, 0.5f);
        }

        public int Count { get; }

        /// <summary>How far apart the characters stand, metres.</summary>
        public float Spacing { get; }

        /// <summary>Where the middle of the screen is along the row, in slots: whole with one standing in the middle, between two otherwise.</summary>
        public float Scroll { get; private set; }

        /// <summary>The slot last glided to the middle.</summary>
        public int Target { get; private set; }

        /// <summary>The slot nearest the middle of the screen.</summary>
        public int Nearest => Count == 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(Scroll), 0, Count - 1);

        /// <summary>The belt's speed, m/s, positive to the screen's right (bringing a later slot in from the right runs it left).</summary>
        public float BeltSpeed { get; private set; }

        /// <summary>Gliding a slot to the middle.</summary>
        public bool Gliding => _gliding;

        /// <summary>Stopped, and not on its way to a slot.</summary>
        public bool Settled => BeltSpeed == 0.0f && !_gliding && _push == 0.0f;

        /// <summary>Where slot <paramref name="slot"/> stands, metres from the middle of the screen, positive to its right.</summary>
        public float At(int slot) => (slot - Scroll) * Spacing;

        /// <summary>Glides <paramref name="slot"/> to the middle (clamped to the row).</summary>
        public void RollTo(int slot)
        {
            if (Count > 0)
            {
                Target = Mathf.Clamp(slot, 0, Count - 1);
                _gliding = true;
            }
        }

        /// <summary>
        /// This frame's push: 1 brings the later characters in from the right (the belt runs left), -1 the earlier ones from the left, 0 lets
        /// the belt slow to a stop. A push takes the belt over from a glide.
        /// </summary>
        public void Push(float push)
        {
            _push = Mathf.Clamp(push, -1.0f, 1.0f);
            if (_push != 0.0f)
            {
                _gliding = false;
            }
        }

        /// <summary>One frame. Returns how far the belt's surface ran, metres, positive to the screen's right.</summary>
        public float Step(float deltaTime)
        {
            if (deltaTime <= 0.0f || Count == 0)
            {
                return 0.0f;
            }
            return _gliding ? Glide(deltaTime) : Run(deltaTime);
        }

        private float Glide(float deltaTime)
        {
            // Metres the row still has to run left for the target to stand in the middle.
            float left = (Target - Scroll) * Spacing;
            float wanted = -Mathf.Sign(left) * Mathf.Min(TopSpeed, Mathf.Sqrt(2.0f * Acceleration * Mathf.Abs(left)));
            BeltSpeed = Mathf.MoveTowards(BeltSpeed, wanted, Acceleration * deltaTime);
            float moved = BeltSpeed * deltaTime;
            bool reaches = Mathf.Abs(left) <= Arrived || (left != 0.0f && -moved / left >= 1.0f);
            if (reaches)
            {
                moved = -left;
                BeltSpeed = 0.0f;
                Scroll = Target;
                _gliding = false;
                return moved;
            }
            Scroll -= moved / Spacing;
            return moved;
        }

        private float Run(float deltaTime)
        {
            // Pushed toward an end, no faster than lets it stop at that end.
            float toEnd = _push > 0.0f ? (Count - 1 - Scroll) * Spacing : _push < 0.0f ? Scroll * Spacing : float.MaxValue;
            float wanted = -_push * Mathf.Min(FreeSpeed, Mathf.Sqrt(2.0f * FreeAcceleration * Mathf.Max(0.0f, toEnd)));
            BeltSpeed = Mathf.MoveTowards(BeltSpeed, wanted, FreeAcceleration * deltaTime);
            float moved = BeltSpeed * deltaTime;
            float scroll = Scroll - moved / Spacing;
            float last = Count - 1;
            if (scroll < 0.0f || scroll > last)
            {
                scroll = Mathf.Clamp(scroll, 0.0f, last);
                moved = (Scroll - scroll) * Spacing;
                BeltSpeed = 0.0f;
            }
            Scroll = scroll;
            return moved;
        }
    }
}
