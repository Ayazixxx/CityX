using System.IO;
using System.Text;
using CityX.Character;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Renders the soulslike sword combat driving the REAL Player.prefab rig, so
    /// the swing can be looked at instead of trusted: a contact sheet of frames
    /// across each attack (side view on the top row, behind-the-shoulder game
    /// view on the bottom), plus a log of the blade tip's path against the
    /// startup/active/recovery windows -- the thing that says whether the
    /// ACTIVE frames land where the mocap actually strikes.
    ///
    /// Needs a real graphics device -- do not run with -nographics.
    /// </summary>
    public static class SoulsSwordCapture
    {
        private const string PlayerPrefabPath = "Assets/CityX/Character/Player.prefab";
        private const string SwordPrefabPath = "Assets/CityX/Weapons/Sword.prefab";
        private const float Step = 1.0f / 60.0f;
        private const int Cell = 300;

        private sealed class Rig
        {
            public GameObject Player;
            public GameObject Sword;
            public Animator Animator;
            public PlayerVitals Vitals;
            public SoulsSwordCombat Combat;
            public SwordWeapon Blade;
            public PlayerLocomotion Locomotion;
        }

        [MenuItem("CityX/Capture Souls Sword")]
        public static void Capture()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            Rig rig = Build();
            if (rig == null)
            {
                Finish();
                return;
            }

            try
            {
                CaptureAttack(rig, "light1", outputDirectory, () => rig.Combat.RequestLight(0.0f), rig.Combat.Light1, 8);
                Rest(rig, 2.0f);
                CaptureAttack(rig, "light2", outputDirectory, () =>
                {
                    rig.Combat.RequestLight(0.0f);
                    // Buffer the second swing so it chains at the window.
                    rig.Combat.RequestLight(0.0f);
                }, rig.Combat.Light2, 8, skipFirstSwing: true);
                Rest(rig, 2.0f);
                CaptureAttack(rig, "heavy", outputDirectory, () => rig.Combat.RequestHeavy(0.0f), rig.Combat.Heavy, 8);
                Rest(rig, 2.0f);
                CaptureBlock(rig, outputDirectory);
            }
            finally
            {
                Object.DestroyImmediate(rig.Sword);
                Object.DestroyImmediate(rig.Player);
            }

            Finish();
        }

        /// <summary>
        /// The raw clip timelines, at animator speed 1 and with no combat
        /// machinery involved: for each sword state, the blade tip's speed and
        /// position against normalised clip time. This is what the
        /// startup/active windows have to line up with -- the fitted numbers
        /// in SoulsSwordCombat are guesses until they are checked against
        /// where the blade actually is fastest.
        /// </summary>
        [MenuItem("CityX/Measure Sword Clips")]
        public static void MeasureClips()
        {
            Rig rig = Build();
            if (rig == null)
            {
                Finish();
                return;
            }

            try
            {
                var idle = new LocomotionInput { SwordEquipped = true };
                foreach (string state in new[] { "sword_attack", "sword_attack2", "sword_charged_attack" })
                {
                    rig.Animator.speed = 1.0f;
                    for (int i = 0; i < 90; i++)
                    {
                        rig.Locomotion.Tick(idle, Step);
                        rig.Animator.Update(Step);
                    }
                    rig.Animator.Update(0.0f);
                    rig.Animator.CrossFadeInFixedTime(state, 0.0f, 0);
                    rig.Animator.Update(Step);

                    var log = new StringBuilder();
                    Vector3 previousTip = rig.Blade.BladeTipWorld;
                    Vector3 previousBase = rig.Blade.BladeBaseWorld;
                    float length = rig.Animator.GetCurrentAnimatorStateInfo(0).length;
                    log.AppendLine($"--- {state}: clip length {length:0.000}s ---");
                    float peakSpeed = 0.0f;
                    float peakTime = 0.0f;
                    float maxReach = -99.0f;
                    for (float t = 0.0f; t < length; t += Step)
                    {
                        rig.Animator.Update(Step);
                        rig.Sword.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
                        Vector3 tip = rig.Player.transform.InverseTransformPoint(rig.Blade.BladeTipWorld);
                        Vector3 hand = rig.Player.transform.InverseTransformPoint(rig.Blade.BladeBaseWorld);
                        float tipSpeed = (rig.Blade.BladeTipWorld - previousTip).magnitude / Step;
                        float handSpeed = (rig.Blade.BladeBaseWorld - previousBase).magnitude / Step;
                        previousTip = rig.Blade.BladeTipWorld;
                        previousBase = rig.Blade.BladeBaseWorld;
                        if (tipSpeed > peakSpeed)
                        {
                            peakSpeed = tipSpeed;
                            peakTime = t;
                        }
                        maxReach = Mathf.Max(maxReach, tip.z, hand.z);
                        if (Mathf.RoundToInt(t / Step) % 2 == 0)
                        {
                            log.AppendLine($"norm={t / length:0.00} t={t:0.00}s tipSpeed={tipSpeed,5:0.0} handSpeed={handSpeed,5:0.0} " +
                                           $"tip=({tip.x:0.00},{tip.y:0.00},{tip.z:0.00}) hand=({hand.x:0.00},{hand.y:0.00},{hand.z:0.00})");
                        }
                    }
                    log.AppendLine($"peak tip speed {peakSpeed:0.0} m/s at norm {peakTime / length:0.00} ({peakTime:0.00}s); max forward reach {maxReach:0.00}m");
                    Debug.Log("CityX: " + log);
                }
            }
            finally
            {
                Object.DestroyImmediate(rig.Sword);
                Object.DestroyImmediate(rig.Player);
            }

            Finish();
        }

        /// <summary>
        /// The sword idle over time, sampled well past one pass of the 2s clip,
        /// with the bone positions logged -- the idle must BREATHE, not freeze
        /// on its last frame (2hsw_idle used to import with loop off).
        /// </summary>
        [MenuItem("CityX/Capture Sword Idle")]
        public static void CaptureIdle()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            Rig rig = Build();
            if (rig == null)
            {
                Finish();
                return;
            }

            try
            {
                var idle = new LocomotionInput { SwordEquipped = true };
                // Well past one 2s pass of the clip.
                for (int i = 0; i < 60 * 5; i++)
                {
                    rig.Locomotion.Tick(idle, Step);
                    rig.Animator.Update(Step);
                }

                const int frames = 8;
                var sheet = new Texture2D(Cell * frames, Cell * 2, TextureFormat.RGB24, false);
                var log = new StringBuilder();
                Transform hand = rig.Animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform head = rig.Animator.GetBoneTransform(HumanBodyBones.Head);
                for (int column = 0; column < frames; column++)
                {
                    Vector3 h = rig.Player.transform.InverseTransformPoint(hand.position);
                    Vector3 hd = rig.Player.transform.InverseTransformPoint(head.position);
                    var info = rig.Animator.GetCurrentAnimatorStateInfo(0);
                    log.AppendLine($"t={column * 0.25f:0.00} state={StateName(rig.Animator)} norm={info.normalizedTime:0.00} " +
                                   $"hand=({h.x:0.000},{h.y:0.000},{h.z:0.000}) head=({hd.x:0.000},{hd.y:0.000},{hd.z:0.000})");
                    Blit(sheet, column, rig, $"idle {column}");
                    for (int i = 0; i < 15; i++)
                    {
                        rig.Locomotion.Tick(idle, Step);
                        rig.Animator.Update(Step);
                    }
                }

                sheet.Apply();
                string path = Path.Combine(outputDirectory, "souls-idle.png");
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                Debug.Log($"CityX: wrote {path}\n{log}");
            }
            finally
            {
                Object.DestroyImmediate(rig.Sword);
                Object.DestroyImmediate(rig.Player);
            }

            Finish();
        }

        /// <summary>
        /// A light swing and the heavy thrown WHILE WALKING forward: the legs
        /// must keep stepping under the upper-body swing. Contact sheets are
        /// souls-walking-light.png and souls-walking-heavy.png.
        /// </summary>
        [MenuItem("CityX/Capture Sword Walking Swing")]
        public static void CaptureWalkingSwing()
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            Rig rig = Build();
            if (rig == null)
            {
                Finish();
                return;
            }

            try
            {
                var walk = new LocomotionInput { SwordEquipped = true, Move = new Vector2(0.0f, 1.0f) };
                for (int i = 0; i < 90; i++)
                {
                    rig.Locomotion.Tick(walk, Step);
                    rig.Animator.Update(Step);
                }

                rig.Vitals.InfiniteStamina = true;
                CaptureAttack(rig, "walking-light", outputDirectory, () => rig.Combat.RequestLight(0.0f),
                    rig.Combat.Light1, 8, walking: true);
                for (int i = 0; i < 60; i++)
                {
                    rig.Combat.Tick(Step);
                    rig.Locomotion.Tick(walk, Step);
                    rig.Animator.Update(Step);
                }
                CaptureAttack(rig, "walking-heavy", outputDirectory, () => rig.Combat.RequestHeavy(0.0f),
                    rig.Combat.Heavy, 8, walking: true);
            }
            finally
            {
                Object.DestroyImmediate(rig.Sword);
                Object.DestroyImmediate(rig.Player);
            }

            Finish();
        }

        private static void Finish()
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(0);
            }
        }

        private static Rig Build()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var swordPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            if (playerPrefab == null || swordPrefab == null)
            {
                Debug.LogError("CityX: player or sword prefab missing.");
                return null;
            }

            var rig = new Rig
            {
                Player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab),
                Sword = (GameObject)PrefabUtility.InstantiatePrefab(swordPrefab),
            };
            rig.Player.transform.position = Vector3.zero;
            rig.Player.transform.rotation = Quaternion.identity;

            rig.Locomotion = rig.Player.GetComponent<PlayerLocomotion>();
            rig.Locomotion.Bind();
            rig.Animator = rig.Player.GetComponentInChildren<Animator>();
            rig.Vitals = rig.Player.GetComponent<PlayerVitals>();
            rig.Vitals.Revive();

            // Edit mode never runs Awake, and PlayerAnimationDriver binds its
            // Animator there -- without it PlayLocalState plays nothing.
            foreach (var driver in rig.Player.GetComponents<PlayerAnimationDriver>())
            {
                driver.SendMessage("Awake");
            }

            rig.Blade = rig.Sword.GetComponent<SwordWeapon>();
            rig.Blade.OnEquip(rig.Player, CityX.Items.ItemInstanceData.None);
            rig.Combat = rig.Player.GetComponent<SoulsSwordCombat>();

            foreach (SkinnedMeshRenderer skin in rig.Player.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.updateWhenOffscreen = true;
            }

            // Settle into the sword idle first, so a swing crossfades from a
            // real pose rather than a raw bind pose.
            var idle = new LocomotionInput { SwordEquipped = true };
            for (int i = 0; i < 90; i++)
            {
                rig.Locomotion.Tick(idle, Step);
                rig.Animator.Update(Step);
            }
            return rig;
        }

        private static void Rest(Rig rig, float seconds)
        {
            var idle = new LocomotionInput { SwordEquipped = true };
            for (float t = 0.0f; t < seconds; t += Step)
            {
                rig.Combat.Tick(Step);
                rig.Vitals.TickStamina(false, Step);
                rig.Locomotion.Tick(idle, Step);
                rig.Animator.Update(Step);
            }
        }

        private static void CaptureAttack(Rig rig, string label, string outputDirectory,
            System.Action start, SoulsAttack attack, int frames, bool skipFirstSwing = false, bool walking = false)
        {
            start();

            var log = new StringBuilder();
            log.AppendLine($"--- {label}: startup {attack.Startup} active {attack.Active} recovery {attack.Recovery} " +
                           $"(anim '{attack.Animation}', animator speed fitted to {rig.Animator.speed:0.000}) ---");

            float total = attack.Total;
            var sheet = new Texture2D(Cell * frames, Cell * 2, TextureFormat.RGB24, false);
            int column = 0;
            float nextShot = 0.0f;
            float shotInterval = total / (frames - 1);
            var idle = new LocomotionInput { SwordEquipped = true, Move = walking ? new Vector2(0.0f, 1.0f) : Vector2.zero };

            Vector3 previousTip = rig.Blade.BladeTipWorld;
            for (float t = 0.0f; t <= total + Step && column < frames; t += Step)
            {
                var info = rig.Animator.GetCurrentAnimatorStateInfo(0);
                Vector3 tip = rig.Blade.BladeTipWorld;
                Vector3 tipLocal = rig.Player.transform.InverseTransformPoint(tip);
                Vector3 baseLocal = rig.Player.transform.InverseTransformPoint(rig.Blade.BladeBaseWorld);
                float tipSpeed = (tip - previousTip).magnitude / Step;
                previousTip = tip;

                if (Mathf.RoundToInt(t / Step) % 3 == 0)
                {
                    log.AppendLine($"t={t:0.00} {rig.Combat.Phase,-8} state={StateName(rig.Animator)} " +
                                   $"norm={info.normalizedTime:0.00} tipLocal=({tipLocal.x:0.00},{tipLocal.y:0.00},{tipLocal.z:0.00}) " +
                                   $"baseLocal=({baseLocal.x:0.00},{baseLocal.y:0.00},{baseLocal.z:0.00}) tipSpeed={tipSpeed:0.0}m/s");
                }

                if (t >= nextShot - 0.0001f)
                {
                    Blit(sheet, column, rig, $"{label} t={t:0.00} {rig.Combat.Phase}");
                    column++;
                    nextShot += shotInterval;
                }

                rig.Combat.Tick(Step);
                rig.Vitals.TickStamina(false, Step);
                rig.Locomotion.Tick(idle, Step);
                rig.Animator.Update(Step);
                rig.Sword.SendMessage("Update", SendMessageOptions.DontRequireReceiver);
            }

            sheet.Apply();
            string path = Path.Combine(outputDirectory, $"souls-{label}.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log($"CityX: wrote {path}\n{log}");
        }

        private static void CaptureBlock(Rig rig, string outputDirectory)
        {
            const int frames = 8;
            var log = new StringBuilder();
            log.AppendLine("--- block (held for 2.0s) ---");
            var sheet = new Texture2D(Cell * frames, Cell * 2, TextureFormat.RGB24, false);
            int column = 0;
            float nextShot = 0.0f;
            const float hold = 2.0f;
            var idle = new LocomotionInput { SwordEquipped = true };

            for (float t = 0.0f; t <= hold + Step && column < frames; t += Step)
            {
                rig.Combat.SetBlockHeld(true);
                var info = rig.Animator.GetCurrentAnimatorStateInfo(0);
                if (Mathf.RoundToInt(t / Step) % 6 == 0)
                {
                    log.AppendLine($"t={t:0.00} blocking={rig.Combat.IsBlocking} vitalsBlocking={rig.Vitals.IsBlocking} " +
                                   $"state={StateName(rig.Animator)} norm={info.normalizedTime:0.00} " +
                                   $"inTransition={rig.Animator.IsInTransition(0)}");
                }
                if (t >= nextShot - 0.0001f)
                {
                    Blit(sheet, column, rig, $"block t={t:0.00}");
                    column++;
                    nextShot += hold / (frames - 1);
                }

                rig.Combat.Tick(Step);
                rig.Vitals.TickHitstun(Step);
                rig.Locomotion.Tick(idle, Step);
                rig.Animator.Update(Step);
            }
            rig.Combat.SetBlockHeld(false);

            sheet.Apply();
            string path = Path.Combine(outputDirectory, "souls-block.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log($"CityX: wrote {path}\n{log}");
        }

        private static string StateName(Animator animator)
        {
            // The sword's swings and guard play on the upper-body layer now.
            int upper = animator.GetLayerIndex(PlayerAnimationDriver.UpperBodyLayerName);
            if (upper > 0)
            {
                foreach (string name in new[] { "sword_attack", "sword_attack2", "sword_charged_attack", "SwordBlock" })
                {
                    if (animator.GetCurrentAnimatorStateInfo(upper).IsName(name))
                    {
                        return "upper:" + name;
                    }
                }
            }

            foreach (string name in new[]
            {
                "sword_attack", "sword_attack2", "sword_charged_attack", "SwordBlock", "SwordLocomotion",
                "Locomotion", "Block", "Dash",
            })
            {
                if (animator.GetCurrentAnimatorStateInfo(0).IsName(name))
                {
                    return name;
                }
            }
            return "?";
        }

        /// <summary>Renders both views into one column of the sheet.</summary>
        private static void Blit(Texture2D sheet, int column, Rig rig, string caption)
        {
            float height = rig.Locomotion.StandingHeight;
            Vector3 chest = rig.Player.transform.position + Vector3.up * (height * 0.55f);

            // Top row: side view from the player's right, so the swing arc
            // (which happens in front, in a vertical plane) is in profile.
            Vector3 sidePosition = chest + new Vector3(5.0f, 0.3f, 0.6f);
            Texture2D side = Shoot(sidePosition, (chest + Vector3.forward * 0.8f - sidePosition).normalized, 40.0f);
            // Bottom row: behind and above the right shoulder, like the game camera.
            Vector3 behind = rig.Player.transform.position + new Vector3(0.5f, height * 0.8f, -4.0f);
            Vector3 lookAt = chest + Vector3.forward * 1.5f;
            Texture2D game = Shoot(behind, (lookAt - behind).normalized, 50.0f);

            sheet.SetPixels(column * Cell, Cell, Cell, Cell, side.GetPixels());
            sheet.SetPixels(column * Cell, 0, Cell, Cell, game.GetPixels());
            Object.DestroyImmediate(side);
            Object.DestroyImmediate(game);
            Debug.Log($"CityX: shot {column}: {caption}");
        }

        private static Texture2D Shoot(Vector3 position, Vector3 forward, float fov)
        {
            var cameraObject = new GameObject("Camera");
            var sunObject = new GameObject("Sun");
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.55f, 0.68f, 0.85f);
                camera.fieldOfView = fov;
                camera.nearClipPlane = 0.05f;
                cameraObject.transform.position = position;
                cameraObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(40.0f, 60.0f, 0.0f);

                target = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
                image.Apply();
                return image;
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                Object.DestroyImmediate(sunObject);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
