using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// POISON on one body: damage over time for a fixed while. Put on whatever was poisoned (anything with an IDamageable) by <see cref="Apply"/>;
    /// it drains health in half-second ticks, shows a few rising purple bubbles on the victim, and removes itself (and the bubbles) when it runs
    /// out or the victim dies.
    ///
    /// DOES NOT STACK: being poisoned again while poisoned restarts the clock (and keeps the stronger rate); standing in a cloud keeps it topped
    /// up, so you stay poisoned until ten seconds after you leave, never "twice as poisoned".
    ///
    /// Plain health loss: a target that implements IPoisonable takes it quietly (a player does not flinch every tick); any other IDamageable takes
    /// each tick as a zero-stun hit. The total is exactly DamagePerSecond x Seconds, tick by tick, however the frames fall.
    /// </summary>
    public sealed class PoisonEffect : MonoBehaviour
    {
        /// <summary>How long poison lasts, in seconds (a first guess to tune: asked for 10).</summary>
        public const float DefaultSeconds = 10.0f;

        /// <summary>Health lost per second while poisoned: 3 of a body's 100 (so a full dose is 30%: it hurts and does not kill on its own from full health).</summary>
        public const float DefaultDamagePerSecond = 3.0f;

        /// <summary>Seconds between ticks of damage.</summary>
        public const float TickSeconds = 0.5f;

        public float DamagePerSecond { get; private set; }

        public float Seconds { get; private set; }

        /// <summary>Seconds since the poison was applied or last topped up.</summary>
        public float Elapsed { get; private set; }

        public float Remaining => Mathf.Max(0.0f, Seconds - Elapsed);

        public bool IsActive => Elapsed < Seconds;

        /// <summary>How many ticks of damage have landed since it was last restarted.</summary>
        public int TicksDone { get; private set; }

        private IDamageable _target;
        private GameObject _vfx;

        /// <summary>
        /// Poisons <paramref name="victim"/> (the GameObject carrying its IDamageable): starts the effect, or restarts it if it is already
        /// there. Returns the effect, or null when the thing cannot be hurt.
        /// </summary>
        public static PoisonEffect Apply(GameObject victim, float damagePerSecond, float seconds, GameObject vfxPrefab = null)
        {
            if (victim == null)
            {
                return null;
            }
            IDamageable target = victim.GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive)
            {
                return null;
            }
            GameObject host = ((Component)target).gameObject;
            var effect = host.GetComponent<PoisonEffect>();
            if (effect == null)
            {
                effect = host.AddComponent<PoisonEffect>();
            }
            effect._target = target;
            effect.DamagePerSecond = Mathf.Max(effect.IsActive ? effect.DamagePerSecond : 0.0f, damagePerSecond);
            effect.Seconds = Mathf.Max(0.01f, seconds);
            effect.Elapsed = 0.0f;
            effect.TicksDone = 0;
            effect.ShowBubbles(vfxPrefab);
            return effect;
        }

        private void ShowBubbles(GameObject prefab)
        {
            if (_vfx != null || prefab == null)
            {
                return;
            }
            _vfx = Instantiate(prefab, transform);
            _vfx.transform.localPosition = Vector3.up * 1.2f;
            _vfx.transform.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>Advances the poison by <paramref name="deltaTime"/> seconds, landing every tick that has come due. Public so a test can step it.</summary>
        public void Tick(float deltaTime)
        {
            if (_target == null || !_target.IsAlive)
            {
                End();
                return;
            }
            Elapsed += deltaTime;
            float counted = Mathf.Min(Elapsed, Seconds);
            int due = Mathf.FloorToInt(counted / TickSeconds + 1e-4f);
            while (TicksDone < due)
            {
                TicksDone++;
                Hurt(DamagePerSecond * TickSeconds);
                if (!_target.IsAlive)
                {
                    End();
                    return;
                }
            }
            if (Elapsed >= Seconds)
            {
                End();
            }
        }

        private void Hurt(float amount)
        {
            if (_target is IPoisonable poisonable)
            {
                poisonable.TakePoison(amount);
            }
            else
            {
                _target.ApplyHit(amount, transform.position, 0.0f);
            }
        }

        private void End()
        {
            if (_vfx != null)
            {
                foreach (ParticleSystem particles in _vfx.GetComponentsInChildren<ParticleSystem>())
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                Destroy(_vfx, 2.0f);
                _vfx = null;
            }
            Destroy(this);
        }
    }
}
