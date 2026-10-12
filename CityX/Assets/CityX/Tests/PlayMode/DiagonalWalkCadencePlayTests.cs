using System.Collections;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Walking diagonally (W+A, W+D) must step at the same pace as walking straight (W, A or D). Owner, 2026-10-11: "when you hold A and W at the
    /// same time the animation appears in like slow motion compared to just holding A or W or D ... the movement is the same speed". The
    /// locomotion blend's cycle length is what sets the pace of the steps: measured here at a straight walk and at a diagonal walk of the same
    /// speed, with how much of the idle clip leaks into the diagonal.
    /// </summary>
    public sealed class DiagonalWalkCadencePlayTests
    {
        private GameObject _player;

        [TearDown]
        public void TearDown()
        {
            if (_player != null) Object.Destroy(_player);
        }

        private struct Sample
        {
            public float CycleSeconds;
            public float IdleWeight;
        }

        private static IEnumerator Measure(Animator animator, Vector2 moveXZ, System.Action<Sample> done)
        {
            animator.SetFloat("MoveX", moveXZ.x);
            animator.SetFloat("MoveZ", moveXZ.y);
            animator.SetFloat("Speed", moveXZ.magnitude);
            animator.SetBool("Grounded", true);
            for (int i = 0; i < 20; i++)
            {
                animator.Update(1.0f / 60.0f);
                yield return null;
            }
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            float idle = 0.0f;
            foreach (AnimatorClipInfo clip in animator.GetCurrentAnimatorClipInfo(0))
            {
                if (clip.clip != null && clip.clip.name.ToLowerInvariant().Contains("idle"))
                {
                    idle += clip.weight;
                }
            }
            TestContext.WriteLine($"move {moveXZ}: cycle {state.length:0.000} s, idle weight {idle:0.00}");
            done(new Sample { CycleSeconds = state.length, IdleWeight = idle });
        }

        [UnityTest]
        public IEnumerator ADiagonalWalkStepsAtTheSamePaceAsAStraightOne()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0, 500, 0), Quaternion.identity);
            foreach (MonoBehaviour behaviour in _player.GetComponents<MonoBehaviour>())
            {
                behaviour.enabled = false;   // only the Animator, driven by hand
            }
            Animator animator = _player.GetComponentInChildren<Animator>();
            animator.enabled = false;
            yield return null;

            float walk = _player.GetComponent<PlayerLocomotion>().WalkSpeed;
            Sample forward = default, left = default, right = default, forwardLeft = default, forwardRight = default;
            yield return Measure(animator, new Vector2(0.0f, walk), s => forward = s);
            yield return Measure(animator, new Vector2(-walk, 0.0f), s => left = s);
            yield return Measure(animator, new Vector2(walk, 0.0f), s => right = s);
            float d = walk * Mathf.Sqrt(0.5f);
            yield return Measure(animator, new Vector2(-d, d), s => forwardLeft = s);
            yield return Measure(animator, new Vector2(d, d), s => forwardRight = s);

            float slowestStraight = Mathf.Max(forward.CycleSeconds, Mathf.Max(left.CycleSeconds, right.CycleSeconds));
            float fastestStraight = Mathf.Min(forward.CycleSeconds, Mathf.Min(left.CycleSeconds, right.CycleSeconds));
            Assert.Less(forwardLeft.IdleWeight, 0.05f, "W+A blends in the idle: its steps slow down");
            Assert.Less(forwardRight.IdleWeight, 0.05f, "W+D blends in the idle: its steps slow down");
            Assert.LessOrEqual(forwardLeft.CycleSeconds, slowestStraight * 1.03f, "W+A steps slower than any straight walk");
            Assert.LessOrEqual(forwardRight.CycleSeconds, slowestStraight * 1.03f, "W+D steps slower than any straight walk");
            Assert.GreaterOrEqual(forwardLeft.CycleSeconds, fastestStraight * 0.97f, "W+A steps faster than any straight walk");
            Assert.GreaterOrEqual(forwardRight.CycleSeconds, fastestStraight * 0.97f, "W+D steps faster than any straight walk");
#else
            yield break;
#endif
        }

        /// <summary>Saves Logs/CityShots/diagonal-walk.png: a W+D walk cycle in eight frames, seen from the front-left. Run with -Graphics and look.</summary>
        [UnityTest, Explicit]
        public IEnumerator DiagonalWalkCapture()
        {
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            _player = Object.Instantiate(prefab, new Vector3(0, 500, 0), Quaternion.identity);
            foreach (MonoBehaviour behaviour in _player.GetComponents<MonoBehaviour>())
            {
                behaviour.enabled = false;
            }
            Animator animator = _player.GetComponentInChildren<Animator>();
            animator.enabled = false;
            yield return null;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                yield break;
            }
            float d = _player.GetComponent<PlayerLocomotion>().WalkSpeed * Mathf.Sqrt(0.5f);
            animator.SetFloat("MoveX", d);
            animator.SetFloat("MoveZ", d);
            animator.SetFloat("Speed", d * Mathf.Sqrt(2.0f));
            animator.SetBool("Grounded", true);
            for (int i = 0; i < 30; i++) animator.Update(1.0f / 60.0f);

            var holder = new GameObject("Cam");
            Camera camera = holder.AddComponent<Camera>();
            camera.fieldOfView = 40.0f;
            camera.backgroundColor = new Color(0.45f, 0.5f, 0.58f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            Vector3 centre = _player.transform.position + Vector3.up * 1.3f;
            holder.transform.position = centre + new Vector3(-4.5f, 0.6f, 5.5f);
            holder.transform.LookAt(centre);
            const int cell = 240, frames = 8;
            var sheet = new Texture2D(cell * frames, cell, TextureFormat.RGB24, false);
            var rt = RenderTexture.GetTemporary(cell, cell, 24);
            camera.targetTexture = rt;
            for (int f = 0; f < frames; f++)
            {
                animator.Update(1.033f / frames);
                camera.Render();
                RenderTexture.active = rt;
                var image = new Texture2D(cell, cell, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, cell, cell), 0, 0);
                sheet.SetPixels(f * cell, 0, cell, cell, image.GetPixels());
                Object.Destroy(image);
            }
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            sheet.Apply();
            string directory = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            System.IO.Directory.CreateDirectory(directory);
            var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "diagonal-walk.png"), (byte[])encode.Invoke(null, new object[] { sheet }));
            Object.Destroy(sheet);
            Object.Destroy(holder);
#else
            yield break;
#endif
        }
    }
}
