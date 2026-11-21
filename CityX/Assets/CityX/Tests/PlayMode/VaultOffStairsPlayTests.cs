using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// NO VAULT RUNNING UP A STAIR (owner, 2026-11-19: "i accidentally vault when running up stairs sometimes"; 2026-11-21: "just focus on the
    /// accidental vaulting running up stairs"). The real body sprints up a stair of the city's size (0.227 m risers, 0.36 m treads) into what
    /// looked like a wall to jump: a rail along its landing with a roof beyond, or a short landing with a drop past it. Every frame the vault is
    /// asked for as a player holding Shift and W asks for it (PlayerVaulting.TryStart), and none may start. Stood on the landing first, or the
    /// run let go of and pressed again, the same rail is vaulted: it is the run carried on from the stair, not the rail, that holds the vault back.
    /// </summary>
    public sealed class VaultOffStairsPlayTests
    {
        private const float Step = 1.0f / 60.0f;
        private const float Tread = 0.36f;
        private const float Riser = 0.227f;
        private const int Steps = 6;
        private const float FirstStepZ = 2.0f;

        private readonly List<GameObject> _made = new List<GameObject>();
        private GameObject _player;
        private PlayerLocomotion _locomotion;
        private PlayerVaulting _vaulting;
        private float _highest;

        private static float TopZ => FirstStepZ + Tread * Steps;
        private static float TopY => Riser * Steps;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        private void Box(Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = centre;
            go.transform.localScale = size;
            _made.Add(go);
        }

        /// <summary>
        /// The ground, a flight up +Z and a landing at its top. With a rail: a 1.2 m landing, a 1.3 m rail on its far edge and a roof beyond it a
        /// little lower (a stair up the outside of a building). Without: a 0.6 m landing and the drop back to the ground past it.
        /// </summary>
        private void Build(bool rail)
        {
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            for (int i = 0; i < Steps; i++)
            {
                float height = Riser * (i + 1);
                Box(new Vector3(0.0f, height * 0.5f, FirstStepZ + Tread * (i + 0.5f)), new Vector3(2.0f, height, Tread));
            }
            float landing = rail ? 1.2f : 0.6f;
            Box(new Vector3(0.0f, TopY * 0.5f, TopZ + landing * 0.5f), new Vector3(2.0f, TopY, landing));
            if (rail)
            {
                Box(new Vector3(0.0f, TopY + 0.65f, TopZ + landing - 0.05f), new Vector3(2.0f, 1.3f, 0.1f));
                float roof = TopY - 0.2f;
                Box(new Vector3(0.0f, roof * 0.5f, TopZ + landing + 3.0f), new Vector3(10.0f, roof, 6.0f));
            }
            Physics.SyncTransforms();
        }

        private IEnumerator Spawn(Vector3 at)
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), at, Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _vaulting = _player.GetComponent<PlayerVaulting>();
            for (int i = 0; i < 60; i++)
            {
                _locomotion.Tick(new LocomotionInput(), Step);
                yield return null;
            }
#else
            Assert.Ignore("needs the editor's asset database");
            yield break;
#endif
        }

        /// <summary>Sprints up +Z for <paramref name="seconds"/>, asking for a vault every frame as Shift and W do; true when one started.</summary>
        private IEnumerator Sprint(float seconds, System.Action<bool> vaulted, System.Action<bool> sawStairs = null)
        {
            var input = new LocomotionInput { Move = Vector2.up, Sprint = true };
            bool stairs = false;
            for (float t = 0.0f; t < seconds; t += Step)
            {
                _locomotion.Tick(input, Step);
                yield return null;
                stairs |= _locomotion.OnStair;
                _highest = Mathf.Max(_highest, _player.transform.position.y);
                if (_vaulting.TryStart(true, true, _locomotion.IsGrounded, _locomotion.CurrentSpeed, Step, Vector3.forward))
                {
                    vaulted(true);
                    sawStairs?.Invoke(stairs);
                    yield break;
                }
            }
            vaulted(false);
            sawStairs?.Invoke(stairs);
        }

        [UnityTest]
        public IEnumerator SprintingUpAStairNeverVaultsItsTopStepOrItsLandingsRail([Values(true, false)] bool rail)
        {
            Build(rail);
            yield return Spawn(new Vector3(0.0f, 0.05f, FirstStepZ - 3.0f));
            bool vaulted = false, stairs = false;
            yield return Sprint(3.0f, v => vaulted = v, s => stairs = s);
            Assert.IsTrue(stairs, "setup: the body never knew it was on the stair");
            Assert.IsFalse(vaulted, $"a vault running up the stair, from {_player.transform.position} (the top step at z {TopZ:F2}, y {TopY:F2})");
            Assert.Greater(_highest, TopY - 0.3f, "setup: the body never got up the stair");
        }

        /// <summary>Up the stair and held into the rail, then the run let go of and pressed again: now the rail is vaulted, as anywhere.</summary>
        [UnityTest]
        public IEnumerator LetGoAndRunAgainTheLandingsRailIsVaulted()
        {
            Build(rail: true);
            yield return Spawn(new Vector3(0.0f, 0.05f, FirstStepZ - 3.0f));
            bool vaulted = false;
            yield return Sprint(2.0f, v => vaulted = v);
            Assert.IsFalse(vaulted, "setup: vaulted running up the stair");
            _vaulting.TryStart(false, false, _locomotion.IsGrounded, 0.0f, Step, Vector3.forward);   // Shift and W let go
            _locomotion.Tick(new LocomotionInput(), Step);
            yield return null;
            yield return Sprint(1.0f, v => vaulted = v);
            Assert.IsTrue(vaulted, "pressed again, the run still vaulted nothing");
        }

        [UnityTest]
        public IEnumerator StoodOnTheLandingFirstItsRailIsVaulted()
        {
            Build(rail: true);
            yield return Spawn(new Vector3(0.0f, TopY + 0.05f, TopZ + 0.3f));
            bool vaulted = false;
            yield return Sprint(1.5f, v => vaulted = v);
            Assert.IsTrue(vaulted, "the rail was not vaulted from the landing: the stair holds the vault back for good, or the rail is not vaultable");
        }
    }
}
