using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CityX.Tests
{
    /// <summary>
    /// On the real rig: a climb goes on past what juts from a wall -- the base course over a shop front, a deep cornice -- by leaning out round it
    /// (ClimbJut), and settles back onto the wall above. Before, the first course stopped the body rising and the stuck guard let go.
    /// </summary>
    public sealed class ClimbPastJutPlayTests
    {
        private readonly System.Collections.Generic.List<GameObject> _made = new System.Collections.Generic.List<GameObject>();
        private GameObject _player;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            foreach (GameObject go in _made) if (go != null) Object.Destroy(go);
        }

        private void Box(Vector3 centre, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = centre;
            go.transform.localScale = size;
            _made.Add(go);
        }

        /// <summary>The ground, and a 14 m wall whose near face is at z = 1, with a ledge of this depth sticking out of it from y0 to y1.</summary>
        private void WallWithLedge(float depth, float y0, float y1)
        {
            Box(new Vector3(0.0f, -0.5f, 0.0f), new Vector3(100.0f, 1.0f, 100.0f));
            Box(new Vector3(0.0f, 7.0f, 2.0f), new Vector3(10.0f, 14.0f, 2.0f));
            Box(new Vector3(0.0f, (y0 + y1) * 0.5f, 1.0f - depth * 0.5f), new Vector3(10.0f, y1 - y0, depth));
            Physics.SyncTransforms();
        }

        /// <summary>Grabs the wall and climbs up (mantling wherever the climb finds a ledge) until the feet pass <paramref name="untilY"/> or it ends.</summary>
        private IEnumerator Climb(float seconds, float untilY, System.Action<PlayerClimbing> eachStep)
        {
#if UNITY_EDITOR
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0.0f, 0.05f, 0.3f), Quaternion.identity);
            var climbing = _player.GetComponent<PlayerClimbing>();
            var vitals = _player.GetComponent<PlayerVitals>();
            vitals.InfiniteStamina = true;
            yield return null;
            yield return null;
            Assert.IsTrue(climbing.TryStart(GrabIntent.AlreadyHeld(), true, 100.0f), "setup: could not grab the wall");
            const float dt = 1.0f / 60.0f;
            for (float t = 0.0f; t < seconds && _player.transform.position.y < untilY; t += dt)
            {
                if (climbing.IsClimbing) climbing.Tick(false, true, false, vitals, dt);
                else if (climbing.IsToppingOut) climbing.TickMantle(dt);
                else break;
                eachStep(climbing);
                yield return null;
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AClimbGoesOnPastTheCourseOverTheShopFronts()
        {
            WallWithLedge(0.24f, 3.1f, 3.52f);
            float maxLean = 0.0f;
            PlayerClimbing climbing = null;
            yield return Climb(6.0f, 6.0f, c => { climbing = c; maxLean = Mathf.Max(maxLean, c.Lean); });
            Assert.IsTrue(climbing.IsClimbing, $"the climb let go at y = {_player.transform.position.y:0.0}, under the course");
            Assert.GreaterOrEqual(_player.transform.position.y, 6.0f, "it never got past the course");
            Assert.Greater(maxLean, 0.2f, "it got past without leaning out: was the course there at all?");
            Assert.Less(climbing.Lean, 0.01f, "past the course, the body should be back on the wall");
            Assert.IsTrue(LedgeProbe.HasRoom(_player.transform.position, BodyRig.StandingHeight, 0.3f, _player.transform), "the body is inside geometry");
        }

        [UnityTest]
        public IEnumerator ADeepCorniceIsClimbedPastOrStoodOn()
        {
            // 0.85 m deep is deep enough to stand on: pulling up onto it is as good as climbing round it. Hanging under it, or letting go, is not.
            WallWithLedge(0.85f, 4.0f, 4.7f);
            PlayerClimbing climbing = null;
            yield return Climb(8.0f, 6.0f, c => climbing = c);
            for (int i = 0; i < 120 && climbing.IsToppingOut; i++)
            {
                climbing.TickMantle(1.0f / 60.0f);
                yield return null;
            }
            float y = _player.transform.position.y;
            Assert.Greater(y, 4.6f, $"the climb ended at y = {y:0.0}, under the cornice");
            Assert.IsTrue(climbing.IsClimbing || _player.GetComponent<CharacterController>().enabled, "left hanging with the controller off");
            Assert.IsTrue(LedgeProbe.HasRoom(_player.transform.position, BodyRig.StandingHeight, 0.3f, _player.transform), "the body is inside geometry");
        }

        [UnityTest]
        public IEnumerator ABalconyIsNotLeanedRound()
        {
            WallWithLedge(1.4f, 4.0f, 4.3f);
            float maxLean = 0.0f;
            yield return Climb(6.0f, 6.0f, c => maxLean = Mathf.Max(maxLean, c.Lean));
            Assert.Less(maxLean, 0.01f, "a 1.4 m balcony is too deep to lean round");
            // It may pull up onto the balcony (a ledge like any other), but never pass up through it.
            Assert.IsTrue(LedgeProbe.HasRoom(_player.transform.position, BodyRig.StandingHeight, 0.3f, _player.transform), "the body is inside the balcony");
        }
    }
}
