using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Another player's voice on this machine (docs/VOICE.md): it plays from their head while frames come and stops a moment after; it is
    /// quieter and duller through a wall than in the open, and quieter far away than near.
    /// </summary>
    public sealed class VoicePlaybackPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private ushort _sequence;
        private VoiceCodec.State _coder;

        [SetUp]
        public void SetUp()
        {
            Time.captureFramerate = 60;   // the volume eases by time, and a headless frame is a fraction of a millisecond
        }

        [TearDown]
        public void TearDown()
        {
            VoicePlayback.ClearListener(VoicePlayback.ListenerBody);
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        private VoicePlayback Speaker(Vector3 at)
        {
#if UNITY_EDITOR
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            _made.Add(body);
            body.GetComponent<PlayerInputReader>().enabled = false;
            return body.AddComponent<VoicePlayback>();
#else
            return null;
#endif
        }

        private Transform Ear(Vector3 at)
        {
            var ear = new GameObject("Ear");
            _made.Add(ear);
            ear.transform.position = at;
            VoicePlayback.SetListener(ear.transform, ear.transform);
            return ear.transform;
        }

        private IEnumerator Talk(VoicePlayback speaker, int frames)
        {
            var said = new float[VoiceCodec.FrameSamples];
            var frame = new byte[VoiceCodec.FrameBytes];
            for (int f = 0; f < frames; f++)
            {
                for (int i = 0; i < said.Length; i++) said[i] = 0.3f * Mathf.Sin(i * 0.2f);
                VoiceCodec.Encode(said, _sequence++, ref _coder, frame);
                speaker.Receive(frame);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AVoicePlaysWhileItComesAndStopsAMomentAfter()
        {
            VoicePlayback speaker = Speaker(Vector3.zero);
            Ear(new Vector3(0.0f, 2.4f, 3.0f));
            yield return Talk(speaker, 10);
            Assert.IsTrue(speaker.IsPlaying, "the voice is not playing");
            Assert.IsTrue(speaker.IsSpeaking);
            Assert.Greater(speaker.Volume, 0.9f, "a voice three metres away is not at full volume");
            yield return new WaitForSeconds(VoicePlayback.StopAfterSeconds + 0.3f);
            Assert.IsFalse(speaker.IsPlaying, "the voice kept playing after the speaker stopped");
        }

        [UnityTest]
        public IEnumerator AVoiceThroughAWallIsQuieterThanInTheOpenAndFarIsQuieterThanNear()
        {
            VoicePlayback speaker = Speaker(Vector3.zero);
            Transform ear = Ear(new Vector3(0.0f, 2.4f, 12.0f));
            yield return Talk(speaker, 30);
            float open = speaker.Volume;
            Assert.AreEqual(0, speaker.Walls);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(wall);
            wall.transform.position = new Vector3(0.0f, 2.0f, 6.0f);
            wall.transform.localScale = new Vector3(10.0f, 6.0f, 0.3f);
            Physics.SyncTransforms();
            yield return Talk(speaker, 30);
            Assert.AreEqual(1, speaker.Walls, "the wall between was not counted");
            Assert.Less(speaker.Volume, open * 0.6f, "the voice through a wall is not quieter");

            Object.Destroy(wall);
            yield return null;
            ear.position = new Vector3(0.0f, 2.4f, 30.0f);
            yield return Talk(speaker, 30);
            Assert.Less(speaker.Volume, open, "the voice far away is not quieter than near");
            Assert.Greater(speaker.Volume, 0.0f, "the voice 30 m away is gone");
        }
    }
}
