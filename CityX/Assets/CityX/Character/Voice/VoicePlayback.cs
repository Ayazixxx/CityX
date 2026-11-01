using CityX.World;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Another player's voice coming out of their head on this machine (docs/VOICE.md): frames from <see cref="NetworkedVoice"/> are decoded
    /// into a <see cref="VoiceJitterBuffer"/> and played by a 3D sound at the head, so it also comes from their side of the screen. How loud and
    /// how muffled is this game's own (<see cref="VoiceHearing"/>), measured from this machine's player to the speaker, counting the walls
    /// between. The sound plays only while they talk (an idle voice would hold one of the mixer's few voices).
    /// </summary>
    public sealed class VoicePlayback : MonoBehaviour
    {
        /// <summary>How long after the last frame the voice stops playing, seconds.</summary>
        public const float StopAfterSeconds = 1.0f;

        /// <summary>A voice heard within this long counts as speaking, seconds.</summary>
        public const float SpeakingSeconds = 0.25f;

        /// <summary>How often the walls between are counted again, seconds.</summary>
        public const float WallCheckSeconds = 0.1f;

        /// <summary>How fast the volume and the muffling follow a change (per second, of the whole range), so a door passed does not click.</summary>
        public const float FollowPerSecond = 6.0f;

        /// <summary>
        /// Where this machine's player hears from (their head) and their body, set by their own <see cref="NetworkedVoice"/>; without one, the
        /// scene's audio listener.
        /// </summary>
        public static Transform ListenerHead { get; private set; }

        public static Transform ListenerBody { get; private set; }

        public static void SetListener(Transform head, Transform body)
        {
            ListenerHead = head;
            ListenerBody = body;
        }

        public static void ClearListener(Transform body)
        {
            if (ListenerBody == body)
            {
                ListenerHead = null;
                ListenerBody = null;
            }
        }

        public int FramesHeard { get; private set; }

        public float Volume { get; private set; }

        public int Walls { get; private set; }

        public bool IsSpeaking => Time.time - _lastHeard < SpeakingSeconds;

        public bool IsPlaying => _source != null && _source.isPlaying;

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        private readonly VoiceJitterBuffer _buffer = new VoiceJitterBuffer();
        private readonly float[] _samples = new float[VoiceCodec.FrameSamples];
        private AudioSource _source;
        private AudioLowPassFilter _filter;
        private float _lastHeard = float.NegativeInfinity;
        private float _sinceWallCheck = WallCheckSeconds;
        private float _cutoff = VoiceHearing.OpenCutoffHz;
        private bool _anySequence;
        private ushort _lastSequence;

        private void Awake()
        {
            var animator = GetComponentInChildren<Animator>();
            Transform head = animator != null && animator.avatar != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var mouth = new GameObject("Voice");
            mouth.transform.SetParent(head != null ? head : transform, false);
            if (head == null)
            {
                mouth.transform.localPosition = Vector3.up * (BodyRig.StandingHeight * 0.9f);
            }
            _source = mouth.AddComponent<AudioSource>();
            _source.clip = AudioClip.Create("Voice", VoiceCodec.SampleRate, 1, VoiceCodec.SampleRate, true, _buffer.Read);
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 1.0f;
            _source.dopplerLevel = 0.0f;
            // The fade is VoiceHearing's, so Unity's own distance fade is held flat.
            _source.rolloffMode = AudioRolloffMode.Custom;
            _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0.0f, 1.0f, 1.0f));
            _source.maxDistance = VoiceHearing.RangeMetres;
            _source.volume = 0.0f;
            _filter = mouth.AddComponent<AudioLowPassFilter>();
            _filter.cutoffFrequency = VoiceHearing.OpenCutoffHz;
        }

        /// <summary>One frame from the speaker; a frame older than the last one played is late and dropped.</summary>
        public void Receive(byte[] frame)
        {
            if (!VoiceCodec.TryDecode(frame, _samples, out ushort sequence))
            {
                return;
            }
            bool fresh = !_anySequence || (short)(sequence - _lastSequence) > 0 || Time.time - _lastHeard > StopAfterSeconds;
            if (!fresh)
            {
                return;
            }
            _anySequence = true;
            _lastSequence = sequence;
            _lastHeard = Time.time;
            FramesHeard++;
            _buffer.Write(_samples, _samples.Length);
            if (!_source.isPlaying)
            {
                _sinceWallCheck = WallCheckSeconds;
                Hear(0.0f, true);
                _source.Play();
            }
        }

        private void Update()
        {
            if (_source == null || !_source.isPlaying)
            {
                return;
            }
            if (Time.time - _lastHeard > StopAfterSeconds)
            {
                _source.Stop();
                _buffer.Clear();   // anything left is a second stale: it must not start the next sentence
                return;
            }
            Hear(Time.deltaTime, false);
        }

        /// <summary>Sets the volume and the muffling for where the listener is now: at once when <paramref name="snap"/>, else easing there.</summary>
        private void Hear(float deltaTime, bool snap)
        {
            Vector3 ear = ListenerHead != null ? ListenerHead.position : ListenerFallback();
            Vector3 mouth = _source.transform.position;
            _sinceWallCheck += deltaTime;
            if (snap || _sinceWallCheck >= WallCheckSeconds)
            {
                _sinceWallCheck = 0.0f;
                Walls = WallsBetween(ear, mouth);
            }
            float volume = VoiceHearing.VolumeAt(Vector3.Distance(ear, mouth), Walls);
            float cutoff = VoiceHearing.CutoffFor(Walls);
            float step = snap ? 1.0f : FollowPerSecond * deltaTime;
            Volume = Mathf.MoveTowards(Volume, volume, step);
            _cutoff = Mathf.MoveTowards(_cutoff, cutoff, step * VoiceHearing.OpenCutoffHz);
            _source.volume = Volume;
            _filter.cutoffFrequency = _cutoff;
        }

        private static Vector3 ListenerFallback()
        {
            var listener = Object.FindAnyObjectByType<AudioListener>();
            return listener != null ? listener.transform.position : Vector3.zero;
        }

        /// <summary>The solid things between the ear and the mouth, up to <see cref="VoiceHearing.MaxWalls"/>: not either body, not glass shards.</summary>
        private int WallsBetween(Vector3 ear, Vector3 mouth)
        {
            Vector3 line = mouth - ear;
            float length = line.magnitude;
            if (length < 0.01f)
            {
                return 0;
            }
            int count = Physics.RaycastNonAlloc(ear, line / length, Hits, length, ~0, QueryTriggerInteraction.Ignore);
            int walls = 0;
            for (int i = 0; i < count && walls < VoiceHearing.MaxWalls; i++)
            {
                Transform hit = Hits[i].transform;
                if (hit.IsChildOf(transform) || (ListenerBody != null && hit.IsChildOf(ListenerBody)) || hit.gameObject.layer == GlassFracture.ShardLayer)
                {
                    continue;
                }
                walls++;
            }
            return walls;
        }
    }
}
