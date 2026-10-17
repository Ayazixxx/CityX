using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// SELF SMITE'S TIMELINE AND NUMBERS: Pikachu's Thunder (its down special in Super Smash Bros. Ultimate), frame for frame at 60 frames a
    /// second (owner, 2026-10-15: "it is like pikachu's down special from smash bros and i want you to time it and make it function exactly like
    /// it"). Thunder's frame data:
    ///   frame 2       the thundercloud forms high over Pikachu
    ///   frame 13      the bolt leaves the cloud, hurting whatever it passes on the way down (6 to 8%)
    ///   frame 33      it reaches Pikachu
    ///   frames 34-36  a burst of electricity round Pikachu (15%, a strong launch)
    ///   frames 34-43  Pikachu cannot be hit
    ///   frame 75      Pikachu can act again (86 when the bolt misses; here it follows the caster and never does)
    /// Pikachu stands still for all of it. Frame N runs from (N - 1) / 60 s to N / 60 s after the button, so the strike is at 33/60 = 0.55 s.
    ///
    /// The bolt comes from the cloud layer (<see cref="CloudLayer"/>, 1.5 km up: owner, "make the bolt appear from SUPER high in the sky"), so it
    /// falls 1.5 km in a third of a second. It slows as it comes (<see cref="TipHeightAbove"/>) so the last stretch, the part the camera behind
    /// the caster can see, takes the last nine frames rather than one: the bolt is seen coming, as Thunder's is.
    ///
    /// The animation (selfsmite.fbx, the owner's) throws its hands down at <see cref="ClipHandsDownSeconds"/> of its own time; the Animator
    /// plays it at <see cref="ClipSpeed"/> so the hands come down as the bolt lands (owner: "make the thunder bolt appear as he throws his hands
    /// down"), and the clip then ends about when Thunder frees Pikachu.
    /// </summary>
    public static class SelfSmiteMove
    {
        public const float FramesPerSecond = 60.0f;

        public const int CloudFrame = 2;
        public const int BoltFrame = 13;
        public const int StrikeFrame = 33;
        public const int BlastLastFrame = 36;
        public const int IntangibleLastFrame = 43;
        public const int FreeFrame = 75;

        /// <summary>When frame <paramref name="frame"/> starts, seconds after the button.</summary>
        public static float StartOf(int frame) => (frame - 1) / FramesPerSecond;

        /// <summary>When frame <paramref name="frame"/> ends.</summary>
        public static float EndOf(int frame) => frame / FramesPerSecond;

        /// <summary>The cloud forms.</summary>
        public static readonly float CloudSeconds = StartOf(CloudFrame);

        /// <summary>The bolt leaves the cloud.</summary>
        public static readonly float BoltSeconds = StartOf(BoltFrame);

        /// <summary>The bolt reaches the caster: the burst, and the caster can no longer be hit.</summary>
        public static readonly float StrikeSeconds = EndOf(StrikeFrame);

        /// <summary>The burst's last moment of hurting.</summary>
        public static readonly float BlastEndSeconds = EndOf(BlastLastFrame);

        /// <summary>The caster can be hit again.</summary>
        public static readonly float IntangibleEndSeconds = EndOf(IntangibleLastFrame);

        /// <summary>The caster can move and act again.</summary>
        public static readonly float FreeSeconds = StartOf(FreeFrame);

        /// <summary>The bolt and the burst have faded away: the whole effect's life, seconds.</summary>
        public const float EffectSeconds = 1.1f;

        /// <summary>The Animator state that plays selfsmite.fbx (PlayerRigBuilder.AddSelfSmiteState).</summary>
        public const string StateName = "SelfSmite";

        /// <summary>
        /// When selfsmite.fbx's hands are thrown down, in its own seconds: the fastest fall of the hands, measured on the rig (CityX > Measure Self
        /// Smite Clip). The owner's second clip (2026-10-17, SelfSmiteA.fbx: "replace the self smite animation with this one") raises the hands
        /// to 0.4 s, slams them at 0.49 s, stays low to 1.7 s and stands up by 2.3 s. (The first clip's slam was at 1.27 s of 2.967.)
        /// </summary>
        public const float ClipHandsDownSeconds = 0.49f;

        /// <summary>The clip's own length, seconds.</summary>
        public const float ClipSeconds = 2.367f;

        /// <summary>
        /// Where in its own seconds the clip is left for the walk: as the caster is freed. The clip stays crouched long after that, so the state
        /// hands back then and blends up out of the crouch over <see cref="ClipExitBlendSeconds"/> rather than holding a free body down.
        /// </summary>
        public static float ClipExitSeconds => FreeSeconds * ClipSpeed;

        public const float ClipExitBlendSeconds = 0.35f;

        /// <summary>The Animator's speed for the clip: its hands come down as the bolt lands.</summary>
        public static float ClipSpeed => ClipHandsDownSeconds / StrikeSeconds;

        /// <summary>Seconds between casts. Thunder has none (it is its own long commitment); a spell here does.</summary>
        public const float CooldownSeconds = 5.0f;

        /// <summary>
        /// The bolt on its way down hurts what it passes: <see cref="BoltDamage"/> to anyone within <see cref="BoltRadius"/> of it (Thunder's
        /// 6 to 8%, against the burst's 15%).
        /// </summary>
        public const float BoltDamage = 18.0f;

        public const float BoltRadius = 1.5f;

        /// <summary>The burst round the caster as the bolt lands: <see cref="BlastDamage"/> to anyone within <see cref="BlastRadius"/>.</summary>
        public const float BlastDamage = 40.0f;

        /// <summary>The burst's reach from the caster's middle, metres: about one and a half body heights, as Thunder's is round Pikachu.</summary>
        public const float BlastRadius = 4.0f;

        /// <summary>How hard the burst throws who it hits, metres per second: away and up, a strong launch.</summary>
        public const float BlastLaunchOut = 13.0f;

        public const float BlastLaunchUp = 8.0f;

        /// <summary>How far through the bolt's fall it is at <paramref name="seconds"/>: 0 in the cloud (and before it leaves), 1 on the caster.</summary>
        public static float Fall(float seconds)
        {
            return Mathf.Clamp01((seconds - BoltSeconds) / (StrikeSeconds - BoltSeconds));
        }

        /// <summary>
        /// How high above the caster the bolt's tip is at <paramref name="seconds"/>, for a cloud <paramref name="cloudAbove"/> metres overhead.
        /// Fast high up, slow near the end ((1 - fall)^4): from 1.5 km, the last 50 m take nine frames.
        /// </summary>
        public static float TipHeightAbove(float seconds, float cloudAbove)
        {
            float left = 1.0f - Fall(seconds);
            return Mathf.Max(0.0f, cloudAbove) * left * left * left * left;
        }

        public static bool IsIntangible(float seconds) => seconds >= StrikeSeconds && seconds < IntangibleEndSeconds;

        public static bool BlastHurts(float seconds) => seconds >= StrikeSeconds && seconds < BlastEndSeconds;

        /// <summary>The caster is held by the move: no walking, jumping or other actions.</summary>
        public static bool HoldsTheCaster(float seconds) => seconds >= 0.0f && seconds < FreeSeconds;

        /// <summary>How big the burst is drawn at <paramref name="seconds"/>, 0..1 of <see cref="BlastRadius"/>: out in three frames, then fading.</summary>
        public static float BlastGrowth(float seconds)
        {
            return Mathf.Clamp01((seconds - StrikeSeconds) / (BlastEndSeconds - StrikeSeconds));
        }

        /// <summary>The launch the burst gives somebody standing at <paramref name="victim"/> from a caster's middle at <paramref name="centre"/>.</summary>
        public static Vector3 Launch(Vector3 centre, Vector3 victim)
        {
            Vector3 away = victim - centre;
            away.y = 0.0f;
            Vector3 direction = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
            return direction * BlastLaunchOut + Vector3.up * BlastLaunchUp;
        }
    }
}
