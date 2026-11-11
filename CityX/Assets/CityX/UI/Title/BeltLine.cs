using UnityEngine;

namespace CityX.UI
{
    /// <summary>
    /// The characters standing in a row on the conveyor belt, and the belt rolling one of them to the middle of the screen
    /// (docs/CHARACTER_SELECT.md). Slot <c>i</c> stands <see cref="Spacing"/> metres right of slot <c>i - 1</c>; <see cref="Scroll"/> is the
    /// slot in the middle, fractional while the belt runs. Rolled to a slot, the belt speeds up, runs, and slows to set that slot down in the
    /// middle exactly. Pure: the scene reads it (CharacterSelect).
    /// </summary>
    public sealed class BeltLine
    {
        /// <summary>How far apart the characters stand on a 16:9 screen, metres (CharacterSelect.RowSpacing fits it to the screen's shape).</summary>
        public const float WideSpacing = 4.7f;

        /// <summary>The belt's top speed rolling from one to the next, m/s, and how fast it gets there and stops, m/s².</summary>
        public const float TopSpeed = 7.0f;

        public const float Acceleration = 16.0f;

        /// <summary>This near its slot the belt has set it down, metres.</summary>
        private const float Arrived = 0.002f;

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

        /// <summary>The slot in the middle of the screen: whole when stopped, between two while the belt runs.</summary>
        public float Scroll { get; private set; }

        /// <summary>The slot the belt is rolling to the middle, or holding there.</summary>
        public int Target { get; private set; }

        /// <summary>The belt's speed, m/s, positive to the screen's right (rolling a later slot in from the right runs it left).</summary>
        public float BeltSpeed { get; private set; }

        /// <summary>Stopped with <see cref="Target"/> in the middle.</summary>
        public bool Settled => BeltSpeed == 0.0f && Scroll == Target;

        /// <summary>Where slot <paramref name="slot"/> stands, metres from the middle of the screen, positive to its right.</summary>
        public float At(int slot) => (slot - Scroll) * Spacing;

        /// <summary>Rolls <paramref name="slot"/> to the middle (clamped to the row).</summary>
        public void RollTo(int slot)
        {
            if (Count > 0)
            {
                Target = Mathf.Clamp(slot, 0, Count - 1);
            }
        }

        /// <summary>One frame. Returns how far the belt's surface ran, metres, positive to the screen's right.</summary>
        public float Step(float deltaTime)
        {
            if (deltaTime <= 0.0f || Count == 0)
            {
                return 0.0f;
            }
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
                return moved;
            }
            Scroll -= moved / Spacing;
            return moved;
        }
    }
}
