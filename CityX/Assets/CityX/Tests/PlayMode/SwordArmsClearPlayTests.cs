using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Owner, 2026-10-19, with screenshots: the bat's carry had "the elbows clipping through the body" (every two-handed weapon did), and "after
    /// you swing the crowbar it leave it in this weird arm position" (the hand up over the shoulder). Holding each, standing and after a swing:
    /// both arms' elbows and bones stay out of the torso (ElbowClearance), and a one-handed weapon's ready stance keeps the hand below the shoulder.
    /// </summary>
    public sealed class SwordArmsClearPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>How far into the torso an arm may still sink: the soft skin of the elbow brushing the side.</summary>
        private const float Touch = 0.03f;

        private GameObject _player;
        private GameObject _ground;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = 0;
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        [UnityTest]
        public IEnumerator HoldingEachSwordStandingAndAfterASwingTheArmsStayOutOfTheBody()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            var failures = new System.Collections.Generic.List<string>();
            foreach (string weapon in new[] { "Sword", "SpikeSword", "BaseballBat", "CombatSword", "MetalCrowbar" })
            {
                _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                    new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
                _player.GetComponent<PlayerInputReader>().enabled = false;
                _player.GetComponent<PlayerVitals>().InfiniteStamina = true;
                var locomotion = _player.GetComponent<PlayerLocomotion>();
                var sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Weapons/{weapon}.prefab"),
                    new Vector3(30.0f, 1.0f, 30.0f), Quaternion.identity).GetComponent<SwordWeapon>();
                sword.OnEquip(_player, CityX.Items.ItemInstanceData.None);
                bool oneHanded = ((ISwordBlade)sword).IsOneHanded;
                var input = new LocomotionInput { SwordEquipped = !oneHanded, Sword1HEquipped = oneHanded };
                Animator animator = _player.GetComponentInChildren<Animator>();
                var combat = _player.GetComponent<SoulsSwordCombat>();
                combat.HitStopSeconds = 0.0f;
                for (int phase = 0; phase < 2; phase++)
                {
                    if (phase == 1)
                    {
                        combat.RequestLight(0.0f);
                    }
                    for (float t = 0.0f; t < 1.6f; t += Dt)
                    {
                        combat.Tick(Dt);
                        locomotion.Tick(input, Dt);
                        yield return null;
                    }
                    string when = phase == 0 ? "standing" : "after a swing";
                    Vector3 bottom = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                    Vector3 top = animator.GetBoneTransform(HumanBodyBones.Neck).position;
                    float radius = FusedBodyShape.TorsoRadius + FusedBodyShape.ArmRadius * 0.8f;
                    foreach ((string side, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand) in new[]
                    {
                        ("right", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
                        ("left", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                    })
                    {
                        if (oneHanded && side == "left")
                        {
                            continue;   // the free hand hangs with the clips
                        }
                        float clear = ElbowClearance.Clearance(animator.GetBoneTransform(upper).position, animator.GetBoneTransform(lower).position,
                            animator.GetBoneTransform(hand).position, bottom, top, radius);
                        if (clear < -Touch)
                        {
                            failures.Add($"{weapon} {when}: the {side} arm is {-clear * 100.0f:0} cm inside the body");
                        }
                    }
                    if (oneHanded && phase == 1)
                    {
                        float above = animator.GetBoneTransform(HumanBodyBones.RightHand).position.y - animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position.y;
                        if (above > 0.0f)
                        {
                            failures.Add($"{weapon} after a swing: the hand is {above * 100.0f:0} cm above the shoulder");
                        }
                    }
                }
                Object.Destroy(sword.gameObject);
                Object.Destroy(_player);
                _player = null;
                yield return null;
            }
            Assert.IsEmpty(failures, string.Join("; ", failures));
#else
            yield break;
#endif
        }
    }
}
