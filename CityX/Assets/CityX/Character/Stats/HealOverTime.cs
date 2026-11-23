using System.Collections.Generic;

namespace CityX.Character
{
    /// <summary>
    /// HEALS GIVEN OUT OVER TIME (a can of PHIZZ: 80 health over 10 seconds, the owner's, docs/PHIZZ_CAN.md). Each heal runs on its own at an even
    /// rate until all of it is given, so two drinks heal side by side. Pure: PlayerVitals ticks it and adds what it gives. A blow does not pause it
    /// (the body's own regeneration waits after a blow; a drink does not).
    /// </summary>
    public sealed class HealOverTime
    {
        private struct Dose
        {
            public float PerSecond;
            public float Left;
        }

        private readonly List<Dose> _doses = new List<Dose>();

        /// <summary>Health still to come from every heal running.</summary>
        public float Remaining
        {
            get
            {
                float left = 0.0f;
                foreach (Dose dose in _doses)
                {
                    left += dose.Left;
                }
                return left;
            }
        }

        public bool Running => _doses.Count > 0;

        /// <summary><paramref name="amount"/> health over <paramref name="seconds"/>.</summary>
        public void Add(float amount, float seconds)
        {
            if (amount <= 0.0f || float.IsNaN(amount))
            {
                return;
            }
            _doses.Add(new Dose { PerSecond = seconds > 0.0f ? amount / seconds : float.PositiveInfinity, Left = amount });
        }

        /// <summary>How much health <paramref name="deltaTime"/> gives.</summary>
        public float Take(float deltaTime)
        {
            if (deltaTime <= 0.0f)
            {
                return 0.0f;
            }
            float given = 0.0f;
            for (int i = _doses.Count - 1; i >= 0; i--)
            {
                Dose dose = _doses[i];
                float now = dose.PerSecond * deltaTime;
                if (now >= dose.Left)
                {
                    given += dose.Left;
                    _doses.RemoveAt(i);
                    continue;
                }
                given += now;
                dose.Left -= now;
                _doses[i] = dose;
            }
            return given;
        }

        /// <summary>Every heal stopped (death).</summary>
        public void Clear()
        {
            _doses.Clear();
        }
    }
}
