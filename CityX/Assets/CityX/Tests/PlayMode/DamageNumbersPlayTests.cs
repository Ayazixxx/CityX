using System.Collections;
using System.Linq;
using CityX.Character;
using CityX.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace CityX.Tests
{
    /// <summary>
    /// The damage numbers on a real frame (owner, 2026-10-24: "try to recreate it how fortnite does it, make it turn more red based on the
    /// amount of the enemies healthbar you are taking away with that 1 attack"): only your own blows, at the spot they hit, red by the share of
    /// the bar, a burn's ticks adding into one number, each gone within a second.
    /// </summary>
    public sealed class DamageNumbersPlayTests
    {
        private GameObject _player;
        private GameObject _other;
        private GameObject _cameraObject;
        private GameObject _numbersObject;
        private DamageNumbers _numbers;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _other = new GameObject("SomeoneElse");
            _cameraObject = new GameObject("Camera");
            Camera camera = _cameraObject.AddComponent<Camera>();
            _cameraObject.transform.position = new Vector3(0.0f, 1.5f, -4.0f);
            _numbersObject = new GameObject("DamageNumbers", typeof(RectTransform));
            _numbers = _numbersObject.AddComponent<DamageNumbers>();
            _numbers.Player = _player.transform;
            _numbers.View = camera;
            Time.captureFramerate = 60;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            Object.Destroy(_player);
            Object.Destroy(_other);
            Object.Destroy(_cameraObject);
            Object.Destroy(_numbersObject);
            foreach (SparringDummy dummy in Object.FindObjectsByType<SparringDummy>(FindObjectsSortMode.None))
            {
                Object.Destroy(dummy.gameObject);
            }
        }

        private static SparringDummy Dummy(float maxHealth, Vector3 at)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.transform.position = at;
            var dummy = body.AddComponent<SparringDummy>();
            dummy.MaxHealth = maxHealth;
            dummy.Reset();
            return dummy;
        }

        private static void Hit(IDamageable target, float amount, GameObject striker)
        {
            DamageContext.Strike(target, amount, Vector3.zero, 0.2f, DamageKind.Physical, 0.0f, null, striker);
        }

        [UnityTest]
        public IEnumerator YourBlowShowsItsDamageWhereItHitAndSomeoneElsesDoesNot()
        {
            SparringDummy dummy = Dummy(100.0f, Vector3.zero);
            yield return null;

            Hit(dummy, 20.0f, _other);
            Hit(dummy, 20.0f, null);
            Assert.AreEqual(0, _numbers.ShownCount, "someone else's blow, or a forwarded one, showed a number");

            Hit(dummy, 20.0f, _player);
            Assert.AreEqual(1, _numbers.ShownCount, "your blow showed no number");
            Text label = _numbers.ShownLabels.First();
            Assert.AreEqual("20", label.text);
            yield return null;
            Assert.IsTrue(label.enabled, "the number is not drawn");
            Vector3 hitOnScreen = _cameraObject.GetComponent<Camera>().WorldToScreenPoint(DamageDealt.PointOn(dummy));
            Assert.Less(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(null, label.rectTransform.position), hitOnScreen), 120.0f,
                "the number is not by the spot it hit");
        }

        [UnityTest]
        public IEnumerator TheSameBlowIsRedOnAWeakOpponentAndWhiteOnAStrongOne()
        {
            SparringDummy weak = Dummy(40.0f, new Vector3(-1.0f, 0.0f, 0.0f));
            SparringDummy strong = Dummy(2000.0f, new Vector3(1.0f, 0.0f, 0.0f));
            yield return null;
            Hit(weak, 20.0f, _player);
            Hit(strong, 20.0f, _player);
            Text[] labels = _numbers.ShownLabels.ToArray();
            Assert.AreEqual(2, labels.Length);
            Assert.Greater(labels[0].color.r - labels[0].color.g, 0.7f, $"half a weak opponent's bar shows {labels[0].color}, not red");
            Assert.Less(labels[1].color.r - labels[1].color.g, 0.05f, $"a hundredth of a strong opponent's bar shows {labels[1].color}, not white");
            Assert.Greater(labels[0].fontSize, labels[1].fontSize, "the big bite is not bigger");
        }

        [UnityTest]
        public IEnumerator ABurnsTicksAddIntoOneNumberAndAnAutomaticsBulletsEachGetTheirOwn()
        {
            SparringDummy dummy = Dummy(1000.0f, Vector3.zero);
            yield return null;
            for (int i = 0; i < 10; i++)   // a flamethrower: a little every frame
            {
                Hit(dummy, 1.5f, _player);
                yield return null;
            }
            Assert.AreEqual(1, _numbers.ShownCount, "a burn's ticks each made a number");
            Assert.AreEqual("15", _numbers.ShownLabels.First().text, "the burn's number does not add up its ticks");

            for (int i = 0; i < 40; i++)
            {
                yield return null;
            }
            for (int i = 0; i < 3; i++)   // an automatic: a bullet every tenth of a second
            {
                Hit(dummy, 30.0f, _player);
                for (int f = 0; f < 6; f++)
                {
                    yield return null;
                }
            }
            Assert.AreEqual(3, _numbers.ShownLabels.Count(l => l.text == "30"), "an automatic's bullets did not each get a number");
        }

        [UnityTest]
        public IEnumerator ANumberIsGoneWithinASecondAndABlockedBlowShowsNone()
        {
            SparringDummy dummy = Dummy(100.0f, Vector3.zero);
            yield return null;
            Hit(dummy, 20.0f, _player);
            Assert.AreEqual(1, _numbers.ShownCount);
            for (int i = 0; i < 60; i++)
            {
                yield return null;
            }
            Assert.AreEqual(0, _numbers.ShownCount, "the number outlived a second");

            var blocker = new GameObject("Blocker").AddComponent<PlayerVitals>();
            blocker.Roll = () => 1.0f;
            blocker.Revive();
            blocker.StartBlock();
            Hit(blocker, 20.0f, _player);
            Assert.AreEqual(0, _numbers.ShownCount, "a blocked blow showed a number");
            Object.Destroy(blocker.gameObject);
        }

        [UnityTest]
        public IEnumerator ASwordCutNamesItsWielder()
        {
            SparringDummy dummy = Dummy(100.0f, new Vector3(0.0f, 1.0f, 1.5f));
            _player.AddComponent<PlayerVitals>().Revive();
            var combat = _player.AddComponent<SoulsSwordCombat>();
            combat.Equip(null);
            combat.HitStopSeconds = 0.0f;
            yield return null;

            combat.RequestLight(0.0f);
            for (float t = 0.0f; t < combat.Light1.Startup + combat.Light1.Active + 0.05f; t += 1.0f / 60.0f)
            {
                combat.Tick(1.0f / 60.0f);
            }
            Assert.Greater(dummy.TimesHit, 0, "the cut missed the dummy");
            Assert.AreEqual(1, _numbers.ShownCount, "the sword's cut showed no number");
        }
    }
}
