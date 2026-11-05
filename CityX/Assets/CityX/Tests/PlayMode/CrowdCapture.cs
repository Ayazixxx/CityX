using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.View;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Not a test: THE CROWD TEST (owner, 2026-10-27: "do the crowd test and make them have random various outfits and carrying various weapons
    /// ... the players i have created already but a whole bunch of clones of them ... some doing spells, some doing attacks, some just running
    /// around. start small and scale up see how much it can handle. if it starts getting laggy make less doing spells and attacks. no acid
    /// rain. no grow or shrink spell"; docs/GAME_DESIGN.md "Crowds"). In the real city, round the spawn: clones of the characters made in the
    /// creator (this machine's, as the local server keeps them), each in random clothes with a random weapon (CrowdMember drives them).
    ///
    /// The crowd grows step by step (<see cref="Steps"/>). At each size it is timed first with everyone just running (what the bodies
    /// themselves cost), then with some fighting and casting; when that is laggy -- the crowd costing more than <see cref="LaggyExtraMs"/> over
    /// the empty street -- fewer fight and cast, and from then on stay fewer. Past <see cref="GiveUpExtraMs"/> with everyone just running it
    /// stops growing. Each timing is split into where the frame went (scripts, animation, skinning, physics, drawing) from Unity's own profiler
    /// markers. Then a picture at each size, and a video of <see cref="VideoCrowd"/> fighting and casting.
    ///
    /// The frame is timed with the picture waited for each frame (the graphics card's work counted in, not overlapped with the next frame's
    /// as the game overlaps it), in the editor (slower scripts than a built game): the absolute numbers are on the slow side; what each
    /// player ADDS is the useful part. Results to Logs/CityShots/crowd-results.txt, pictures crowd-*.png, the video crowd.mp4. Explicit; run
    /// by name with -Graphics: ./tools/unity_tests.ps1 -Platform PlayMode -Graphics -Filter CityX.Tests.CrowdCapture
    ///
    /// Quick, for a before and after: with CITYX_CROWD_STEPS set (say "25,50") only those sizes are timed, and no video is made.
    /// </summary>
    [Explicit("renders screenshots and a video; needs a graphics device")]
    public sealed class CrowdCapture
    {
        private const string ScenePath = "Assets/CityX/Scenes/CityMassing.unity";
        private const string BodyPath = "Assets/CityX/Character/NetworkedPlayer.prefab";
        private const int Width = 1280, Height = 720;

        /// <summary>The crowd sizes tried, smallest first.</summary>
        private static readonly int[] AllSteps = { 10, 25, 50, 75, 100, 150, 200, 300 };

        /// <summary>The sizes asked for in CITYX_CROWD_STEPS, else <see cref="AllSteps"/>.</summary>
        private static int[] Steps
        {
            get
            {
                string asked = System.Environment.GetEnvironmentVariable("CITYX_CROWD_STEPS");
                if (string.IsNullOrWhiteSpace(asked))
                {
                    return AllSteps;
                }
                var sizes = new List<int>();
                foreach (string part in asked.Split(','))
                {
                    if (int.TryParse(part.Trim(), out int size) && size > 0) sizes.Add(size);
                }
                return sizes.Count > 0 ? sizes.ToArray() : AllSteps;
            }
        }

        private static bool Quick => !string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable("CITYX_CROWD_STEPS"));

        /// <summary>More than this over the empty street (half a 60 fps frame) is laggy: fewer fight and cast.</summary>
        private const float LaggyExtraMs = 8.3f;

        /// <summary>More than this over the empty street with everyone just running, and the crowd stops growing.</summary>
        private const float GiveUpExtraMs = 60.0f;

        /// <summary>How many wear something on their head (two hats to share between them: the rest go bare-headed or with their hair).</summary>
        private const double HatShare = 0.35;

        /// <summary>How many are in the video.</summary>
        private const int VideoCrowd = 50;

        private const int WarmFrames = 90;
        private const int TimedFrames = 150;

        /// <summary>The share of runners at each calming: the start, fewer fighting and casting, then everyone just running.</summary>
        private static readonly float[] RunnerShares = { 0.4f, 0.7f, 1.0f };

        private static readonly string[] Weapons =
        {
            ItemRegistry.CombatSword, ItemRegistry.SpikeSword, ItemRegistry.BaseballBat, ItemRegistry.MetalCrowbar, ItemRegistry.Pistol,
            ItemRegistry.AK47, null,
        };

        /// <summary>Where a frame goes, by the player loop's own profiler markers (each shown as its label in the results).</summary>
        private static readonly (string label, string marker)[] Parts =
        {
            ("scripts", "Update.ScriptRunBehaviourUpdate"),
            ("late scripts", "PreLateUpdate.ScriptRunBehaviourLateUpdate"),
            ("animation", "PreLateUpdate.DirectorUpdateAnimationBegin"),
            ("animation 2", "PreLateUpdate.DirectorUpdateAnimationEnd"),
            ("skinning", "PostLateUpdate.UpdateAllSkinnedMeshes"),
            ("physics", "FixedUpdate.PhysicsFixedUpdate"),
        };

        private struct Cost
        {
            public float Frame;
            public float Slowest;
            public float Drawing;
            public float[] Parts;
        }

        private Scene _scene;
        private Camera _camera;
        private Camera _sceneCamera;
        private string _directory;
        private Vector3 _eye;
        private readonly List<GameObject> _crowd = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            foreach (GameObject member in _crowd)
            {
                if (member != null) Object.Destroy(member);
            }
            _crowd.Clear();
            if (_camera != null) Object.Destroy(_camera.gameObject);
            if (_sceneCamera != null) _sceneCamera.enabled = true;
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_scene.IsValid() && _scene.isLoaded)
            {
                LogAssert.ignoreFailingMessages = true;
                yield return SceneManager.UnloadSceneAsync(_scene);
            }
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator RunTheCrowd()
        {
#if UNITY_EDITOR
            CaptureGuard.NeedsGraphics();
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 60;
            _directory = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(_directory);
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            yield return null;
            _scene = SceneManager.GetSceneByPath(ScenePath);
            BuildingShapeCapture.UseTheScenesAir();
            var cycle = Object.FindFirstObjectByType<DayNightCycle>();
            if (cycle != null)
            {
                cycle.Running = false;
                cycle.TimeOfDay = SkyCycle.DaySeconds * 0.42f;
                cycle.Apply(SkyCycle.At(cycle.TimeOfDay));
            }
            var player = Object.FindFirstObjectByType<PlayerVitals>();
            player.GetComponent<PlayerInputReader>().enabled = false;
            var playerBody = player.GetComponent<PlayerLocomotion>();
            for (int i = 0; i < 120; i++)
            {
                playerBody.Tick(default, 1.0f / 60.0f);
                yield return null;
            }
            Vector3 home = player.transform.position;

            _sceneCamera = Camera.main;
            _sceneCamera.enabled = false;
            var cameraObject = new GameObject("CrowdCam");
            _camera = cameraObject.AddComponent<Camera>();
            _camera.fieldOfView = 50.0f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 1500.0f;
            _camera.allowHDR = true;
            var data = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;
            _eye = Viewpoint(home);
            LookAt(home + Vector3.up);

            List<CharacterSave> characters = OwnersCharacters();
            Debug.Log($"CityX crowd: {characters.Count} characters to clone ({string.Join(", ", characters.ConvertAll(c => c.Name))})");
            var body = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(BodyPath);
            Assert.IsNotNull(body, "no NetworkedPlayer.prefab -- rebuild the scene");

            var report = new StringBuilder();
            report.AppendLine("CityX crowd test (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ", " + SystemInfo.processorType.Trim() + ", "
                              + SystemInfo.graphicsDeviceName + ", in the editor)");
            report.Append("crowd | doing | frame ms | over empty | slowest 5% | drawing");
            foreach ((string label, string _) in Parts)
            {
                report.Append(" | " + label);
            }
            report.AppendLine();

            Cost empty = default;
            yield return Timed(c => empty = c);
            Line(report, "0", "-", empty, empty);

            int calm = 0;
            foreach (int size in Steps)
            {
                while (_crowd.Count < size)
                {
                    int index = _crowd.Count;
                    _crowd.Add(Spawn(body, characters[index % characters.Count], index, home));
                }

                // What the bodies themselves cost: everyone just running.
                SetRoles(1.0f);
                Cost running = default;
                yield return Warm();
                yield return Timed(c => running = c);
                Line(report, size.ToString(), "running", running, empty);

                // Then fighting and casting, fewer of them while that is laggy.
                while (calm < RunnerShares.Length - 1)
                {
                    SetRoles(RunnerShares[calm]);
                    Cost busy = default;
                    yield return Warm();
                    yield return Timed(c => busy = c);
                    Line(report, size.ToString(), $"{1.0f - RunnerShares[calm]:P0} fight/cast", busy, empty);
                    if (busy.Frame - empty.Frame <= LaggyExtraMs)
                    {
                        break;
                    }
                    calm++;
                }

                LookAt(Centre() + Vector3.up);
                Shoot($"crowd-{size}");
                if (running.Frame - empty.Frame > GiveUpExtraMs)
                {
                    report.AppendLine($"stopped growing at {size}: the crowd alone costs over {GiveUpExtraMs:0} ms a frame");
                    break;
                }
            }

            if (Quick)
            {
                File.WriteAllText(Path.Combine(_directory, "crowd-results-quick.txt"), report.ToString());
                Debug.Log("CityX crowd results:\n" + report);
                yield break;
            }

            // The video: VideoCrowd of them, the starting mix -- some running, some fighting, some casting.
            while (_crowd.Count > VideoCrowd)
            {
                Object.Destroy(_crowd[_crowd.Count - 1]);
                _crowd.RemoveAt(_crowd.Count - 1);
            }
            SetRoles(RunnerShares[0]);
            yield return Record(Path.Combine(_directory, "crowd.mp4"), 12.0f);
            // From the street at head height, on the crowd's edge: backed off until nobody and nothing is round the lens.
            Vector3 centre = Centre();
            Vector3 away = _eye - centre;
            away.y = 0.0f;
            away.Normalize();
            Vector3 low = centre + away * 8.0f + Vector3.up * 1.8f;
            for (int step = 0; step < 20 && Physics.CheckSphere(low, 1.2f, ~0, QueryTriggerInteraction.Ignore); step++)
            {
                low += away;
            }
            _camera.transform.SetPositionAndRotation(low, Quaternion.LookRotation(centre + Vector3.up * 1.4f - low, Vector3.up));
            Shoot("crowd-street-level");
            report.AppendLine($"video: {_crowd.Count} players, {1.0f - RunnerShares[0]:P0} fighting and casting, crowd.mp4");
            File.WriteAllText(Path.Combine(_directory, "crowd-results.txt"), report.ToString());
            Debug.Log("CityX crowd results:\n" + report);
#else
            yield break;
#endif
        }

        private static void Line(StringBuilder report, string size, string doing, Cost cost, Cost empty)
        {
            report.Append($"{size} | {doing} | {cost.Frame:0.0} ({1000.0f / cost.Frame:0} fps) | {cost.Frame - empty.Frame:+0.0;-0.0} | {cost.Slowest:0.0} | {cost.Drawing:0.0}");
            foreach (float part in cost.Parts)
            {
                report.Append(part >= 0.0f ? $" | {part:0.0}" : " | n/a");
            }
            report.AppendLine();
            Debug.Log($"CityX crowd: {size} {doing}: {cost.Frame:0.0} ms a frame ({cost.Frame - empty.Frame:+0.0;-0.0} over the empty street), drawing {cost.Drawing:0.0}");
        }

        private void SetRoles(float runnerShare)
        {
            for (int i = 0; i < _crowd.Count; i++)
            {
                _crowd[i].GetComponent<CrowdMember>().Role = RoleFor(i, runnerShare);
            }
        }

        /// <summary>Runners first by share, the rest split between fighters and casters, spread through the crowd.</summary>
        private static CrowdRole RoleFor(int index, float runnerShare)
        {
            float spread = (index * 0.6180339f) % 1.0f;
            if (spread < runnerShare)
            {
                return CrowdRole.Runner;
            }
            return spread < runnerShare + (1.0f - runnerShare) * 0.5f ? CrowdRole.Fighter : CrowdRole.Caster;
        }

        private Vector3 Centre()
        {
            Vector3 sum = Vector3.zero;
            foreach (GameObject member in _crowd)
            {
                sum += member.transform.position;
            }
            return _crowd.Count > 0 ? sum / _crowd.Count : _eye;
        }

        private static IEnumerator Warm()
        {
            for (int i = 0; i < WarmFrames; i++)
            {
                yield return null;
            }
        }

        /// <summary>
        /// A spot over the street to film the crowd from: up in the open air (nothing over it), with a clear line down to the crowd's middle;
        /// tried round it from the street's own directions first.
        /// </summary>
        private static Vector3 Viewpoint(Vector3 home)
        {
            Vector3 target = home + Vector3.up * 3.0f;   // over the heads: the player's own capsule stands at home
            Vector3 fallback = home + new Vector3(0.0f, 30.0f, -6.0f);
            for (int ring = 0; ring < 3; ring++)
            {
                float distance = 14.0f + ring * 6.0f;
                float height = 10.0f + ring * 4.0f;
                for (int turn = 0; turn < 360; turn += 15)
                {
                    Vector3 eye = home + Quaternion.Euler(0.0f, turn, 0.0f) * new Vector3(0.0f, height, -distance);
                    bool open = !Physics.Raycast(eye, Vector3.up, 60.0f, ~0, QueryTriggerInteraction.Ignore);
                    bool clear = !Physics.Linecast(eye, target, ~0, QueryTriggerInteraction.Ignore);
                    if (open && clear)
                    {
                        return eye;
                    }
                }
            }
            return fallback;
        }

        private void LookAt(Vector3 point)
        {
            _camera.transform.SetPositionAndRotation(_eye, Quaternion.LookRotation(point - _eye, Vector3.up));
        }

#if UNITY_EDITOR
        /// <summary>
        /// The characters made in the creator on this machine, as its local server keeps them (the newest account's list), else the ones this
        /// machine keeps itself, else the swordsman's Knight.
        /// </summary>
        private static List<CharacterSave> OwnersCharacters()
        {
            string accounts = Path.Combine(Application.persistentDataPath, "server", "accounts");
            string newest = null;
            if (Directory.Exists(accounts))
            {
                System.DateTime newestTime = System.DateTime.MinValue;
                foreach (string file in Directory.GetFiles(accounts, CharacterFile.FileName, SearchOption.AllDirectories))
                {
                    System.DateTime written = File.GetLastWriteTimeUtc(file);
                    if (written > newestTime)
                    {
                        newestTime = written;
                        newest = Path.GetDirectoryName(file);
                    }
                }
            }
            List<CharacterSave> characters = newest != null ? new CharacterFile(newest).Load() : new List<CharacterSave>();
            if (characters.Count == 0)
            {
                characters = new CharacterFile(Application.persistentDataPath).Load();
            }
            if (characters.Count == 0)
            {
                var knight = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/CityX/Character/NpcLooks/Knight.json");
                characters.Add(knight != null ? JsonUtility.FromJson<CharacterSave>(knight.text) : new CharacterSave { Name = "Nobody", Skin = Color.grey });
            }
            return characters;
        }

        /// <summary>
        /// One crowd member: a player's body (the joining player's, which has every spell and every item), its network parts taken off before
        /// it wakes (no session here), dressed as <paramref name="look"/>, in random clothes, holding a random weapon. On the street round
        /// <paramref name="home"/>: open sky over the spot, ground at the street's height.
        /// </summary>
        private static GameObject Spawn(GameObject prefab, CharacterSave look, int index, Vector3 home)
        {
            var random = new System.Random(9173 + index * 31);
            Vector3 at = home + Vector3.up * 0.1f;
            float angle = 0.0f;
            for (int tries = 0; tries < 12; tries++)
            {
                angle = (float)random.NextDouble() * Mathf.PI * 2.0f;
                float distance = 2.0f + 12.0f * Mathf.Sqrt((float)random.NextDouble());
                Vector3 spot = home + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * distance;
                if (Physics.Raycast(spot + Vector3.up * 300.0f, Vector3.down, out RaycastHit hit, 600.0f, ~0, QueryTriggerInteraction.Ignore)
                    && Mathf.Abs(hit.point.y - home.y) < 1.5f)
                {
                    at = hit.point + Vector3.up * 0.1f;
                    break;
                }
            }

            var asleep = new GameObject("CrowdAsleep");
            asleep.SetActive(false);
            GameObject body = Object.Instantiate(prefab, at, Quaternion.Euler(0.0f, angle * Mathf.Rad2Deg, 0.0f), asleep.transform);
            body.name = $"Crowd {index} ({look.Name})";
            // Every network part (the joining body gains more of them as features come; a list of them went stale and a newer part switched
            // the whole body off with no session to spawn it in), then the observer and the object itself.
            Strip<FishNet.Object.NetworkBehaviour>(body);
            Strip<FishNet.Observing.NetworkObserver>(body);
            Strip<FishNet.Object.NetworkObject>(body);
            Strip<PlayerInputReader>(body);   // only the real player reads the keyboard; CrowdMember drives this one
            Strip<PlayerVoiceCapture>(body);
            body.transform.SetParent(null, true);
            Object.Destroy(asleep);

            var identity = body.GetComponent<CharacterIdentity>();
            if (identity != null)
            {
                identity.Apply(look);
                identity.enabled = false;   // no playtime counted, nothing saved
            }
            var inventory = body.GetComponent<Inventory>();
            if (inventory != null)
            {
                Dress(inventory, body, random);
                string weapon = Weapons[random.Next(Weapons.Length)];
                if (weapon != null)
                {
                    inventory.EquipItemId(weapon, body);
                }
            }
            body.AddComponent<CrowdMember>().Begin(5501 + index * 7, CrowdRole.Runner, home);
            return body;
        }

        /// <summary>One random piece of clothing for each place on the body that has any (and now and then nothing there).</summary>
        private static void Dress(Inventory inventory, GameObject body, System.Random random)
        {
            var bySlot = new Dictionary<string, List<string>>();
            foreach (string id in ItemRegistry.Ids)
            {
                if (!ItemRegistry.TryGet(id, out ItemDefinition definition) || !definition.IsWearable || inventory.PrefabForItem(id) == null)
                {
                    continue;
                }
                if (!bySlot.TryGetValue(definition.EquipSlot, out List<string> ids))
                {
                    bySlot[definition.EquipSlot] = ids = new List<string>();
                }
                ids.Add(id);
            }
            foreach (KeyValuePair<string, List<string>> slot in bySlot)
            {
                if (random.NextDouble() < (slot.Key == EquipSlots.Helmet ? HatShare : 0.8))
                {
                    inventory.EquipItemId(slot.Value[random.Next(slot.Value.Count)], body);
                }
            }
        }

        private static void Strip<T>(GameObject body) where T : Component
        {
            foreach (T part in body.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(part);
            }
        }
#endif

        /// <summary>
        /// <see cref="TimedFrames"/> whole frames, milliseconds: each frame the crowd camera renders and is waited for (a pixel read back), so
        /// the graphics card's work counts as well as everything the game does; with that drawing alone, and each player-loop part.
        /// </summary>
        private IEnumerator Timed(System.Action<Cost> result)
        {
            var recorders = new ProfilerRecorder[Parts.Length];
            for (int p = 0; p < Parts.Length; p++)
            {
                recorders[p] = ProfilerRecorder.StartNew(ProfilerCategory.Internal, Parts[p].marker);
            }
            var partSums = new double[Parts.Length];
            RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var frames = new List<float>();
            double drawing = 0.0;
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var drawClock = new System.Diagnostics.Stopwatch();
            try
            {
                _camera.targetTexture = target;
                for (int i = 0; i <= TimedFrames; i++)
                {
                    LookAt(Centre() + Vector3.up);
                    drawClock.Restart();
                    _camera.Render();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                    RenderTexture.active = previous;
                    drawClock.Stop();
                    yield return null;
                    if (i > 0)
                    {
                        frames.Add((float)clock.Elapsed.TotalMilliseconds);
                        drawing += drawClock.Elapsed.TotalMilliseconds;
                        for (int p = 0; p < Parts.Length; p++)
                        {
                            partSums[p] += recorders[p].Valid ? recorders[p].LastValue * 1e-6 : 0.0;
                        }
                    }
                    clock.Restart();
                }
            }
            finally
            {
                _camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(pixel);
                foreach (ProfilerRecorder recorder in recorders)
                {
                    recorder.Dispose();
                }
            }
            var cost = new Cost { Parts = new float[Parts.Length] };
            for (int p = 0; p < Parts.Length; p++)
            {
                cost.Parts[p] = recorders[p].Valid ? (float)(partSums[p] / TimedFrames) : -1.0f;
            }
            cost.Drawing = (float)(drawing / TimedFrames);
            frames.Sort();
            float sum = 0.0f;
            foreach (float ms in frames)
            {
                sum += ms;
            }
            cost.Frame = sum / frames.Count;
            int top = Mathf.Max(1, frames.Count / 20);
            float slow = 0.0f;
            for (int i = frames.Count - top; i < frames.Count; i++)
            {
                slow += frames[i];
            }
            cost.Slowest = slow / top;
            result(cost);
        }

        private void Shoot(string name)
        {
            Texture2D image = Grab(TextureFormat.RGB24);
            CapturePng.Write(image, Path.Combine(_directory, name + ".png"));
            Object.Destroy(image);
        }

        private Texture2D Grab(TextureFormat format)
        {
            RenderTexture target = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                _camera.targetTexture = target;
                _camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, format, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                return image;
            }
            finally
            {
                _camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>
        /// A video of the crowd: <paramref name="seconds"/> of game time at 30 frames a second, the camera at the viewpoint following the
        /// crowd's middle and coming slowly down and in toward it.
        /// </summary>
        private IEnumerator Record(string path, float seconds)
        {
#if UNITY_EDITOR
            const int rate = 30;
            Time.captureFramerate = rate;
            var attributes = new UnityEditor.Media.VideoTrackAttributes
            {
                frameRate = new UnityEditor.Media.MediaRational(rate),
                width = Width,
                height = Height,
                includeAlpha = false,
                bitRateMode = UnityEditor.VideoBitrateMode.High,
            };
            int frames = Mathf.RoundToInt(seconds * rate);
            Vector3 start = _eye;
            Vector3 focus = Centre() + Vector3.up;
            using (var encoder = new UnityEditor.Media.MediaEncoder(path, attributes))
            {
                for (int i = 0; i < frames; i++)
                {
                    float t = (float)i / frames;
                    focus = Vector3.Lerp(focus, Centre() + Vector3.up, 0.05f);
                    _eye = Vector3.Lerp(start, focus + (start - focus) * 0.6f, t * t * (3.0f - 2.0f * t));
                    LookAt(focus);
                    Texture2D frame = Grab(TextureFormat.RGBA32);
                    encoder.AddFrame(frame);
                    if (i == frames / 2)
                    {
                        CapturePng.Write(frame, Path.ChangeExtension(path, null) + "-middle.png");   // to see the video is not blank without playing it
                    }
                    Object.Destroy(frame);
                    yield return null;
                }
            }
            _eye = start;
            Time.captureFramerate = 60;
#else
            yield break;
#endif
        }
    }
}
