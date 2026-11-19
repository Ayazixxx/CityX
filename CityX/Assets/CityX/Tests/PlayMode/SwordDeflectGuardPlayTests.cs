using System.Collections;
using CityX.Character;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// The held sword guard knocked to a new angle by each bullet it turns aside (docs/BULLET_DEFLECTION.md), on the real body: the blade
    /// snaps there within the snap's time and holds it until the next bullet; a bullet that gets through the guard leaves it be; letting go
    /// lowers the sword, and the guard raised again starts from its own angle. At every angle, with every kind of sword, the arms stay out of
    /// the body and the head and the blade out of both.
    /// </summary>
    public sealed class SwordDeflectGuardPlayTests
    {
        private const float Dt = 1.0f / 60.0f;

        /// <summary>Where the shots come from, in the defender's axes: 12 m ahead at a gun's height.</summary>
        private static readonly Vector3 Shooter = new Vector3(0.0f, 2.2f, 12.0f);

        /// <summary>How far an arm may sink into the torso or the head, metres: the soft skin of the elbow brushing the side.</summary>
        private const float Touch = 0.03f;

        /// <summary>How far the blade stays off the skin, metres, from a fifth of the way along it (the handle is in the hand): as every swing keeps it.</summary>
        private const float BladeRoom = 0.06f;

        private const float BladeFromShare = 0.2f;

        /// <summary>
        /// How much of the blade must lie across the front of the head or the body at every angle, metres, the front as a shooter sees it:
        /// as wide as the head, from the hips to the top of the head, and in front of the chest (owner, 2026-11-19: "make it so all the
        /// blocking positions have either the sword blocking the head or body"). A knife's short blade, its larger part.
        /// </summary>
        private const float CoveringMetres = 0.4f;

        private const float BodyBottom = 0.95f;
        private const float BodyFront = 0.3f;

        /// <summary>How much of <paramref name="blade"/> lies across the front of the body (its own axes), metres.</summary>
        private float Covering(ISwordBlade blade)
        {
            const int Steps = 100;
            Vector3 from = _player.transform.InverseTransformPoint(blade.BladeBaseWorld), to = _player.transform.InverseTransformPoint(blade.BladeTipWorld);
            float step = Vector3.Distance(from, to) / Steps, covering = 0.0f;
            for (int i = 0; i < Steps; i++)
            {
                Vector3 point = Vector3.Lerp(from, to, (i + 0.5f) / Steps);
                if (Mathf.Abs(point.x) <= FusedBodyShape.HeadRadius && point.y >= BodyBottom && point.y <= _locomotion.StandingHeight && point.z >= BodyFront)
                {
                    covering += step;
                }
            }
            return covering;
        }

        private GameObject _player;
        private GameObject _ground;
        private GameObject _sword;
        private PlayerVitals _vitals;
        private PlayerLocomotion _locomotion;
        private SoulsSwordCombat _combat;
        private SwordPoseDriver _poser;
        private ISwordBlade _blade;
        private LocomotionInput _input;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_sword != null) Object.Destroy(_sword);
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        private void Hold(string weapon)
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            if (_ground == null)
            {
                _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            }
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            _vitals = _player.GetComponent<PlayerVitals>();
            _vitals.InfiniteStamina = true;
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            var sword = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/CityX/Weapons/{weapon}.prefab"),
                new Vector3(30.0f, 1.0f, 30.0f), Quaternion.identity).GetComponent<SwordWeapon>();
            sword.OnEquip(_player, CityX.Items.ItemInstanceData.None);
            _sword = sword.gameObject;
            _combat = _player.GetComponent<SoulsSwordCombat>();   // (added by the sword as it is equipped)
            _poser = _player.GetComponent<SwordPoseDriver>();
            _blade = sword;
            _input = new LocomotionInput { SwordEquipped = !_blade.IsOneHanded, Sword1HEquipped = _blade.IsOneHanded };
#endif
        }

        private IEnumerator Frames(int count, bool guard)
        {
            for (int i = 0; i < count; i++)
            {
                _combat.SetBlockHeld(guard);
                _combat.Tick(Dt);
                _vitals.TickHitstun(Dt);   // the parry's instant runs out, as the player's input ticks it in the game
                _locomotion.Tick(_input, Dt);
                yield return null;
            }
        }

        /// <summary>Which way the blade points, in the body's axes.</summary>
        private Vector3 BladeWay() => _player.transform.InverseTransformDirection(_blade.BladeTipWorld - _blade.BladeBaseWorld).normalized;

        /// <summary>The middle of the blade, in the body's axes.</summary>
        private Vector3 BladeMiddle() => _player.transform.InverseTransformPoint((_blade.BladeBaseWorld + _blade.BladeTipWorld) * 0.5f);

        private void Shoot() => DamageContext.Strike(_vitals, 10.0f, _player.transform.TransformPoint(Shooter), 0.0f, DamageKind.Bullet, 0.0f);

        [UnityTest]
        public IEnumerator EachBulletTheGuardTurnsAsideSnapsTheBladeToANewAngleThatHoldsUntilTheNext([Values("CombatSword", "SpikeSword")] string weapon)
        {
#if UNITY_EDITOR
            Hold(weapon);
            yield return Frames(30, false);
            yield return Frames(40, true);   // up, and past the parry's instant
            Assert.IsTrue(_poser.IsGuarding, "the guard did not come up");
            Assert.AreEqual(0, _poser.DeflectPose, "the guard did not come up at its own angle");
            Vector3 ownWay = BladeWay();
            var effect = _player.GetComponent<BlockDeflectEffect>();
            _vitals.Roll = () => 0.0f;   // every bullet is turned aside
            int snapFrames = Mathf.CeilToInt(SwordStrikes.DeflectSnapSeconds / Dt) + 1;
            for (int shot = 0; shot < 6; shot++)
            {
                int was = _poser.DeflectPose;
                Vector3 wayBefore = BladeWay(), middleBefore = BladeMiddle();
                int shown = effect.Shown;
                float health = _vitals.Health;
                Shoot();
                Assert.AreEqual(health, _vitals.Health, 0.01f, $"shot {shot}: the turned-aside bullet hurt");
                Assert.AreEqual(shown + 1, effect.Shown, $"shot {shot}: the bullet did not glance off with sparks");
                Assert.AreNotEqual(was, _poser.DeflectPose, $"shot {shot}: the guard was not knocked to a new angle");
                yield return Frames(snapFrames, true);
                Vector3 arrivedWay = BladeWay(), arrivedMiddle = BladeMiddle();
                float turned = Vector3.Angle(wayBefore, arrivedWay);
                float moved = Vector3.Distance(middleBefore, arrivedMiddle);
                Assert.IsTrue(turned >= 20.0f || moved >= 0.2f,
                    $"shot {shot} (angle {was} to {_poser.DeflectPose}): the blade turned only {turned:0} degrees and moved {moved * 100.0f:0} cm");
                yield return Frames(60, true);
                Assert.Less(Vector3.Angle(arrivedWay, BladeWay()), 3.0f, $"shot {shot}: the blade did not hold its new angle (or got there late)");
                Assert.Less(Vector3.Distance(arrivedMiddle, BladeMiddle()), 0.04f, $"shot {shot}: the blade drifted from its new angle");
            }

            // A bullet the roll does not turn aside gets through the guard (a quarter of it): the blade is not knocked.
            _vitals.Roll = () => 1.0f;
            int held = _poser.DeflectPose;
            Vector3 heldWay = BladeWay();
            float before = _vitals.Health;
            Shoot();
            Assert.Less(_vitals.Health, before, "the bullet did not get through");
            yield return Frames(snapFrames + 10, true);
            Assert.AreEqual(held, _poser.DeflectPose, "a bullet that got through moved the guard");
            Assert.Less(Vector3.Angle(heldWay, BladeWay()), 2.0f, "a bullet that got through moved the blade");

            // Let go, and up again: the guard's own angle.
            yield return Frames(60, false);
            Assert.IsFalse(_poser.IsGuarding, "the guard did not come down");
            Assert.AreEqual(0, _poser.DeflectPose);
            yield return Frames(40, true);
            Assert.AreEqual(0, _poser.DeflectPose, "the guard raised again kept the last bullet's angle");
            Assert.Less(Vector3.Angle(ownWay, BladeWay()), 4.0f, "the guard raised again is not at its own angle");

            // A bullet caught in the guard's first instant (the parry) is turned aside for certain: the knock is its flourish, and holds.
            yield return Frames(40, false);
            yield return Frames(4, true);
            _vitals.Roll = () => 1.0f;   // the roll would fail: the parry does not roll
            float unhurt = _vitals.Health;
            Shoot();
            Assert.AreEqual(unhurt, _vitals.Health, 0.01f, "the bullet in the guard's first instant was not turned aside");
            Assert.AreNotEqual(0, _poser.DeflectPose, "the parried bullet did not knock the guard");
            yield return Frames(snapFrames, true);
            Vector3 parriedWay = BladeWay(), parriedMiddle = BladeMiddle();
            yield return Frames(60, true);
            Assert.Less(Vector3.Angle(parriedWay, BladeWay()), 3.0f, "after a parried bullet the blade did not hold its new angle");
            Assert.Less(Vector3.Distance(parriedMiddle, BladeMiddle()), 0.04f, "after a parried bullet the blade drifted from its new angle");
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator AtEveryAngleTheArmsAndTheBladeStayOutOfTheBody()
        {
#if UNITY_EDITOR
            var failures = new System.Collections.Generic.List<string>();
            var report = new System.Text.StringBuilder();
            foreach (string weapon in new[] { "CombatSword", "MetalCrowbar", "KitchenKnife", "SpikeSword", "BaseballBat", "Sledgehammer" })
            {
                Hold(weapon);
                bool oneHanded = _blade.IsOneHanded;
                Animator animator = _player.GetComponentInChildren<Animator>();
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips), neck = animator.GetBoneTransform(HumanBodyBones.Neck);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                yield return Frames(30, false);
                yield return Frames(30, true);
                for (int angle = 0; angle < SwordStrikes.DeflectPoseCount; angle++)
                {
                    _poser.Deflect(angle);
                    yield return Frames(30, true);
                    string at = $"{weapon} at angle {angle}";
                    float worstTorso = float.MaxValue, worstHead = float.MaxValue;
                    string torsoArm = "";
                    foreach ((HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones hand) in new[]
                    {
                        (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand),
                        (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand),
                    })
                    {
                        if (oneHanded && upper == HumanBodyBones.LeftUpperArm)
                        {
                            continue;   // the free arm hangs with the clips
                        }
                        Vector3 shoulder = animator.GetBoneTransform(upper).position, elbow = animator.GetBoneTransform(lower).position;
                        Vector3 wrist = animator.GetBoneTransform(hand).position;
                        float torso = ElbowClearance.Clearance(shoulder, elbow, wrist, hips.position, neck.position,
                            FusedBodyShape.TorsoRadius + FusedBodyShape.ArmRadius * 0.8f);
                        if (torso < worstTorso)
                        {
                            worstTorso = torso;
                            torsoArm = upper == HumanBodyBones.RightUpperArm ? "right" : "left";
                        }
                        foreach (Vector3 point in new[] { elbow, (elbow + wrist) * 0.5f, wrist })
                        {
                            worstHead = Mathf.Min(worstHead, Vector3.Distance(point, head.position) - FusedBodyShape.HeadRadius - FusedBodyShape.WristRadius);
                        }
                    }
                    float blade = SwordBladeClearance.Measure(animator, _blade, oneHanded, BladeFromShare, out string where);
                    float tipUp = _blade.BladeTipWorld.y - _player.transform.position.y;
                    float covering = Covering(_blade), wanted = Mathf.Min(CoveringMetres, 0.6f * Vector3.Distance(_blade.BladeBaseWorld, _blade.BladeTipWorld));
                    report.AppendLine($"{at}: arms clear the torso by {worstTorso * 100.0f:0} cm (the {torsoArm}), the head by {worstHead * 100.0f:0} cm; the blade the body by {blade * 100.0f:0} cm ({where}); {covering * 100.0f:0} cm of it across the front; the tip {tipUp * 100.0f:0} cm up");
                    if (worstTorso < -Touch) failures.Add($"{at}: the {torsoArm} arm is {-worstTorso * 100.0f:0} cm inside the torso");
                    if (worstHead < -Touch) failures.Add($"{at}: an arm is {-worstHead * 100.0f:0} cm inside the head");
                    if (blade < BladeRoom) failures.Add($"{at}: the blade passes {blade * 100.0f:0} cm from the skin ({where})");
                    if (tipUp < 0.2f) failures.Add($"{at}: the tip drags along the ground ({tipUp * 100.0f:0} cm up)");
                    if (covering < wanted) failures.Add($"{at}: only {covering * 100.0f:0} cm of the blade is across the front of the head or the body");
                }
                Object.Destroy(_sword);
                Object.Destroy(_player);
                _player = null;
                yield return null;
            }
            Debug.Log($"CityX sword guard at every deflected angle:\n{report}");
            Assert.IsEmpty(failures, string.Join("; ", failures));
#else
            yield break;
#endif
        }
    }
}
