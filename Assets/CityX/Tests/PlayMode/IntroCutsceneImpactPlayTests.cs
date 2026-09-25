using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// "EXTRA JUMP CUT IN THE FALLING CUTSCENE BEFORE HE HITS THE GROUND" --
    /// StartImpact() used to call Animator.Play("FallingFlatImpact", 0, 0.0f),
    /// an instant zero-blend cut between two clips with no reason to agree:
    /// mid-air dive pose (arms out, legs trailing) versus a flat-impact pose,
    /// switched in a single frame. (Hips/spine themselves are NOT the
    /// culprit -- confirmed directly: PlayerLocomotion.LateUpdate freezes
    /// both to identity every frame regardless of which cutscene state is
    /// playing, so they can't be what pops. The rest of the skeleton -- arms,
    /// legs, chest -- has no such freeze and is exactly what a zero-blend
    /// Animator.Play() cut between dissimilar poses snaps instantly.) This is
    /// the same class of bug character.gd's own BLEND_SECONDS constant
    /// already documents fixing elsewhere in the Godot original ("an instant
    /// cut... reads as a visible pop/snap... even a short cross-fade over the
    /// gap reads as fluid instead"), which this one transition never got.
    ///
    /// NEEDS THE REAL Player.prefab -- see PlayerLocomotionHipsPlayTests's
    /// own comment on why; there is no real "falling" dive pose to pop out of
    /// without the genuine rig and clips.
    /// </summary>
    public sealed class IntroCutsceneImpactPlayTests
    {
        private static readonly HumanBodyBones[] UnfrozenBones =
        {
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.RightUpperLeg,
        };

        private GameObject _ground;
        private GameObject _player;
        private GameObject _cameraObject;
        private PlayerLocomotion _locomotion;
        private Animator _animator;
        private Transform[] _bones;
        private ThirdPersonCamera _thirdPersonCamera;
        private IntroCutscene _cutscene;

        [SetUp]
        public void SetUp()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "Ground";
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
            if (_cameraObject != null) Object.Destroy(_cameraObject);
            if (_ground != null) Object.Destroy(_ground);
        }

        [UnityTest]
        public IEnumerator ThePoseDoesNotPopInASingleFrameWhenImpactStarts()
        {
#if UNITY_EDITOR
            GameObject playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/CityX/Character/Player.prefab");
            Assert.IsNotNull(playerPrefab, "Player.prefab is missing");

            _player = Object.Instantiate(playerPrefab, new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity);
            _locomotion = _player.GetComponent<PlayerLocomotion>();
            _animator = _player.GetComponentInChildren<Animator>();
            Assert.IsNotNull(_animator, "Player.prefab has no Animator");
            _bones = new Transform[UnfrozenBones.Length];
            for (int i = 0; i < UnfrozenBones.Length; i++)
            {
                _bones[i] = _animator.GetBoneTransform(UnfrozenBones[i]);
                Assert.IsNotNull(_bones[i], $"Player.prefab's Animator has no {UnfrozenBones[i]} bone mapped");
            }

            // Camera added BEFORE ThirdPersonCamera -- see
            // ThirdPersonCameraPlayTests's own comment on the component-add
            // ordering gotcha (Start() caches GetComponent<Camera>()).
            _cameraObject = new GameObject("Camera", typeof(Camera));
            _thirdPersonCamera = _cameraObject.AddComponent<ThirdPersonCamera>();
            _thirdPersonCamera.Target = _player.transform;
            _thirdPersonCamera.Distance = 4.5f;

            _cutscene = _player.AddComponent<IntroCutscene>();
            _cutscene.Begin(_player.GetComponent<CharacterController>(), _thirdPersonCamera,
                _animator, _cameraObject.transform, _locomotion);

            // Real frames, not logical ones -- the Animator's own clip
            // playback advances on real Time.deltaTime regardless of what
            // deltaTime this test hands to Tick() (see PlayerAimingPlayTests'
            // own comment on the identical gotcha). Tiny logical steps keep
            // the cutscene's own phase timer from reaching FallDurationSeconds
            // early, so it stays in Falling for the whole loop while the real
            // "falling" clip loops around enough times to settle into its own
            // steady dive-lean pose.
            for (int step = 0; step < 3000; step++)
            {
                _cutscene.Tick(0.001f);
                yield return null;
            }

            var beforeImpact = new Quaternion[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
            {
                beforeImpact[i] = _bones[i].localRotation;
            }

            // A baseline of how much these bones normally move from ONE real
            // frame to the next while still falling -- the pop assertion
            // below needs something to compare against besides zero, since
            // a looping mid-air dive pose is not perfectly static either.
            yield return null;
            float steadyStateDelta = 0.0f;
            for (int i = 0; i < _bones.Length; i++)
            {
                steadyStateDelta = Mathf.Max(steadyStateDelta,
                    Quaternion.Angle(beforeImpact[i], _bones[i].localRotation));
                beforeImpact[i] = _bones[i].localRotation;
            }

            // One single Tick() call that overshoots FallDurationSeconds --
            // TickFalling adds it to _phaseElapsed and calls StartImpact()
            // synchronously within the same call once t >= 1, exactly like a
            // real frame landing on the fall's last moment.
            _cutscene.Tick(IntroCutscene.FallDurationSeconds + 1.0f);

            // The very next real frame -- what the player actually sees cut
            // from one pose to the next.
            yield return null;

            float impactFrameDelta = 0.0f;
            HumanBodyBones worstBone = UnfrozenBones[0];
            for (int i = 0; i < _bones.Length; i++)
            {
                float delta = Quaternion.Angle(beforeImpact[i], _bones[i].localRotation);
                if (delta > impactFrameDelta)
                {
                    impactFrameDelta = delta;
                    worstBone = UnfrozenBones[i];
                }
            }

            Assert.Less(impactFrameDelta, Mathf.Max(steadyStateDelta * 3.0f, 10.0f),
                $"{worstBone} rotated {impactFrameDelta:0.0} degrees in the single frame impact " +
                $"started (normal frame-to-frame movement while falling was only {steadyStateDelta:0.0} " +
                "degrees) -- that's the reported jump cut, an instant pop rather than a smooth blend " +
                "into the impact pose");
#else
            Assert.Ignore("Player.prefab can only be loaded by path inside the editor.");
            yield break;
#endif
        }
    }
}
