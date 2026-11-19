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
    /// Owner, 2026-10-19, with screenshots: the bat's carry had "the elbows clipping through the body" (every two-handed weapon did), and "after
    /// you swing the crowbar it leave it in this weird arm position" (the hand up over the shoulder). Holding each, standing and after a swing:
    /// both arms' elbows and bones stay out of the torso (ElbowClearance), and a one-handed weapon's ready stance keeps the hand below the shoulder.
    /// </summary>
    public sealed class SwordArmsClearPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>How far into the torso an arm may still sink: the soft skin of the elbow brushing the side.</summary>
        private const float Touch = 0.03f;

        /// <summary>How far along the blade, from the hand to the tip (0-1), it is held out of the body: the handle is in the hand.</summary>
        private const float BladeFromShare = 0.2f;

        /// <summary>
        /// How far the blade stays off the skin, metres (owner, 2026-11-14, of a blade measured just touching the hip: "the third attack ...
        /// still sends the sword through the users body"): grazing it reads as going through it.
        /// </summary>
        private const float BladeRoom = 0.06f;

        /// <summary>A spear's shaft, tucked under the arm, is kept this far out of the skin: clear of it, no more.</summary>
        private const float SpearRoom = 0.01f;

        private GameObject _player;
        private GameObject _ground;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
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
            foreach (string weapon in new[] { "SpikeSword", "BaseballBat", "CombatSword", "MetalCrowbar" })
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

        /// <summary>
        /// Owner, 2026-11-13, of a draft of the full-body swings: "why are his wrists all twisted and poking through his torso". Every frame of
        /// every attack with each kind of sword, under the attack's own clip of the legs and torso: the arms on the handle stay out of the
        /// torso and out of the head, and no wrist goes deeper into the body than the stance rests it.
        /// </summary>
        [UnityTest]
        public IEnumerator ThroughEverySwingTheArmsStayOutOfTheBodyAndTheHead()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            var failures = new System.Collections.Generic.List<string>();
            var report = new System.Text.StringBuilder();
            foreach (string weapon in new[] { "SpikeSword", "BaseballBat", "CombatSword", "MetalCrowbar", "Sledgehammer", "Spear" })
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
                var poser = _player.GetComponent<SwordPoseDriver>();
                combat.HitStopSeconds = 0.0f;
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips), neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                for (int i = 0; i < 60; i++)
                {
                    combat.Tick(Dt);
                    locomotion.Tick(input, Dt);
                    yield return null;
                }

                // The whole light chain (each next cut buffered as the last one lands) and the heavy, standing; then on the move, the keys
                // held all through: the first light at a walk, the running attack at a sprint, and the whole light chain at a walk.
                for (int pass = 0; pass < 5; pass++)
                {
                    int cuts = pass == 0 || pass == 4 ? combat.LightChainLength : 1;
                    LocomotionInput keys = input;
                    if (pass >= 2)
                    {
                        keys.Move = Vector2.up;
                        keys.Sprint = pass == 3;
                        for (int i = 0; i < 80; i++)
                        {
                            combat.Tick(Dt);
                            locomotion.Tick(keys, Dt);
                            yield return null;
                        }
                    }
                    float worstTorso = float.MaxValue, worstHand = float.MaxValue, worstHead = float.MaxValue, worstBlade = float.MaxValue;
                    string torsoWhen = "", handWhen = "", headWhen = "", bladeWhen = "";
                    bool clipHeld = false;
                    string lastThrown = "";
                    for (int cut = 0; cut < cuts; cut++)
                    {
                        if (pass == 1) combat.RequestHeavy(0.0f); else combat.RequestLight(0.0f);
                        yield return null;
                        combat.Tick(Dt);
                        SoulsAttack attack = combat.CurrentAttack;
                        lastThrown = attack.Animation;
                        float until = cut < cuts - 1 ? attack.Startup + attack.Active + attack.Recovery * attack.ChainAt + 0.02f : attack.Total + 0.3f;
                        for (float t = 0.0f; t < until; t += Dt)
                        {
                            combat.Tick(Dt);
                            locomotion.Tick(keys, Dt);
                            yield return null;
                            clipHeld |= poser.ClipWeight > 0.99f;
                            if (pass >= 2 && poser.ClipWeight > 0.99f && poser.ClipLegs > 0.01f)
                            {
                                failures.Add($"{weapon}, pass {pass}: the attack's clip has the legs of a body that is on the move ({poser.ClipLegs:0.00})");
                                break;
                            }
                            if (pass >= 2 && poser.ClipWeight < 0.99f)
                            {
                                // Thrown on the move, the body is still coming out of the pose it ran in with until the clip has it, and a
                                // two-handed weapon trailed at a sprint has its left forearm across the belly (the carry's own, 9 cm in):
                                // not the attack's.
                                continue;
                            }
                            string when = $"{attack.Animation} at {t:0.00} s";
                            // The blade along its length, past the hand that holds it, out of the body (owner, 2026-11-14: "on the third
                            // swing, the sword goes through his body", twice): measured against the body's own shape, the pieces its skin is
                            // made from (FusedBodyShape.Segments) at the size the body is, all but the sword arm, and the head.
                            var blade = (ISwordBlade)sword;
                            float bodyScale = Vector3.Distance(hips.position, neck.position)
                                / Vector3.Distance(BodyRig.ShapePositionOf(HumanBodyBones.Hips), BodyRig.ShapePositionOf(HumanBodyBones.Neck));
                            List<FusedBodyShape.Segment> body = FusedBodyShape.Segments(b => animator.GetBoneTransform(b).position,
                                FusedBodyShape.ReferenceHeight * bodyScale);
                            // (The arms on the handle are where the blade begins: the sword arm's forearm, and with two hands both arms.)
                            body.RemoveAll(s => s.Bone == HumanBodyBones.RightLowerArm || s.Bone == HumanBodyBones.RightHand
                                || (!oneHanded && (s.Bone == HumanBodyBones.RightUpperArm || s.Bone == HumanBodyBones.LeftUpperArm
                                    || s.Bone == HumanBodyBones.LeftLowerArm || s.Bone == HumanBodyBones.LeftHand))
                                // (A spear's front hand holds the shaft itself, ahead of the rear one: its fingers are round what is measured.)
                                || (blade.IsSpear && s.Bone >= HumanBodyBones.LeftThumbProximal && s.Bone <= HumanBodyBones.LeftLittleDistal));
                            for (float along = BladeFromShare; along <= 1.0001f; along += 0.1f)
                            {
                                Vector3 point = Vector3.Lerp(blade.BladeBaseWorld, blade.BladeTipWorld, along);
                                float inBody = FusedBodyShape.Distance(body, point);
                                float inHead = Vector3.Distance(point, head.position) - FusedBodyShape.HeadRadius * bodyScale;
                                float out_ = Mathf.Min(inBody, inHead);
                                if (out_ < worstBlade)
                                {
                                    worstBlade = out_;
                                    bladeWhen = $"{(inBody < inHead ? FusedBodyShape.NearestBone(body, point).ToString() : "head")}, {along:0.0} along the blade, {when}";
                                }
                            }
                            foreach ((string side, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand) in new[]
                            {
                                ("right", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
                                ("left", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                            })
                            {
                                if (oneHanded && side == "left")
                                {
                                    continue;   // the free arm works against the blade, placed by the attack's own keys
                                }
                                Vector3 shoulder = animator.GetBoneTransform(upper).position, elbow = animator.GetBoneTransform(lower).position;
                                Vector3 wrist = animator.GetBoneTransform(hand).position;
                                // The elbow and the two bones, as the game keeps them clear (ElbowClearance); and the wrist itself against the torso.
                                float torso = ElbowClearance.Clearance(shoulder, elbow, wrist, hips.position, neck.position, FusedBodyShape.TorsoRadius + FusedBodyShape.ArmRadius * 0.8f);
                                if (torso < worstTorso)
                                {
                                    worstTorso = torso;
                                    torsoWhen = $"{side} arm, {when}";
                                }
                                float handOut = ElbowClearance.Clearance(wrist, wrist, wrist, hips.position, neck.position, FusedBodyShape.TorsoRadius);
                                if (handOut < worstHand)
                                {
                                    worstHand = handOut;
                                    handWhen = $"{side} wrist, {when}";
                                }
                                foreach (Vector3 point in new[] { elbow, (elbow + wrist) * 0.5f, wrist })
                                {
                                    float out_ = Vector3.Distance(point, head.position) - FusedBodyShape.HeadRadius - FusedBodyShape.WristRadius;
                                    if (out_ < worstHead)
                                    {
                                        worstHead = out_;
                                        headWhen = $"{side} arm, {when}";
                                    }
                                }
                            }
                        }
                    }
                    string which = pass == 0 ? "light chain" : pass == 1 ? "heavy" : pass == 2 ? "first light at a walk" : pass == 3 ? "running attack" : "light chain at a walk";
                    if (pass >= 2 && pass <= 3 && (lastThrown == combat.LightAttack(0).Animation) != (pass == 2)) failures.Add($"{weapon} {which}: {lastThrown} was thrown");
                    report.AppendLine($"{weapon} {which}: arms clear the torso by {worstTorso * 100.0f:0} cm at the least ({torsoWhen}), the wrists by {worstHand * 100.0f:0} cm ({handWhen}), the head by {worstHead * 100.0f:0} cm ({headWhen})");
                    if (!clipHeld) failures.Add($"{weapon} {which}: the attack's clip never held the body");
                    if (worstTorso < -SwingTouch) failures.Add($"{weapon} {which}: an arm is {-worstTorso * 100.0f:0.0} cm inside the torso ({torsoWhen})");
                    if (worstHand < -RestingHandDepth) failures.Add($"{weapon} {which}: a wrist is {-worstHand * 100.0f:0} cm inside the torso ({handWhen})");
                    if (worstHead < -Touch) failures.Add($"{weapon} {which}: an arm is {-worstHead * 100.0f:0} cm inside the head ({headWhen})");
                    report.AppendLine($"{weapon} {which}: the blade clears the body by {worstBlade * 100.0f:0} cm at the least ({bladeWhen})");
                    // (A spear is couched: its shaft lies along the ribs under the arm. Out of the body, but with no room asked between.)
                    float room = ((ISwordBlade)sword).IsSpear ? SpearRoom : BladeRoom;
                    if (worstBlade < room) failures.Add($"{weapon} {which}: the blade passes {worstBlade * 100.0f:0} cm from the skin, under the {room * 100.0f:0} cm it keeps ({bladeWhen})");
                    for (int i = 0; i < 240; i++)
                    {
                        combat.Tick(Dt);
                        locomotion.Tick(input, Dt);
                        yield return null;
                    }
                    // (Walked 80 frames up to its pace and through a swing: back to the middle of the ground for the next.)
                    _player.GetComponent<CharacterController>().enabled = false;
                    _player.transform.position = new Vector3(0.0f, 0.05f, 0.0f);
                    _player.GetComponent<CharacterController>().enabled = true;
                }
                Object.Destroy(sword.gameObject);
                Object.Destroy(_player);
                _player = null;
                yield return null;
            }
            Debug.Log($"CityX sword arms through every swing:\n{report}");
            Assert.IsEmpty(failures, string.Join("; ", failures));
#else
            yield break;
#endif
        }

        /// <summary>
        /// A two-handed weapon's ready stance rests the lower hand against the belly, its wrist's bone 5 cm inside the torso's round (so it
        /// was before the attacks had clips); a swing may sink it no deeper than that and a touch.
        /// </summary>
        private const float RestingHandDepth = 0.07f;

        /// <summary>
        /// In the fastest of a swing an elbow brushes the side a little deeper than standing: 4 cm, as the two-handed chain's second cut had
        /// it before the attacks had clips.
        /// </summary>
        private const float SwingTouch = 0.04f;
    }
}
