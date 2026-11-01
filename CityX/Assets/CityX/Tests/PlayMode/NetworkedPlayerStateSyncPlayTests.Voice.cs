using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Proximity voice over real sockets (docs/VOICE.md; GAME_DESIGN "Social"): what A says comes out of A's copy on B's machine while B stands
    /// near, and the server sends B nothing once B is out of earshot. A copy's microphone stays off.
    /// </summary>
    public sealed partial class NetworkedPlayerStateSyncPlayTests
    {
        [UnityTest]
        public IEnumerator WhatOnePlayerSaysIsHeardByANearPlayerAndNotByAFarOne()
        {
#if UNITY_EDITOR
            yield return StartTwoPlayers();
            var speaker = _aOnA.GetComponent<NetworkedVoice>();
            Assert.IsNotNull(speaker, "the player's body has no voice -- rebuild the scene");
            var heardOnB = _aOnB.GetComponent<VoicePlayback>();
            Assert.IsNotNull(heardOnB, "a copy has nothing to play a voice with");
            Assert.IsFalse(_aOnB.GetComponent<PlayerVoiceCapture>().enabled, "a copy's microphone is on");
            Assert.IsTrue(_aOnA.GetComponent<PlayerVoiceCapture>().enabled, "the owner's microphone is off");

            var said = new float[VoiceCodec.FrameSamples];
            int frame = 0;
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && heardOnB.FramesHeard < 10)
            {
                for (int i = 0; i < said.Length; i++) said[i] = 0.3f * Mathf.Sin((frame * said.Length + i) * 0.17f);
                frame++;
                speaker.Send(said);
                yield return null;
            }
            Assert.GreaterOrEqual(heardOnB.FramesHeard, 10, "B never heard A, two metres away");
            Assert.IsTrue(heardOnB.IsPlaying, "A's voice is not playing from A's copy on B's machine");

            // B walks out of earshot: once the server sees B there, it sends B none of A's voice.
            Vector3 away = new Vector3(VoiceHearing.RangeMetres + NetworkedVoice.RelayMarginMetres + 20.0f, 0.5f, 0.0f);
            var controller = _bOnB.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            _bOnB.transform.position = away;
            if (controller != null) controller.enabled = true;
            NetworkedVoice serverSpeaker = null;
            foreach (NetworkedVoice voice in Object.FindObjectsByType<NetworkedVoice>(FindObjectsSortMode.None))
            {
                if (voice.IsServerInitialized && voice.ObjectId == _aOnA.ObjectId) serverSpeaker = voice;
            }
            Assert.IsNotNull(serverSpeaker, "no server copy of A's voice");
            NetworkedPlayerIdentity bOnServer = null;
            foreach (NetworkedPlayerIdentity identity in Object.FindObjectsByType<NetworkedPlayerIdentity>(FindObjectsSortMode.None))
            {
                if (identity.IsServerInitialized && identity.ObjectId == _bOnB.ObjectId) bOnServer = identity;
            }
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && (bOnServer == null || Vector3.Distance(bOnServer.transform.position, away) > 2.0f))
            {
                yield return null;
            }
            for (int i = 0; i < 10; i++) yield return null;   // frames already on their way arrive

            int relayed = serverSpeaker.FramesRelayed, heard = heardOnB.FramesHeard;
            for (int i = 0; i < 30; i++)
            {
                speaker.Send(said);
                yield return null;
            }
            Assert.AreEqual(relayed, serverSpeaker.FramesRelayed, "the server sent A's voice to B out of earshot");
            Assert.AreEqual(heard, heardOnB.FramesHeard, "B heard A from out of earshot");
#else
            Assert.Ignore("needs the editor to load the networked player prefab.");
            yield break;
#endif
        }
    }
}
