using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE HEAD WITH A TWO-HANDED SWORD (owner, 2026-10-14, with a screenshot of the greatsword held up and the face tipped to the sky: "fix the
    /// head posture when holding two handed sword why is he looking upward. make him looking straight like the idle position ... make sure the
    /// head isnt too far forward or backward once rotating it ... use the idle head positioning as a guide that one is fine"). Standing with the
    /// greatsword, the head is held as it is standing empty-handed: facing as level, its neck as upright, and the head no further forward or back
    /// over the hips.
    /// </summary>
    public sealed class SwordIdleHeadPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
            {
                if (go != null) Object.Destroy(go);
            }
            _made.Clear();
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
        }

        /// <summary>How the head is held, in the body's own frame (turned with it): degrees, positive up or forward; metres, positive forward.</summary>
        private struct Posture
        {
            public float Pitch;        // where the face points, above the level
            public float NeckLean;     // the neck (neck joint to the head's centre) from upright, forward positive
            public float TorsoLean;    // the spine (spine joint to the neck joint) from upright, forward positive
            public float HeadForward;  // the head's centre ahead of the hips

            public override string ToString() => $"face {Pitch:+0.0;-0.0} deg, neck {NeckLean:+0.0;-0.0} deg, torso {TorsoLean:+0.0;-0.0} deg, head {HeadForward * 100:+0.0;-0.0} cm ahead of the hips";
        }

        private static Posture Measure(GameObject player)
        {
            Animator animator = player.GetComponentInChildren<Animator>();
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            Quaternion toBody = Quaternion.Inverse(Quaternion.Euler(0.0f, player.transform.eulerAngles.y, 0.0f));
            Vector3 face = toBody * head.forward;
            Vector3 neckLine = toBody * (head.position - neck.position);
            Vector3 torsoLine = toBody * (neck.position - spine.position);
            return new Posture
            {
                Pitch = Mathf.Asin(Mathf.Clamp(face.y, -1.0f, 1.0f)) * Mathf.Rad2Deg,
                NeckLean = Mathf.Atan2(neckLine.z, neckLine.y) * Mathf.Rad2Deg,
                TorsoLean = Mathf.Atan2(torsoLine.z, torsoLine.y) * Mathf.Rad2Deg,
                HeadForward = (toBody * (head.position - hips.position)).z,
            };
        }

        /// <summary>Stands for a while, then averages the posture over the last half second (the idles breathe).</summary>
        private static IEnumerator StandAndMeasure(PlayerLocomotion locomotion, LocomotionInput input, System.Action<Posture> result)
        {
            const float dt = 1.0f / 60.0f;
            var sum = new Posture();
            int count = 0;
            for (int frame = 0; frame < 150; frame++)
            {
                locomotion.Tick(input, dt);
                yield return null;
                if (frame >= 120)
                {
                    // Read here, before this frame's animation: the bones still hold last frame's finished pose (after LateUpdate).
                    Posture p = Measure(locomotion.gameObject);
                    sum.Pitch += p.Pitch;
                    sum.NeckLean += p.NeckLean;
                    sum.TorsoLean += p.TorsoLean;
                    sum.HeadForward += p.HeadForward;
                    count++;
                }
            }
            result(new Posture
            {
                Pitch = sum.Pitch / count,
                NeckLean = sum.NeckLean / count,
                TorsoLean = sum.TorsoLean / count,
                HeadForward = sum.HeadForward / count,
            });
        }

        [UnityTest]
        public IEnumerator StandingWithAGreatswordTheHeadIsHeldAsItIsStandingEmptyHanded()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(60.0f, 1.0f, 60.0f);
            _made.Add(ground);
            var playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            GameObject player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _made.Add(player);
            player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = player.GetComponent<PlayerLocomotion>();

            Posture idle = default;
            yield return StandAndMeasure(locomotion, new LocomotionInput(), p => idle = p);

            foreach (string path in new[] { "Assets/CityX/Weapons/SpikeSword.prefab", "Assets/CityX/Weapons/Sword.prefab" })
            {
                var swordPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(swordPrefab, path + " is missing");
                GameObject swordObject = Object.Instantiate(swordPrefab, new Vector3(20.0f, 0.5f, 20.0f), Quaternion.identity);
                _made.Add(swordObject);
                var sword = swordObject.GetComponent<SwordWeapon>();
                if (sword.IsOneHanded)
                {
                    continue;
                }
                sword.OnEquip(player, CityX.Items.ItemInstanceData.None);
                Posture held = default;
                yield return StandAndMeasure(locomotion, new LocomotionInput { SwordEquipped = true }, p => held = p);
                Debug.Log($"CityX head with {swordPrefab.name}: idle {idle}; with the greatsword {held}");

                Assert.AreEqual(idle.Pitch, held.Pitch, 5.0f, $"{swordPrefab.name}: the face is not level as at idle ({held} against {idle})");
                Assert.AreEqual(idle.NeckLean, held.NeckLean, 6.0f, $"{swordPrefab.name}: the neck is craned compared with idle ({held} against {idle})");
                Assert.AreEqual(idle.HeadForward, held.HeadForward, 0.06f, $"{swordPrefab.name}: the head is too far forward or back compared with idle ({held} against {idle})");
                sword.OnUnequip(player);
                Object.Destroy(swordObject);
                yield return StandAndMeasure(locomotion, new LocomotionInput(), _ => { });
            }
#else
            yield break;
#endif
        }
    }
}
