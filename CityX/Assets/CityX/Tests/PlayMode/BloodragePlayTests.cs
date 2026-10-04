using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Bloodrage in the real scene, from the real key: 8 starts it, the animator really runs the Bloodrage walk, run and jump clips for it
    /// (and the ordinary ones again after), an aura appears and fades, and the body really moves faster.
    /// </summary>
    public sealed class BloodragePlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private Scene _scene;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private IEnumerator Load(System.Action<PlayerInputReader, PlayerLocomotion, BloodrageAbility> then)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            var input = Object.FindFirstObjectByType<PlayerInputReader>();
            Assert.IsNotNull(input, "no player");
            var locomotion = input.GetComponent<PlayerLocomotion>();
            var rage = input.GetComponent<BloodrageAbility>();
            Assert.IsNotNull(rage, "the player has no Bloodrage -- rebuild the player rig and the scene");
            for (int i = 0; i < 300 && !locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            Assert.IsTrue(locomotion.IsGrounded, "setup failed -- the real player never settled on the ground");
            then(input, locomotion, rage);
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator PressingEightSwapsTheAnimationsStartsTheAuraAndEndingPutsEverythingBack()
        {
            PlayerInputReader input = null;
            BloodrageAbility rage = null;
            yield return Load((i, l, r) => { input = i; rage = r; });

            Animator animator = input.GetComponentInChildren<Animator>();
            Assert.IsNotNull(animator);
            Assert.AreEqual(3, rage.OriginalClips.Length, "walking, running and jumping");
            foreach (AnimationClip clip in rage.BloodrageClips)
            {
                Assert.IsNotNull(clip, "a Bloodrage clip is missing from the player");
            }

            Press(_keyboard.digit8Key);
            yield return null;
            Release(_keyboard.digit8Key);
            yield return null;
            Assert.IsTrue(rage.IsActive, "8 did not start Bloodrage");

            var live = animator.runtimeAnimatorController as AnimatorOverrideController;
            Assert.IsNotNull(live, "the animator is not running the override controller");
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            live.GetOverrides(pairs);
            for (int c = 0; c < rage.OriginalClips.Length; c++)
            {
                AnimationClip standingIn = null;
                foreach (var pair in pairs)
                {
                    if (pair.Key == rage.OriginalClips[c])
                    {
                        standingIn = pair.Value;
                    }
                }
                Assert.AreSame(rage.BloodrageClips[c], standingIn, $"clip {c}: the Bloodrage animation is not standing in for the ordinary one");
            }

            // The aura exists while it runs.
            Assert.IsNotNull(input.transform.Find("BloodrageAura(Clone)"), "no aura");

            rage.Tick(BloodrageAbility.DurationSeconds + 0.1f);
            Assert.IsFalse(rage.IsActive);
            live.GetOverrides(pairs);
            foreach (var pair in pairs)
            {
                if (System.Array.IndexOf(rage.OriginalClips, pair.Key) >= 0)
                {
                    Assert.IsNull(pair.Value, "an ordinary clip is still replaced after Bloodrage");
                }
            }

            rage.Tick(BloodrageAbility.AuraFadeOutSeconds + 0.2f);
            yield return null;
            Assert.IsNull(input.transform.Find("BloodrageAura(Clone)"), "the aura did not fade away");
        }

        [UnityTest]
        public IEnumerator TheBodyReallyWalksAQuarterFasterWhileItLasts()
        {
            PlayerInputReader input = null;
            PlayerLocomotion locomotion = null;
            BloodrageAbility rage = null;
            yield return Load((i, l, r) => { input = i; locomotion = l; rage = r; });

            // Walks forward holding W (the real key, through the real reader), measuring the second after a second of getting up to speed.
            IEnumerator WalkAndMeasure(System.Action<float> result)
            {
                Press(_keyboard.wKey);
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                }
                Vector3 from = locomotion.transform.position;
                float t0 = Time.time;
                for (int i = 0; i < 60; i++)
                {
                    yield return null;
                }
                Vector3 to = locomotion.transform.position;
                float seconds = Time.time - t0;
                Release(_keyboard.wKey);
                float flat = Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z));
                result(flat / Mathf.Max(seconds, 0.0001f));
            }

            float normal = 0.0f, raging = 0.0f;
            yield return WalkAndMeasure(v => normal = v);

            // Back the way it came (backwards, not measured), then the same walk with Bloodrage on.
            Press(_keyboard.sKey);
            for (int i = 0; i < 150; i++)
            {
                yield return null;
            }
            Release(_keyboard.sKey);
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Assert.IsTrue(rage.TryActivate());
            yield return WalkAndMeasure(v => raging = v);

            Assert.Greater(normal, 1.5f, "the body hardly moved");
            Assert.AreEqual(1.25f, raging / normal, 0.12f, $"normal {normal:0.00} m/s, raging {raging:0.00} m/s");
        }
    }
}
