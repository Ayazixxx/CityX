using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Owner, 2026-11-17: "when you spam click, right before the third swing the body glitches a little". The second cut's clip has the sword
    /// arm in it, the third's has not: for one frame the body stood straight up in the third's ready stance (the new clip's first frame drawn
    /// before it was crossfaded) and both hands jumped (to the arm the game had carried on posing unseen under the clip's). Clicking all the
    /// way through the chain, no part of the body jolts more as the third cut takes over than in the cuts themselves.
    /// </summary>
    public sealed class SwordChainHandOverPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>The most a part's frame-to-frame motion may change in one frame as the third cut takes over, metres (it was 29 cm at the hips, 1.4 m at the sword hand).</summary>
        private static readonly Dictionary<HumanBodyBones, float> MostJolt = new Dictionary<HumanBodyBones, float>
        {
            { HumanBodyBones.Hips, 0.05f },
            { HumanBodyBones.Head, 0.10f },
            { HumanBodyBones.RightHand, 0.15f },
            { HumanBodyBones.LeftHand, 0.15f },
            { HumanBodyBones.LeftFoot, 0.10f },
        };

        [UnityTest]
        public IEnumerator SpamClickedTheThirdCutTakesOverWithoutAJolt()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerVitals>().InfiniteStamina = true;
            var locomotion = player.GetComponent<PlayerLocomotion>();
            SwordWeapon sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/CombatSword.prefab"),
                new Vector3(40.0f, 1.0f, 40.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            sword.OnEquip(player, ItemInstanceData.None);
            var combat = player.GetComponent<SoulsSwordCombat>();
            var poser = player.GetComponent<SwordPoseDriver>();
            combat.HitStopSeconds = 0.0f;
            var input = new LocomotionInput { Sword1HEquipped = true };
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    combat.Tick(Dt);
                    locomotion.Tick(input, Dt);
                    yield return null;
                }
                Animator animator = player.GetComponentInChildren<Animator>();
                var parts = new List<HumanBodyBones>(MostJolt.Keys);
                var frames = new List<Vector3[]>();
                int thirdStarts = -1;
                for (int frame = 0; frame < 120 && (thirdStarts < 0 || frame < thirdStarts + 6); frame++)
                {
                    if (frame % 3 == 0) combat.RequestLight(0.0f);   // clicking as fast as a player can
                    combat.Tick(Dt);
                    locomotion.Tick(input, Dt);
                    yield return null;
                    if (thirdStarts < 0 && poser.IsStriking && poser.Kind == SwordStrikeKind.Finisher1H)
                    {
                        thirdStarts = frames.Count;
                    }
                    var at = new Vector3[parts.Count];
                    for (int b = 0; b < parts.Count; b++) at[b] = player.transform.InverseTransformPoint(animator.GetBoneTransform(parts[b]).position);
                    frames.Add(at);
                }
                Assert.GreaterOrEqual(thirdStarts, 2, "the chain never reached its third cut");
                var failures = new List<string>();
                for (int b = 0; b < parts.Count; b++)
                {
                    float worst = 0.0f;
                    for (int i = thirdStarts - 1; i <= thirdStarts + 3; i++)
                    {
                        worst = Mathf.Max(worst, (frames[i + 1][b] - 2.0f * frames[i][b] + frames[i - 1][b]).magnitude);
                    }
                    Debug.Log($"CityX third cut takes over: {parts[b]} jolts {worst * 100.0f:0.0} cm");
                    if (worst > MostJolt[parts[b]])
                    {
                        failures.Add($"{parts[b]} jolts {worst * 100.0f:0} cm in a frame as the third cut takes over");
                    }
                }
                Assert.IsEmpty(failures, string.Join("; ", failures));
            }
            finally
            {
                Object.Destroy(sword.gameObject);
                Object.Destroy(player);
                Object.Destroy(ground);
                Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            }
#else
            yield break;
#endif
        }
    }
}
