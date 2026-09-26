using System.IO;
using CityX.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CityX.UIEditor
{
    /// <summary>
    /// Renders the built title screen to a PNG.
    ///
    /// A title screen is entirely a matter of whether it looks right, and the
    /// only two ways to find that out are to open Unity and press play, or to
    /// render it. This is the second one.
    ///
    /// Needs a real graphics device, so it must run WITHOUT -nographics.
    /// </summary>
    public static class TitleScreenCapture
    {
        private const int Width = 1600;
        private const int Height = 1000;

        [MenuItem("CityX/Capture Title Screenshot")]
        public static void Capture()
        {
            Shoot("title", TitleScreen.Panel.Menu, seedCharacters: false);
        }

        /// <summary>
        /// The idle "PRESS ANY KEY TO START" state -- rain, the flickering
        /// logo, the pulsing prompt, no menu yet. EXPLICIT ASK: "recreate
        /// the godot title screen instead of the one you have now."
        /// </summary>
        [MenuItem("CityX/Capture Title Idle Screenshot")]
        public static void CaptureIdle()
        {
            Shoot("title-idle", TitleScreen.Panel.Menu, seedCharacters: false, showIdlePrompt: true);
        }

        /// <summary>
        /// Rebuilds the title scene, then photographs the idle and menu states in the
        /// SAME Unity session -- tuning the look means change, rebuild, look, and one
        /// editor start-up instead of two.
        /// </summary>
        public static void RebuildAndCapture()
        {
            TitleScreenBuilder.Build();
            CaptureIdle();
            Capture();
        }

        /// <summary>Rebuilds the title scene, then photographs the character list and the creator -- one editor start for change, rebuild, look.</summary>
        public static void RebuildAndCaptureSelect()
        {
            TitleScreenBuilder.Build();
            CaptureCharacterSelect();
        }

        private static void ShootHairViews(string prefix, string style)
        {
            foreach (string view in new[] { "front", "side", "back" })
            {
                Shoot(prefix + view, TitleScreen.Panel.Creator, seedCharacters: false,
                    hairStyle: style, headView: view);
            }
        }

        /// <summary>Close-ups of the head wearing Hair1: front, side and back.</summary>
        [MenuItem("CityX/Capture Hair1")]
        public static void CaptureHair1()
        {
            ShootHairViews("hair-1-", Character.CharacterHair.Hair1);
        }

        /// <summary>Rebuilds the title scene, then photographs Hair1 -- one editor start for change, rebuild, look.</summary>
        public static void RebuildAndCaptureHair1()
        {
            TitleScreenBuilder.Build();
            CaptureHair1();
        }

        private static void PositionForHead(Camera camera, Character.CharacterHair hair, string view)
        {
            if (hair == null || hair.Head == null)
            {
                return;
            }
            Vector3 head = hair.Head.position;
            Vector3 forward = hair.transform.forward;
            Vector3 right = hair.transform.right;
            Vector3 from = view == "front" ? forward : view == "side" ? right : -forward;
            camera.transform.position = head + from * 1.7f + Vector3.up * 0.15f;
            camera.transform.rotation = Quaternion.LookRotation(head - camera.transform.position, Vector3.up);
            camera.fieldOfView = 38.0f;
        }

        [MenuItem("CityX/Capture Settings Screenshot")]
        public static void CaptureSettings()
        {
            Shoot("settings", TitleScreen.Panel.Settings, seedCharacters: false);
        }

        /// <summary>
        /// The character list, with characters in it.
        ///
        /// SEEDED WITH FAKES INTO A SCRATCH DIRECTORY. An empty list is the one
        /// state of this screen that says nothing about whether the rows work,
        /// and pointing the store at the real save location to fill it would
        /// mean a screenshot tool writing into somebody's characters.
        /// </summary>
        [MenuItem("CityX/Capture Character Select Screenshot")]
        public static void CaptureCharacterSelect()
        {
            Shoot("character-select", TitleScreen.Panel.CharacterSelect, seedCharacters: true);
            Shoot("character-create", TitleScreen.Panel.CharacterSelect, seedCharacters: true,
                CharacterSelect.Mode.Creating);
            Shoot("creator", TitleScreen.Panel.Creator, seedCharacters: false);
            // The hairstyle is the one thing on this screen with no default --
            // a character starts bald -- so without asking for it the shot
            // says nothing about whether the mesh lands on the head or halfway
            // down the back, which is exactly the bug the Godot build had.
            // Dyed blue, not the default: the shot also says whether the colour picker reaches the
            // hair, and a default-coloured head cannot tell that from a picker wired to nothing.
            Shoot("creator-hair", TitleScreen.Panel.Creator, seedCharacters: false,
                hairStyle: Character.CharacterHair.Hair1, hairColour: new Color(0.20f, 0.40f, 0.85f));
        }

        private static void Shoot(string name, TitleScreen.Panel panel, bool seedCharacters,
            CharacterSelect.Mode selectMode = CharacterSelect.Mode.List,
            string hairStyle = null, bool showIdlePrompt = false, string headView = null,
            Color? hairColour = null)
        {
            string outputDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs", "CityShots");
            Directory.CreateDirectory(outputDirectory);

            string scratch = null;
            string previousOverride = Core.CharacterStore.DirectoryOverride;
            if (seedCharacters)
            {
                scratch = Path.Combine(Path.GetTempPath(), "CityXShot", Path.GetRandomFileName());
                Directory.CreateDirectory(scratch);
                Core.CharacterStore.DirectoryOverride = scratch;
                SeedCharacters();
            }

            EditorSceneManager.OpenScene("Assets/CityX/Scenes/Title.unity", OpenSceneMode.Single);

            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("CityX: the title scene has no main camera.");
                return;
            }

            // THE SKY AND THE LIT WINDOWS ARE NOT PART OF THE SCENE FILE. They are
            // RenderSettings and a shared material's emission colour, both written by
            // DayNightCycle.Update -- which never runs reliably outside play mode. Left
            // alone, the shot shows whatever the last tool to touch them left behind
            // (cool-white windows after a city capture, say). Neither is what the game
            // does, so apply the frozen night by hand.
            var cycle = Object.FindFirstObjectByType<View.DayNightCycle>();
            if (cycle != null)
            {
                cycle.Apply(View.SkyCycle.At(cycle.TimeOfDay));
            }

            // The panel's OnEnable is what loads the character list, and
            // OnEnable does not run in edit mode -- so Show alone would render
            // an empty screen. Refresh is what actually reads the store.
            var title = Object.FindFirstObjectByType<TitleScreen>();
            if (title != null)
            {
                if (showIdlePrompt)
                {
                    title.ShowIdlePrompt();
                }
                else
                {
                    title.Show(panel);
                }
                if (panel == TitleScreen.Panel.CharacterSelect && title.Select != null)
                {
                    title.Select.Refresh();
                    title.Select.Show(selectMode);
                }
            }

            // THE CREATOR HAS ITS OWN CAMERA AND ITS OWN LAYER, and this only
            // ever rendered Camera.main -- so the shot came out as the night
            // city with the tool panel over it and no character anywhere,
            // which is nothing like what the screen actually shows. In the
            // game both cameras draw and the creator's, being higher up the
            // depth order, clears over the city. Rendering to a texture by
            // hand does not get that ordering for free: the creator's camera
            // has to be the one asked for the picture.
            //
            // Its stage is off in the saved scene and switched on by
            // CreatorStage's OnEnable, which -- like everything else this file
            // has had to work around -- does not run outside play mode.
            Camera shotCamera = camera;
            CreatorStage relay = null;
            int savedMask = 0;
            if (panel == TitleScreen.Panel.Creator)
            {
                // TWO screens have a stage (the character list's carousel and this one), and only this
                // one's carries a CharacterCreator -- so the first CreatorStage found is not necessarily it.
                foreach (CreatorStage candidate in Object.FindObjectsByType<CreatorStage>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate.Stage != null && candidate.Stage.GetComponent<Character.CharacterCreator>() != null)
                    {
                        relay = candidate;
                        break;
                    }
                }
                if (relay != null && relay.Stage != null)
                {
                    relay.Stage.SetActive(true);
                    var creator = relay.Stage.GetComponent<Character.CharacterCreator>();
                    if (creator != null)
                    {
                        // Awake does not run either, so without this the body
                        // wears its world material over paint-atlas UVs.
                        creator.Initialise();
                        if (hairStyle != null)
                        {
                            creator.SetHairStyle(hairStyle);
                            if (hairColour.HasValue)
                            {
                                creator.SetHairColour(hairColour.Value);
                            }
                            // The picker beside the body, showing what the hair is.
                            HairColourControls picker = Object.FindFirstObjectByType<HairColourControls>(
                                FindObjectsInactive.Include);
                            if (picker != null)
                            {
                                picker.Refresh();
                            }
                        }
                        if (creator.Camera != null)
                        {
                            shotCamera = creator.Camera;
                            savedMask = shotCamera.cullingMask;
                            if (headView != null)
                            {
                                PositionForHead(shotCamera, creator.Hair, headView);
                            }
                        }
                    }
                }
            }

            // THE CHARACTER LIST'S CHARACTERS ARE DRAWN BY AN OVERLAY CAMERA STACKED ON THE TITLE'S.
            // Rendering only the title camera would leave them out -- which is exactly how the city
            // vanishing behind them once went unnoticed: this shot never drew them at all. The stage
            // is off in the saved scene, so switch it on and let the title camera render its stack.
            CreatorStage carousel = null;
            if (panel == TitleScreen.Panel.CharacterSelect)
            {
                foreach (CreatorStage candidate in Object.FindObjectsByType<CreatorStage>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (candidate.Stage != null && candidate.Stage.GetComponent<Character.CharacterCreator>() == null)
                    {
                        carousel = candidate;
                        break;
                    }
                }
                if (carousel != null)
                {
                    carousel.Stage.SetActive(true);
                }
            }

            var canvas = Object.FindFirstObjectByType<Canvas>();
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;

            try
            {
                // Overlay canvases are composited to the display and never
                // appear in a camera render, so the interface is pointed at the
                // capture camera for the shot. The saved scene keeps overlay.
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = shotCamera;
                    canvas.planeDistance = 1.0f;

                    // The interface is built on the DEFAULT layer, and the
                    // creator's camera renders only its own -- so the first
                    // version of this rendered a character on an empty
                    // backdrop with no panel at all. Widening the mask to
                    // include Default is not the fix: the night city is on it
                    // too, and it would come straight back in behind the body.
                    // Moving the interface onto the creator's layer is, and it
                    // matches the game, where an overlay canvas draws over
                    // every camera regardless of layer. Not put back
                    // afterwards on purpose -- the scene is reopened from disk
                    // for each shot and never saved.
                    if (shotCamera != camera)
                    {
                        int layer = shotCamera.gameObject.layer;
                        foreach (Transform t in
                            canvas.GetComponentsInChildren<Transform>(true))
                        {
                            t.gameObject.layer = layer;
                        }
                    }
                }

                // A head close-up is the hair and nothing else: no panel over it.
                if (headView != null && canvas != null)
                {
                    canvas.enabled = false;
                }

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4,
                };
                shotCamera.targetTexture = target;
                shotCamera.Render();

                RenderTexture.active = target;
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();

                string path = Path.Combine(outputDirectory, name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);

                Debug.Log($"CityX: wrote {path}");
            }
            finally
            {
                RenderTexture.active = previous;
                shotCamera.targetTexture = null;
                if (shotCamera != camera)
                {
                    shotCamera.cullingMask = savedMask;
                }
                if (relay != null && relay.Stage != null)
                {
                    relay.Stage.SetActive(false);
                }
                if (carousel != null && carousel.Stage != null)
                {
                    carousel.Stage.SetActive(false);
                }
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }

                Core.CharacterStore.DirectoryOverride = previousOverride;
                if (scratch != null && Directory.Exists(scratch))
                {
                    Directory.Delete(scratch, recursive: true);
                }
            }
        }

        private static void SeedCharacters()
        {
            var made = new System.Collections.Generic.List<Core.CharacterSave>();

            Core.CharacterSave veteran = Core.CharacterSave.Create(
                "Ayaz", new Color(0.55f, 0.40f, 0.32f));
            veteran.Level = 24;
            veteran.PlaytimeSeconds = 9 * 3600 + 41 * 60;
            veteran.LastPlayedUnixSeconds = 3000;
            made.Add(veteran);

            Core.CharacterSave second = Core.CharacterSave.Create(
                "Nightshift", new Color(0.44f, 0.52f, 0.58f));
            second.Level = 7;
            second.PlaytimeSeconds = 52 * 60;
            second.LastPlayedUnixSeconds = 2000;
            made.Add(second);

            Core.CharacterSave fresh = Core.CharacterSave.Create(
                "Newcomer", new Color(0.86f, 0.76f, 0.68f));
            fresh.PlaytimeSeconds = 40;
            fresh.LastPlayedUnixSeconds = 1000;
            made.Add(fresh);

            Core.CharacterStore.Save(made);
        }
    }
}
