using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// THE CHAIN'S SWING, HEARD: the owner's four recordings (2026-11-22: "you can make it cycle through them or play them at random but never 1
    /// twice in a row"), in Resources (the first two with their wind taken out: SourceArt/Chain/chain_sounds.py); and, with the Fire Enhancement
    /// on it, their recordings of the burning chain instead (2026-11-22: "make this the noise that plays when you swing your flame enhanced chain,
    /// i will make more later that it can cycle through at random": a new one is a file in Resources/Chain and a line in <see cref="Hot"/>). One
    /// is picked for each swing from its set, never the one before it, and started so that its loudest moment falls as the blow lands. Played by
    /// <see cref="SwordPoseDriver"/> on every body the attack plays on, so other players hear each other's chains.
    /// </summary>
    public static class ChainSounds
    {
        /// <summary>A set of recordings: their names in Resources and where each is loudest, seconds into it.</summary>
        public sealed class Set
        {
            public readonly string[] ClipNames;
            public readonly float[] LoudestAtSeconds;
            private AudioClip[] _clips;
            internal int Last = -1;

            public Set(string[] clipNames, float[] loudestAtSeconds)
            {
                ClipNames = clipNames;
                LoudestAtSeconds = loudestAtSeconds;
            }

            public int Count => ClipNames.Length;

            public AudioClip Clip(int index)
            {
                if (_clips == null)
                {
                    _clips = new AudioClip[ClipNames.Length];
                    for (int i = 0; i < _clips.Length; i++)
                    {
                        _clips[i] = Resources.Load<AudioClip>(ClipNames[i]);
                    }
                }
                return _clips[Mathf.Clamp(index, 0, _clips.Length - 1)];
            }
        }

        /// <summary>The plain chain (chain_sounds.py prints where each is loudest).</summary>
        public static readonly Set Cold = new Set(
            new[] { "Chain/ChainSwing1", "Chain/ChainSwing2", "Chain/ChainSwing3", "Chain/ChainSwing4" },
            new[] { 0.22f, 0.29f, 0.16f, 0.27f });

        /// <summary>The chain with the Fire Enhancement on it.</summary>
        public static readonly Set Hot = new Set(
            new[] { "Chain/ChainSwingHot1" },
            new[] { 0.27f });

        /// <summary>Heard as the Fire Enhancement takes the chain (owner, 2026-11-22: the "noise that plays when you flame enhance your chain").</summary>
        public const string IgniteClipName = "Chain/ChainIgnite";

        private static AudioClip _ignite;

        /// <summary>Plays the chain catching fire at <paramref name="point"/> (the fist).</summary>
        public static void PlayIgnite(Vector3 point)
        {
            if (_ignite == null)
            {
                _ignite = Resources.Load<AudioClip>(IgniteClipName);
            }
            PlayClip(_ignite, point);
        }

        public const float Volume = 0.8f;

        /// <summary>A swing is at full volume this near, metres (the camera hears for the player from 5 m behind it), and fades beyond.</summary>
        public const float FullVolumeMetres = 8.0f;

        private const string SoundName = "ChainSwingSound";

        /// <summary>
        /// Which of <paramref name="count"/> recordings follows <paramref name="last"/> (-1: none yet) for a <paramref name="roll"/> of 0 up to
        /// one less than the number left to choose from: any but the last one, each equally likely (with only one, that one).
        /// </summary>
        public static int After(int last, int roll, int count)
        {
            if (count <= 1)
            {
                return 0;
            }
            if (last < 0 || last >= count)
            {
                return Mathf.Clamp(roll, 0, count - 1);
            }
            roll = Mathf.Clamp(roll, 0, count - 2);
            return roll >= last ? roll + 1 : roll;
        }

        /// <summary>The next swing's recording from <paramref name="set"/>: not the one just heard.</summary>
        public static int Next(Set set)
        {
            int choices = set.Last < 0 ? set.Count : Mathf.Max(1, set.Count - 1);
            set.Last = After(set.Last, Random.Range(0, choices), set.Count);
            return set.Last;
        }

        /// <summary>How far into a swing whose blow lands at <paramref name="contactSeconds"/> recording <paramref name="index"/> of <paramref name="set"/> starts, so its loudest moment is the blow.</summary>
        public static float StartSeconds(Set set, int index, float contactSeconds) =>
            Mathf.Max(0.0f, contactSeconds - set.LoudestAtSeconds[Mathf.Clamp(index, 0, set.LoudestAtSeconds.Length - 1)]);

        /// <summary>Plays recording <paramref name="index"/> of <paramref name="set"/> at <paramref name="point"/> (the fist), in the world.</summary>
        public static void Play(Set set, int index, Vector3 point)
        {
            PlayClip(set.Clip(index), point);
        }

        private static void PlayClip(AudioClip clip, Vector3 point)
        {
            if (clip == null || !Application.isPlaying)
            {
                return;
            }
            var sound = new GameObject(SoundName);
            sound.transform.position = point;
            AudioSource source = sound.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Volume;
            source.spatialBlend = 1.0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = FullVolumeMetres;
            source.dopplerLevel = 0.0f;
            source.Play();
            Object.Destroy(sound, clip.length + 0.1f);
        }
    }
}
