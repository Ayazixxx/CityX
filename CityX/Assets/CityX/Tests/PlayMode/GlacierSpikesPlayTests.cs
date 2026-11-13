using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Glacier Spikes (docs/GLACIER_SPIKES.md) through the real keys in the real city: its key arms it and the body turns to where the camera
    /// looks with the arrow on the ground; a left click casts it (mana spent, the cooldown started, no punch thrown); a body on the line is
    /// struck once and chilled, one beside it is not; the key again or a right click puts it away. Then, on a flat floor: another player's copy
    /// grows the same field from the cast it is told of and hurts nobody.
    /// </summary>
    public sealed class GlacierSpikesPlayTests : InputTestFixture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const string PlayerPrefabPath = "Assets/CityX/Character/Player.prefab";

        /// <summary>The bar slot it is put on (key 0).</summary>
        private const int Slot = 9;

        private Scene _scene;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private GlacierSpikesAbility _spikes;
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();

        /// <summary>Counts the blows it takes and how cold each one was.</summary>
        private sealed class Target : MonoBehaviour, IDamageable
        {
            public bool IsAlive => true;
            public int Hits;
            public float LastChill;

            public HitResponse ApplyHit(float amount, Vector3 fromPosition, float hitstunSeconds)
            {
                Hits++;
                LastChill = DamageContext.Chill;
                return HitResponse.Landed;
            }
        }

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            foreach (GlacierSpikesField field in Object.FindObjectsByType<GlacierSpikesField>(FindObjectsSortMode.None)) Object.Destroy(field.gameObject);
            foreach (GlacierSpikesArrow arrow in Object.FindObjectsByType<GlacierSpikesArrow>(FindObjectsSortMode.None)) Object.Destroy(arrow.gameObject);
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        private IEnumerator LoadScene()
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            _vitals = Object.FindFirstObjectByType<PlayerVitals>();
            _locomotion = _vitals.GetComponent<PlayerLocomotion>();
            _spikes = _vitals.GetComponent<GlacierSpikesAbility>();
            Assert.IsNotNull(_spikes, "the player has no Glacier Spikes -- PlayerAbilityWiring, then rebuild the scene");
            Assert.IsTrue(PlayerSkills.On(_vitals.gameObject).PutOnBar(Slot, SkillCatalog.GlacierSpikes), "could not put Glacier Spikes on the bar");
            _vitals.InfiniteHealth = true;
            Time.captureFramerate = 60;
            for (int i = 0; i < 400 && !_locomotion.IsGrounded; i++)
            {
                yield return null;
            }
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
            Assert.IsTrue(_locomotion.IsGrounded, "setup failed -- the player never settled on the ground");
#else
            yield break;
#endif
        }

        private IEnumerator Seconds(float seconds)
        {
            for (float t = 0.0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
            }
        }

        private IEnumerator Tap(ButtonControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        /// <summary>The ground under <paramref name="at"/>, found from above.</summary>
        private static Vector3 OnGround(Vector3 at)
        {
            return Physics.Raycast(at + Vector3.up * 15.0f, Vector3.down, out RaycastHit hit, 40.0f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : at;
        }

        private Target Dummy(Vector3 feet)
        {
            var go = new GameObject("Target");
            go.transform.position = feet + Vector3.up * 1.0f;
            var body = go.AddComponent<CapsuleCollider>();
            body.height = 2.0f;
            body.radius = 0.4f;
            go.AddComponent<Target>();
            _made.Add(go);
            Physics.SyncTransforms();
            return go.GetComponent<Target>();
        }

        /// <summary>A player's body standing at <paramref name="feet"/>, its keys off.</summary>
        private GameObject Body(Vector3 feet)
        {
#if UNITY_EDITOR
            GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath), feet + Vector3.up * 0.1f,
                Quaternion.identity);
            body.GetComponent<PlayerInputReader>().enabled = false;
            _made.Add(body);
            return body;
#else
            return null;
#endif
        }

        [UnityTest]
        public IEnumerator ItsKeyArmsItTurningTheBodyToWhereTheCameraLooksWithTheArrowOnTheGround()
        {
            yield return LoadScene();
            Vector3 heading = OpenRun.Face(_vitals, _made);
            _vitals.transform.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, heading));   // facing away to the side
            yield return null;
            Assert.Less(Vector3.Dot(_vitals.transform.forward, heading), 0.3f, "setup: the body already faced the way the camera looks");

            yield return Tap(_keyboard.digit0Key);
            Assert.IsTrue(_spikes.IsArmed, "its key did not arm it");
            yield return Seconds(0.4f);
            Assert.IsTrue(_spikes.IsArmed, "it did not stay armed");
            Assert.Greater(Vector3.Dot(_vitals.transform.forward, heading), 0.98f, "the body did not turn to where the camera looks");
            Assert.IsNotNull(_spikes.Arrow, "no arrow");
            Assert.IsTrue(_spikes.Arrow.IsShowing, "the arrow is not on the ground");
            Assert.Greater(_spikes.Aimed.Length, GlacierSpikesRules.Range * 0.9f, "the arrow does not reach down the open street");
            Assert.Greater(Vector3.Dot(_spikes.Aimed.Direction, heading), 0.99f, "the arrow does not point where the camera looks");
            Vector3 fromFeet = _spikes.Aimed.Start - _vitals.transform.position;
            Assert.Less(Mathf.Abs(fromFeet.y), 0.3f, "the line does not start on the ground at the feet");
            Assert.AreEqual(0.0f, _spikes.CooldownLeft, "arming started the cooldown");
        }

        [UnityTest]
        public IEnumerator AClickCastsItSpendingManaAndStartingTheCooldownAndThrowsNoPunch()
        {
            yield return LoadScene();
            Vector3 heading = OpenRun.Face(_vitals, _made);
            yield return Tap(_keyboard.digit0Key);
            yield return Seconds(0.3f);
            float manaBefore = _vitals.Mana;
            var melee = _vitals.GetComponent<MeleeAttacker>();

            Press(_mouse.leftButton);
            yield return null;
            Assert.IsFalse(_spikes.IsArmed, "the click did not cast it");
            Assert.IsNotNull(_spikes.LastField, "no field grew");
            float spent = manaBefore - _vitals.Mana;
            Assert.AreEqual(GlacierSpikesRules.ManaCost, spent, 0.5f, "it did not pay its mana");
            Assert.Greater(_spikes.CooldownLeft, GlacierSpikesRules.CooldownSeconds - 0.2f, "the cooldown did not start");
            Assert.Greater(Vector3.Dot(_spikes.LastField.Cast.Direction, heading), 0.98f, "it was not cast where the camera looks");
            yield return Seconds(0.3f);
            Release(_mouse.leftButton);
            yield return null;
            Assert.IsFalse(melee != null && melee.IsSwinging, "the click was also a punch");

            yield return Tap(_keyboard.digit0Key);
            Assert.IsFalse(_spikes.IsArmed, "armed again while it cools down");
            yield return Seconds(1.0f);
            Assert.Greater(_spikes.LastField.Drawn, GlacierSpikesRules.SpikeCount / 2, "the crystals are not standing");
        }

        [UnityTest]
        public IEnumerator ABodyOnTheLineIsStruckOnceAndChilledAndOneBesideItIsNot()
        {
            yield return LoadScene();
            Vector3 heading = OpenRun.Face(_vitals, _made);
            Vector3 side = Vector3.Cross(Vector3.up, heading);
            Vector3 feet = _vitals.transform.position;
            GameObject onTheLine = Body(OnGround(feet + heading * 9.0f));
            GameObject beside = Body(OnGround(feet + heading * 9.0f + side * 5.5f));
            Target dummy = Dummy(OnGround(feet + heading * 14.0f + side * 0.8f));
            yield return Seconds(0.5f);
            var struckVitals = onTheLine.GetComponent<PlayerVitals>();
            var besideVitals = beside.GetComponent<PlayerVitals>();
            float struckBefore = struckVitals.Health;
            float besideBefore = besideVitals.Health;

            yield return Tap(_keyboard.digit0Key);
            yield return Seconds(0.2f);
            yield return Tap(_mouse.leftButton);
            Assert.IsNotNull(_spikes.LastField, "it was not cast");
            float coldest = 0.0f;
            var chill = onTheLine.GetComponent<Chill>();
            for (float t = 0.0f; t < 1.5f; t += Time.deltaTime)
            {
                coldest = Mathf.Max(coldest, chill.Level);
                yield return null;
            }
            TestContext.WriteLine($"struck {struckBefore - struckVitals.Health:0.0}, beside {besideBefore - besideVitals.Health:0.0}, dummy hits {dummy.Hits}, " +
                                  $"coldest {coldest:0.00}, struck in all {_spikes.StruckCount}");
            Assert.Greater(struckBefore - struckVitals.Health, GlacierSpikesRules.Damage * 0.5f, "the body on the line was not hurt");
            Assert.AreEqual(GlacierSpikesRules.ChillAmount, coldest, 0.05f, "the body on the line was not chilled by it");
            Assert.AreEqual(1, dummy.Hits, "the dummy on the line was not struck exactly once");
            Assert.AreEqual(GlacierSpikesRules.ChillAmount, dummy.LastChill, 1e-4f, "its blow carried no chill");
            Assert.AreEqual(besideBefore, besideVitals.Health, 0.01f, "the body beside the band was hurt");
            Assert.AreEqual(0.0f, beside.GetComponent<Chill>().Level, 1e-4f, "the body beside the band was chilled");
        }

        [UnityTest]
        public IEnumerator TheKeyAgainOrARightClickPutsItAwayWithoutCasting()
        {
            yield return LoadScene();
            OpenRun.Face(_vitals, _made);
            float manaBefore = _vitals.Mana;
            yield return Tap(_keyboard.digit0Key);
            Assert.IsTrue(_spikes.IsArmed);
            yield return Seconds(0.2f);
            yield return Tap(_keyboard.digit0Key);
            Assert.IsFalse(_spikes.IsArmed, "the key again did not put it away");
            Assert.IsFalse(_spikes.Arrow.IsShowing, "the arrow stayed on the ground");

            yield return Tap(_keyboard.digit0Key);
            Assert.IsTrue(_spikes.IsArmed, "it could not be armed again");
            yield return Seconds(0.2f);
            yield return Tap(_mouse.rightButton);
            Assert.IsFalse(_spikes.IsArmed, "a right click did not put it away");
            yield return Seconds(0.2f);
            Assert.IsNull(_spikes.LastField, "putting it away cast it");
            Assert.AreEqual(0.0f, _spikes.CooldownLeft, "putting it away started the cooldown");
            Assert.GreaterOrEqual(_vitals.Mana, manaBefore - 0.01f, "putting it away spent mana");
        }

        /// <summary>
        /// The caster's machine casts and strikes; another player's copy of the caster, told of the cast, grows the very same field and strikes
        /// nobody: the dummy on the line is struck once, by the caster's field alone.
        /// </summary>
        [UnityTest]
        public IEnumerator AnotherPlayersCopyGrowsTheSameFieldAndHurtsNobody()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(120.0f, 1.0f, 120.0f);
            _made.Add(ground);
            var owner = new GameObject("Caster");
            owner.transform.position = new Vector3(0.0f, 0.02f, 0.0f);
            _made.Add(owner);
            var copy = new GameObject("CopyOfTheCaster");
            copy.transform.position = owner.transform.position;
            _made.Add(copy);
            var cast = owner.AddComponent<GlacierSpikesAbility>();
            var shown = copy.AddComponent<GlacierSpikesAbility>();
            Target dummy = Dummy(new Vector3(0.3f, 0.0f, 8.0f));
            AbilityEvent told = default;
            int sent = 0;
            cast.Fired += e =>
            {
                told = e;
                sent++;
            };
            Physics.SyncTransforms();
            yield return null;

            Assert.IsTrue(cast.TryArm());
            cast.Aim(Vector3.forward);
            Assert.IsTrue(cast.TryCast(), "the caster could not cast on a flat floor");
            Assert.AreEqual(1, sent, "the cast was not told to other players once");
            shown.PlayRemote(told);
            Assert.IsNotNull(shown.LastField, "the copy grew no field");

            GlacierSpikesField real = cast.LastField;
            GlacierSpikesField copied = shown.LastField;
            Assert.AreEqual(real.Cast.Length, copied.Cast.Length, 1e-4f);
            Assert.AreEqual(real.Cast.Seed, copied.Cast.Seed);
            foreach (float age in new[] { 0.2f, 0.7f, 2.0f, 4.6f })
            {
                for (int i = 0; i < real.Count; i++)
                {
                    GlacierSpikePose a = real.PoseOf(i, age);
                    GlacierSpikePose b = copied.PoseOf(i, age);
                    Assert.AreEqual(a.Visible, b.Visible, $"crystal {i} at {age} s");
                    Assert.Less(Vector3.Distance(a.Position, b.Position), 1e-4f, $"crystal {i} stands elsewhere on the copy at {age} s");
                    Assert.Less(Vector3.Distance(a.Size, b.Size), 1e-4f);
                }
            }
            yield return Seconds(1.5f);
            Assert.AreEqual(1, dummy.Hits, "struck other than once: only the caster's machine strikes, once");
            Assert.AreEqual(0, shown.StruckCount, "the copy struck someone");
            Assert.AreEqual(0.0f, shown.CooldownLeft, "the copy started a cooldown of its own");
        }
    }
}
