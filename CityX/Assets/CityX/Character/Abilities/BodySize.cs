using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// GROW AND SHRINK, THE PURE PART (owner, 2026-10-15: "one that makes you 10x as big and one that makes you 10x as small ... 30 seconds each",
    /// in a new Wizard tree). How big the body is from moment to moment: a spell starts a change to its size, the body holds it for
    /// <see cref="HoldSeconds"/> from the cast, then changes back. No scene here; BodySizeAbility applies <see cref="Scale"/> to the body.
    ///
    /// THE CHANGE EASES IN LOG SPACE (1 -> 10 passes 3.16 halfway, not 5.5): the eye reads size by ratio, so each moment of a change looks like
    /// the same amount of growing. The same spell again ends it early; the other spell goes straight to the other size, for a fresh 30 s.
    ///
    /// GROWING NEEDS ROOM: each step of a change that grows is offered to the caller first (<c>fits</c>); if the bigger body would not fit (a
    /// ceiling, a wall too close), the change waits where it is and carries on once there is room -- under a bridge a giant stops at the
    /// height of the deck. Shrinking always fits.
    ///
    /// HOW A RESIZED BODY MOVES (<see cref="SpeedFactor"/>): as a real body of that size would. Gravity is the same for a giant as for anyone,
    /// so a body's natural pace goes with the square root of its size (the Froude number: why a horse walks faster than a mouse but nowhere
    /// near as many body-lengths a second): ten times as big moves 3.2 times as fast, strides ten times as far, and so steps 3.2 times as
    /// slowly (<see cref="StrideRate"/>) -- heavy and slow, a giant. Ten times as small scurries: a third of the speed, three times the steps.
    /// A jump launched sqrt(size) times as fast climbs size times as high: the same jump, in proportion.
    /// </summary>
    public sealed class BodySize
    {
        public const float GrowScale = 10.0f;
        public const float ShrinkScale = 0.1f;

        /// <summary>How long a spell lasts, from the cast, seconds (owner: 30 each).</summary>
        public const float HoldSeconds = 30.0f;

        /// <summary>How long the change itself takes, there and back, seconds.</summary>
        public const float ChangeSeconds = 1.2f;

        /// <summary>Closer to 1 than this is normal size.</summary>
        public const float NormalTolerance = 0.01f;

        public enum Spell
        {
            None,
            Grow,
            Shrink,
        }

        /// <summary>How big the body is now, times its own size.</summary>
        public float Scale { get; private set; } = 1.0f;

        /// <summary>The spell in effect (None once it has run out or been ended, while the body changes back too).</summary>
        public Spell Active { get; private set; }

        /// <summary>Seconds left of the spell in effect.</summary>
        public float Remaining { get; private set; }

        /// <summary>The size the body is changing to (where it rests when not changing).</summary>
        public float Target => _to;

        /// <summary>True while the size is changing (including a change waiting for room).</summary>
        public bool IsChanging => _progress < 1.0f;

        /// <summary>True while a change waits for room to grow.</summary>
        public bool IsWaitingForRoom { get; private set; }

        public bool IsResized => Mathf.Abs(Scale - 1.0f) > NormalTolerance;

        /// <summary>How much faster the body moves than at its own size: the square root of the size (see the class comment).</summary>
        public float SpeedFactor => Mathf.Sqrt(Scale);

        /// <summary>How fast the walk and run cycles play: one over SpeedFactor, so the feet keep to the ground.</summary>
        public float StrideRate => 1.0f / Mathf.Sqrt(Scale);

        private float _from = 1.0f;
        private float _to = 1.0f;
        private float _progress = 1.0f;

        /// <summary>The size a spell takes the body to.</summary>
        public static float ScaleOf(Spell spell) => spell == Spell.Grow ? GrowScale : spell == Spell.Shrink ? ShrinkScale : 1.0f;

        /// <summary>
        /// Casts a spell: the body starts changing to its size, for HoldSeconds. The spell already in effect, cast again, ends it instead (the
        /// body changes back). Returns what happened: the spell now in effect (None when it was an early end).
        /// </summary>
        public Spell Cast(Spell spell)
        {
            if (spell == Spell.None)
            {
                return Active;
            }
            if (spell == Active)
            {
                End();
                return Spell.None;
            }
            Active = spell;
            Remaining = HoldSeconds;
            ChangeTo(ScaleOf(spell));
            return spell;
        }

        /// <summary>Ends the spell in effect now: the body changes back to its own size.</summary>
        public void End()
        {
            Active = Spell.None;
            Remaining = 0.0f;
            ChangeTo(1.0f);
        }

        /// <summary>Puts the body at its own size at once (a death, a respawn).</summary>
        public void Reset()
        {
            Active = Spell.None;
            Remaining = 0.0f;
            Scale = _from = _to = 1.0f;
            _progress = 1.0f;
            IsWaitingForRoom = false;
        }

        /// <summary>
        /// Moves time on. <paramref name="fits"/> is asked whether a bigger size would fit before the body grows to it (null: always). Returns
        /// the size now.
        /// </summary>
        public float Tick(float deltaTime, System.Func<float, bool> fits = null)
        {
            deltaTime = Mathf.Max(0.0f, deltaTime);
            if (Active != Spell.None)
            {
                Remaining -= deltaTime;
                if (Remaining <= 0.0f)
                {
                    End();
                }
            }
            IsWaitingForRoom = false;
            if (_progress >= 1.0f)
            {
                return Scale;
            }
            float progress = Mathf.Min(1.0f, _progress + deltaTime / ChangeSeconds);
            float next = Eased(_from, _to, progress);
            if (next > Scale + 1e-5f && fits != null && !fits(next))
            {
                IsWaitingForRoom = true;   // no room to grow yet: wait here
                return Scale;
            }
            _progress = progress;
            Scale = next;
            return Scale;
        }

        /// <summary>The size a change has reached at <paramref name="t"/> (0..1): smooth in and out, by ratio (log space).</summary>
        public static float Eased(float from, float to, float t)
        {
            t = Mathf.Clamp01(t);
            float s = t * t * (3.0f - 2.0f * t);
            return Mathf.Exp(Mathf.Lerp(Mathf.Log(from), Mathf.Log(to), s));
        }

        private void ChangeTo(float scale)
        {
            _from = Scale;
            _to = scale;
            _progress = Mathf.Approximately(_from, _to) ? 1.0f : 0.0f;
        }
    }
}
