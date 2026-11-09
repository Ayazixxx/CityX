using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: hacked-apart bodies at night, through the game's grade (its bloom and tonemap), under a flashlight's beam, as the owner saw
    /// them (2026-11-08: "there are still little dots of shining light coming from some of the dismembered parts ive asked for it to be fixed
    /// several times"; the earlier checks were by day, with no grade, where no dot can show). Writes Logs/CityShots/gore-night-*.png, counts
    /// the near-white pixels in each picture, and for every material on the scene's bodies and pieces, how many of those go when it alone is
    /// switched off: the dots' makers. <see cref="SweepTheCameraForFlickers"/> looks for what flickers as the mouse moves (owner, 2026-11-08):
    /// the camera turned in small steps, pixels that are not numbers counted before the grade (the bloom turns each into a glowing dot), and
    /// each one traced to the renderer that makes it. Explicit; needs -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class GoreNightCapture
    {
        private const float Step = 1.0f / 60.0f;
        private const int Width = 1280;
        private const int Height = 800;

        /// <summary>Near-white: brighter than this.</summary>
        private const float BrightLuminance = 0.8f;

        /// <summary>A patch of near-white this small or smaller is a dot, metres being pixels here: an eyeball across the scene is about 150.</summary>
        private const int MostDotPixels = 900;

        private GameObject _ground;
        private Camera _camera;
        private UnityEngine.Rendering.Volume _grade;
        private readonly List<GameObject> _bodies = new List<GameObject>();
        private readonly List<Light> _lamps = new List<Light>();
        private Light _flashlight;
        private Color _ambientBefore;
        private Vector3 _middle;
        private Vector3 _eye;

        /// <summary>Hacked-apart bodies on dark ground, landed, under the flashlight and a room's lamps, through the game's grade.</summary>
        private IEnumerator Build()
        {
            LogAssert.ignoreFailingMessages = true;
#if UNITY_EDITOR
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground = ground;
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(200.0f, 1.0f, 200.0f);
            ground.GetComponent<Renderer>().material.color = new Color(0.22f, 0.12f, 0.11f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            _ambientBefore = RenderSettings.ambientLight;
            RenderSettings.ambientLight = new Color(0.07f, 0.03f, 0.03f);
            var camera = _camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 40.0f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.01f, 0.01f, 0.015f);
            camera.allowHDR = true;
            camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = true;
            var grade = _grade = new GameObject("Grade").AddComponent<UnityEngine.Rendering.Volume>();
            grade.isGlobal = true;
            grade.sharedProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/CityX/View/Materials/CityGrade.asset");
            Physics.SyncTransforms();

            (string prefab, HumanBodyBones from, HumanBodyBones to, float share, bool edged)[] kills =
            {
                ("Assets/CityX/Character/Player.prefab", HumanBodyBones.Neck, HumanBodyBones.Head, 0.5f, true),
                ("Assets/CityX/Character/Player.prefab", HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f, false),
                ("Assets/CityX/Character/Swordsman.prefab", HumanBodyBones.Neck, HumanBodyBones.Head, 1.4f, false),
                ("Assets/CityX/Character/Swordsman.prefab", HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 0.5f, true),
                ("Assets/CityX/Character/Player.prefab", HumanBodyBones.Hips, HumanBodyBones.Neck, 0.6f, false),
            };
            for (int k = 0; k < kills.Length; k++)
            {
                (string path, HumanBodyBones from, HumanBodyBones to, float share, bool edged) = kills[k];
                GameObject body = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path),
                    new Vector3((k % 3) * 2.2f - 2.2f, 0.05f, (k / 3) * 2.4f), Quaternion.Euler(0.0f, 180.0f, 0.0f));
                _bodies.Add(body);
                if (body.TryGetComponent(out PlayerInputReader reader))
                {
                    reader.enabled = false;
                }
                foreach (MonoBehaviour mind in body.GetComponents<MonoBehaviour>())
                {
                    if (mind is IHostile)
                    {
                        mind.enabled = false;   // a swordsman stands still to be cut
                    }
                }
                var vitals = body.GetComponent<PlayerVitals>();
                vitals.Revive();
                yield return null;
                Animator animator = body.GetComponentInChildren<Animator>();
                Vector3 at = Vector3.Lerp(animator.GetBoneTransform(from).position, animator.GetBoneTransform(to).position, share);
                vitals.TakeDamage(vitals.Health - 10.0f);
                using (DamageContext.Cutting(edged))
                {
                    DamageContext.Strike(vitals, 90.0f, at + new Vector3(1.0f, 0.0f, -1.5f), 0.0f, DamageKind.Physical, 1.0f, at);
                }
            }
            for (float t = 0.0f; t < 4.0f; t += Step)   // everything landed, the stumps done bleeding
            {
                yield return null;
            }

            Vector3 middle = _middle = new Vector3(0.0f, 0.3f, 1.2f);
            Vector3 eye = _eye = middle + new Vector3(0.6f, 4.2f, -3.6f);
            var flashlight = _flashlight = new GameObject("Flashlight").AddComponent<Light>();
            flashlight.type = LightType.Spot;
            flashlight.intensity = CityX.Weapons.FlashlightRules.Intensity;
            flashlight.range = CityX.Weapons.FlashlightRules.Range;
            flashlight.spotAngle = CityX.Weapons.FlashlightRules.SpillDegrees;
            flashlight.transform.SetPositionAndRotation(eye + Vector3.down * 0.8f, Quaternion.LookRotation(middle - eye));
            // A room's lamps, as where the owner saw the dots: small, warm-red, overhead.
            foreach (Vector3 at in new[] { new Vector3(-3.0f, 3.2f, 0.0f), new Vector3(3.0f, 3.2f, 3.0f), new Vector3(0.0f, 3.2f, 5.0f) })
            {
                Light lamp = new GameObject("Lamp").AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.color = new Color(1.0f, 0.55f, 0.5f);
                lamp.intensity = 6.0f;
                lamp.range = 9.0f;
                lamp.transform.position = at;
                _lamps.Add(lamp);
            }
#else
            yield break;
#endif
        }

        private void Clear()
        {
            foreach (Debris debris in Object.FindObjectsByType<Debris>(FindObjectsSortMode.None))
            {
                Object.Destroy(debris.gameObject);
            }
            foreach (GameObject body in _bodies)
            {
                Object.Destroy(body);
            }
            _bodies.Clear();
            RenderSettings.ambientLight = _ambientBefore;
            foreach (Light lamp in _lamps)
            {
                Object.Destroy(lamp.gameObject);
            }
            _lamps.Clear();
            Object.Destroy(_flashlight.gameObject);
            Object.Destroy(_grade.gameObject);
            Object.Destroy(_camera.gameObject);
            Object.Destroy(_ground);
        }

        [UnityTest]
        public IEnumerator RenderGoreAtNight()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            yield return Build();
            Camera camera = _camera;
            GameObject ground = _ground;
            Vector3 middle = _middle, eye = _eye;
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(middle - eye));
            int all = Bright(camera, "gore-night-wide");
            camera.transform.SetPositionAndRotation(middle + new Vector3(0.3f, 1.8f, -1.6f), Quaternion.LookRotation(Vector3.down * 1.5f + Vector3.forward * 1.6f));
            Bright(camera, "gore-night-close");
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(middle - eye));

            // Which material makes them: each switched off alone.
            var byMaterial = new Dictionary<string, List<Renderer>>();
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.gameObject == ground || !renderer.enabled || renderer is ParticleSystemRenderer)
                {
                    continue;
                }
                foreach (Material material in renderer.sharedMaterials)
                {
                    string key = material != null ? material.name.Replace(" (Instance)", "") : "(none)";
                    if (!byMaterial.TryGetValue(key, out List<Renderer> list))
                    {
                        byMaterial[key] = list = new List<Renderer>();
                    }
                    list.Add(renderer);
                }
            }
            Debug.Log($"CityX gore night: {all} dot pixels with everything shown");
            foreach (KeyValuePair<string, List<Renderer>> entry in byMaterial)
            {
                foreach (Renderer renderer in entry.Value)
                {
                    renderer.enabled = false;
                }
                int without = Bright(camera, null);
                foreach (Renderer renderer in entry.Value)
                {
                    renderer.enabled = true;
                }
                if (without < all)
                {
                    Material sample = entry.Value[0].sharedMaterials[0];
                    Debug.Log($"CityX gore night: without {entry.Key} ({entry.Value.Count} renderers, e.g. {entry.Value[0].name}): {without} " +
                              $"(-{all - without}); smoothness {Smoothness(entry.Value[0], entry.Key)}");
                }
            }

            Clear();
#else
            yield break;
#endif
        }

        // --- what flickers as the mouse moves ---

        private const int SweepPoses = 160;
        private const int SweepWidth = 1600;
        private const int SweepHeight = 900;

        /// <summary>How many poses have each broken pixel traced to its renderer (each costs a render per renderer).</summary>
        private const int TracedPoses = 6;

        private RenderTexture _sweepTarget;
        private Texture2D _sweepImage;

        [UnityTest]
        [Timeout(1800000)]
        public IEnumerator SweepTheCameraForFlickers()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            yield return Build();
            _camera.fieldOfView = 50.0f;
            _camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
            _sweepTarget = new RenderTexture(SweepWidth, SweepHeight, 24, RenderTextureFormat.ARGBFloat);
            _sweepImage = new Texture2D(SweepWidth, SweepHeight, TextureFormat.RGBAFloat, false);
            foreach (bool beam in new[] { true, false })
            {
                _flashlight.enabled = beam;
                yield return null;
                yield return Sweep(beam ? "flashlight on" : "flashlight off");
            }
            Object.Destroy(_sweepTarget);
            Object.Destroy(_sweepImage);
            Clear();
#else
            yield break;
#endif
        }

        private IEnumerator Sweep(string label)
        {
            var renderers = new List<Renderer>();
            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.gameObject != _ground && renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    renderers.Add(renderer);
                }
            }
            int broken = 0, posesWithBroken = 0, traced = 0;
            var makers = new Dictionary<Renderer, int>();
            for (int pose = 0; pose < SweepPoses; pose++)
            {
                Aim(pose);
                List<int> found = BrokenPixels();
                broken += found.Count;
                posesWithBroken += found.Count > 0 ? 1 : 0;
                if (found.Count > 0 && traced < TracedPoses)
                {
                    traced++;
                    yield return Trace(found, renderers, makers);
                }
                yield return null;
            }
            Debug.Log($"CityX gore flicker, {label}: {broken} pixels that are not numbers, in {posesWithBroken} of {SweepPoses} poses, {renderers.Count} renderers");
            foreach (KeyValuePair<Renderer, int> maker in makers)
            {
                Debug.Log($"CityX gore flicker, {label}: {maker.Value} traced to {Describe(maker.Key)}");
            }
        }

        /// <summary>The camera as the mouse turns it: orbiting the bodies in small steps from where the game's camera would sit.</summary>
        private void Aim(int pose)
        {
            float yaw = pose * 0.35f;
            float pitch = 38.0f + 5.0f * Mathf.Sin(pose * 0.11f);
            Quaternion turn = Quaternion.Euler(pitch, yaw, 0.0f);
            _camera.transform.SetPositionAndRotation(_middle - turn * Vector3.forward * 6.5f, turn);
        }

        /// <summary>Where the picture from the camera, before the grade, has a pixel that is not a number.</summary>
        private List<int> BrokenPixels()
        {
            _camera.targetTexture = _sweepTarget;
            _camera.Render();
            RenderTexture.active = _sweepTarget;
            _sweepImage.ReadPixels(new Rect(0, 0, SweepWidth, SweepHeight), 0, 0);
            Unity.Collections.NativeArray<Color> pixels = _sweepImage.GetRawTextureData<Color>();
            _camera.targetTexture = null;
            RenderTexture.active = null;
            var found = new List<int>();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (IsBroken(pixels[i]))
                {
                    found.Add(i);
                }
            }
            return found;
        }

        private static bool IsBroken(Color c) =>
            float.IsNaN(c.r) || float.IsNaN(c.g) || float.IsNaN(c.b) || float.IsInfinity(c.r) || float.IsInfinity(c.g) || float.IsInfinity(c.b);

        /// <summary>Which renderer each broken pixel goes with when it alone is hidden.</summary>
        private IEnumerator Trace(List<int> broken, List<Renderer> renderers, Dictionary<Renderer, int> makers)
        {
            var left = new List<int>(broken);
            foreach (Renderer renderer in renderers)
            {
                if (left.Count == 0)
                {
                    break;
                }
                bool wasOff = renderer.forceRenderingOff;
                renderer.forceRenderingOff = true;
                List<int> without = BrokenPixels();
                renderer.forceRenderingOff = wasOff;
                yield return null;
                for (int i = left.Count - 1; i >= 0; i--)
                {
                    if (!without.Contains(left[i]))
                    {
                        makers.TryGetValue(renderer, out int count);
                        makers[renderer] = count + 1;
                        left.RemoveAt(i);
                    }
                }
            }
            if (left.Count > 0)
            {
                Debug.Log($"CityX gore flicker: {left.Count} broken pixels no one renderer makes");
            }
        }

        private static string Describe(Renderer renderer)
        {
            var text = new StringBuilder($"{renderer.GetType().Name} {renderer.name}, parent {(renderer.transform.parent != null ? renderer.transform.parent.name : "-")}");
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null)
                {
                    text.Append($" | {material.name}: {material.shader.name}, smoothness {(material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness").ToString("0.00") : "-")}, " +
                                $"highlights off {material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")}, metallic {(material.HasProperty("_Metallic") ? material.GetFloat("_Metallic").ToString("0.00") : "-")}");
                }
            }
            Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (mesh != null && mesh.isReadable)
            {
                int none = 0, notUnit = 0;
                foreach (Vector3 normal in mesh.normals)
                {
                    none += float.IsNaN(normal.x) || normal.sqrMagnitude < 1e-6f ? 1 : 0;
                    notUnit += Mathf.Abs(normal.sqrMagnitude - 1.0f) > 0.01f ? 1 : 0;
                }
                text.Append($" | mesh {mesh.name}, {mesh.vertexCount} corners, {none} with no normal, {notUnit} not one long");
            }
            return text.ToString();
        }

        private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        private static string Smoothness(Renderer renderer, string name)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && material.name.Replace(" (Instance)", "") == name)
                {
                    string smooth = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness").ToString("0.00") : "-";
                    string emission = material.IsKeywordEnabled("_EMISSION") ? " emissive " + material.GetColor("_EmissionColor") : "";
                    string colour = material.HasProperty("_BaseColor") ? " colour " + material.GetColor("_BaseColor") : "";
                    return smooth + colour + emission + " shader " + material.shader.name;
                }
            }
            return "-";
        }

        /// <summary>Renders <paramref name="camera"/>, saves it as <paramref name="name"/> (if given), and counts its dot pixels.</summary>
        private static int Bright(Camera camera, string name)
        {
            RenderTexture rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            // A dot: a small patch of near-white (up to MostDotPixels), as a pinpoint or an eyeball is; a lit area (a body in the beam) is
            // bigger and is not one.
            Color[] pixels = image.GetPixels();
            var bright = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                bright[i] = Luminance(pixels[i]) > BrightLuminance;
            }
            int count = 0;
            var seen = new bool[pixels.Length];
            var stack = new Stack<int>();
            for (int start = 0; start < pixels.Length; start++)
            {
                if (!bright[start] || seen[start])
                {
                    continue;
                }
                int area = 0;
                seen[start] = true;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int at = stack.Pop();
                    area++;
                    int x = at % Width, y = at / Width;
                    foreach (int next in new[] { x > 0 ? at - 1 : -1, x < Width - 1 ? at + 1 : -1, y > 0 ? at - Width : -1, y < Height - 1 ? at + Width : -1 })
                    {
                        if (next >= 0 && bright[next] && !seen[next])
                        {
                            seen[next] = true;
                            stack.Push(next);
                        }
                    }
                }
                if (area <= MostDotPixels)
                {
                    count += area;
                }
            }
            if (name != null)
            {
                var conv = System.Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                var encode = conv.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
                File.WriteAllBytes(Path.Combine(BuildingShapeCapture.ShotsFolder, name + ".png"), (byte[])encode.Invoke(null, new object[] { image }));
            }
            camera.targetTexture = null;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(image);
            return count;
        }
    }
}
