using System.Collections.Generic;
using CityX.Character;

namespace CityX.Weapons
{
    /// <summary>
    /// When each body in a held stream (the water jet, the flame) is next due a tick. It outlasts the stream: one let go and started again
    /// hits a body no sooner than one held on it would.
    /// </summary>
    public sealed class StreamTicks
    {
        private readonly Dictionary<IDamageable, float> _next = new Dictionary<IDamageable, float>();
        private readonly List<IDamageable> _past = new List<IDamageable>();

        /// <summary>How many bodies still have a tick to wait for.</summary>
        public int Waiting => _next.Count;

        /// <summary>Whether <paramref name="target"/> is due a tick at <paramref name="now"/>; if it is, its next is <paramref name="tickSeconds"/> on.</summary>
        public bool Due(IDamageable target, float now, float tickSeconds)
        {
            if (_next.TryGetValue(target, out float next) && now < next)
            {
                return false;
            }
            _next[target] = now + tickSeconds;
            return true;
        }

        /// <summary>Drops the bodies whose next tick has come due by <paramref name="now"/>, so the list holds only who a stream must still wait for.</summary>
        public void Forget(float now)
        {
            _past.Clear();
            foreach (KeyValuePair<IDamageable, float> entry in _next)
            {
                if (now >= entry.Value)
                {
                    _past.Add(entry.Key);
                }
            }
            foreach (IDamageable target in _past)
            {
                _next.Remove(target);
            }
        }
    }
}
