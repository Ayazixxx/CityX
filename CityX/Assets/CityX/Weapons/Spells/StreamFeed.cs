using CityX.Character;

namespace CityX.Weapons
{
    /// <summary>
    /// The mana a held stream drinks (the water jet, the flame, the frost spray, the arcane and healing beams), a frame at a time. A stream
    /// that has run dry stays off until there is mana to hold it for <see cref="RestartSeconds"/>, whether the click is kept down or pressed
    /// again: with mana trickling back, it would otherwise start again every few frames.
    /// </summary>
    public struct StreamFeed
    {
        /// <summary>A stream that ran dry runs again once there is mana to hold it this long, seconds.</summary>
        public const float RestartSeconds = 1.0f;

        /// <summary>True from the frame the mana ran out until enough has come back.</summary>
        public bool IsDry { get; private set; }

        /// <summary>
        /// Whether a held stream costing <paramref name="manaPerSecond"/> can run this frame: there is the frame's mana, or, once it has run
        /// dry, the mana of a restart. Nothing is spent; finding too little is what runs it dry. A body without vitals pays nothing.
        /// </summary>
        public bool Ready(PlayerVitals vitals, float manaPerSecond, float deltaTime)
        {
            if (vitals == null)
            {
                return true;
            }
            IsDry = !vitals.HasMana(manaPerSecond * (IsDry ? RestartSeconds : deltaTime));
            return !IsDry;
        }

        /// <summary>Takes a frame of a held stream's mana; false, and nothing taken, when it is not <see cref="Ready"/>.</summary>
        public bool Draw(PlayerVitals vitals, float manaPerSecond, float deltaTime)
        {
            return Ready(vitals, manaPerSecond, deltaTime) && (vitals == null || vitals.TrySpendMana(manaPerSecond * deltaTime));
        }
    }
}
