using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using CityX.Weapons;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// Long hairstyles with the Giant Boomerang on the back (Character/Body/HairPush, Items/BackClip; owner, 2026-11-12: "the hairstyles clip through the boomerang
    /// when its worn on the back", then "the shirt spikes stabbing through the shirt"), with the hair pushed out of the shirt and into the board and without.
    /// Fails if, pushed, a lone lock pokes out of the board's face or the hair leaves a patch of skin showing where it covered it, settled on the back or in
    /// any frame of catching it and putting it back (owner: "I just dont want the scalp visible from any angles", "there should be no 'lone spikes' poking
    /// through", "they can clip through slightly around the edges of the boomerang", "when doing the catch and return to back animation, there is a frame
    /// where the bald scalp is viewable ... i would much rather have a frame where the boomerang clips through the hair"). Each picture is measured with
    /// the hair drawn magenta, and with the board alone. Pictures and measures go to Logs/CityShots/backclip-*; a picture with any is saved again marked
    /// (backclip-*-marked.png: the hair on the face red, the skin green). Explicit; run by name with -Graphics.
    /// </summary>
    [Explicit("renders screenshots; needs a graphics device")]
    public sealed class BackClipCapture
    {
        /// <summary>How far in from the board's outline, pixels of a 1280 by 800 picture, its face starts: hair nearer its edge lies over the edge.</summary>
        private const int EdgeRoom = 8;

        /// <summary>The most pixels of lone hair, pushed, on the board's face: hair there that no run of hair joins to the hair off it.</summary>
        private const int SpikePixelsAllowed = 10;

        /// <summary>
        /// The most pixels, pushed, in patches of skin where the hair as modelled covered it all round (pressed all the way under the board, the kick hair
        /// left a strip of neck showing beside it, about 40 pixels wide).
        /// </summary>
        private const int BaldPixelsAllowed = 20;

        /// <summary>
        /// How far round a pixel the unpushed hair must have covered it all, and the pushed picture must show skin all round it, for it to count as bald,
        /// pixels: a strand's edge moving a pixel over is not.
        /// </summary>
        private const int CoveredAround = 2;

        /// <summary>How long after it is back on the back and the body done with it the frames are still measured, seconds.</summary>
        private const float SettleSeconds = 0.5f;

        /// <summary>Every how many frames of the catch a plain picture of the kick hair is kept (Logs/CityShots/backclip-catch-kickhair-*).</summary>
        private const int FramesBetweenShots = 3;

        private static readonly Color Marked = Color.magenta;

        private static readonly string[] Styles = { CharacterHair.KickHair, CharacterHair.Dreads, CharacterHair.Hyde, CharacterHair.AnimeHair };

        /// <summary>Where the camera stands and looks, in the body's own frame, and whether it looks at the board's outer face (where a lock through it shows).</summary>
        private static readonly (string name, Vector3 position, Vector3 lookAt, bool atItsFace)[] Views =
        {
            ("behind", new Vector3(0.6f, 2.5f, -2.6f), new Vector3(0.0f, 2.0f, 0.0f), true),
            ("side", new Vector3(2.8f, 2.3f, -0.6f), new Vector3(0.0f, 2.0f, -0.2f), false),
            ("left", new Vector3(-2.8f, 2.3f, -0.6f), new Vector3(0.0f, 2.0f, -0.2f), false),
            ("head", new Vector3(-1.1f, 2.9f, -2.2f), new Vector3(0.0f, 2.35f, 0.0f), true),
            ("above", new Vector3(0.4f, 3.7f, -1.7f), new Vector3(0.0f, 2.3f, 0.0f), true),
        };

        /// <summary>The views the catch is watched from: the game's camera over the right shoulder, and close round the head.</summary>
        private static readonly (string name, Vector3 position, Vector3 lookAt)[] CatchViews =
        {
            ("game", new Vector3(0.87f, 2.9f, -3.6f), new Vector3(0.87f, 2.4f, 0.0f)),
            ("behind", new Vector3(0.6f, 2.5f, -2.6f), new Vector3(0.0f, 2.0f, 0.0f)),
            ("head", new Vector3(-1.1f, 2.9f, -2.2f), new Vector3(0.0f, 2.35f, 0.0f)),
            ("right", new Vector3(1.6f, 2.7f, -1.6f), new Vector3(0.0f, 2.3f, 0.0f)),
        };

        /// <summary>A body with the Giant Boomerang on its back and a long hairstyle on, a camera, ground and a sun.</summary>
        private sealed class Stage
        {
            public GameObject Player;
            public Camera Camera;
            public GiantBoomerangItem Item;
            public BoomerangThrower Thrower;
            public CharacterHair Hair;
            public Renderer[] Board;
            private Material _hairMaterial;
            private GameObject _ground;
            private GameObject _sun;

            public static Stage Make()
            {
#if UNITY_EDITOR
                Time.captureFramerate = 60;
                var stage = new Stage();
                stage._ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stage._ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
                stage._ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
                var sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(45.0f, 150.0f, 0.0f);
                stage._sun = sun.gameObject;
                stage.Camera = new GameObject("Cam").AddComponent<Camera>();
                stage.Camera.fieldOfView = 30.0f;
                stage.Camera.nearClipPlane = 0.02f;
                stage.Camera.clearFlags = CameraClearFlags.SolidColor;
                stage.Camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

                stage.Player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"), Vector3.zero, Quaternion.identity);
                stage.Player.GetComponent<PlayerInputReader>().enabled = false;
                stage.Thrower = stage.Player.AddComponent<BoomerangThrower>();
                stage.Thrower.GiantThrownPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/ThrownGiantBoomerang.prefab");
                stage.Item = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/GiantBoomerang.prefab")).GetComponent<GiantBoomerangItem>();
                stage.Item.OnEquip(stage.Player, ItemInstanceData.None);
                stage.Board = stage.Item.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                stage._hairMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
                stage.Hair = stage.Player.GetComponent<CharacterHair>();
                if (stage.Hair == null)
                {
                    stage.Hair = stage.Player.AddComponent<CharacterHair>();
                }
                foreach (Transform child in stage.Player.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "head") stage.Hair.Head = child;
                }
                // One mesh per style after "none", in CharacterHair.ValidStyles order: the long ones tried here.
                var meshes = new Mesh[CharacterHair.ValidStyles.Length - 1];
                foreach ((string style, string asset) in new[] { (CharacterHair.Dreads, "DreadsMesh"), (CharacterHair.Hyde, "HydeMesh"), (CharacterHair.AnimeHair, "AnimeHairMesh"), (CharacterHair.KickHair, "KickHairMesh") })
                {
                    meshes[System.Array.IndexOf(CharacterHair.ValidStyles, style) - 1] = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/CityX/Character/Hair/{asset}.asset");
                }
                stage.Hair.StyleMeshes = meshes;
                stage.Hair.HairMaterial = stage._hairMaterial;
                return stage;
#else
                return null;
#endif
            }

            public IEnumerator Wear(string style)
            {
                var save = CharacterSave.Create("Clip", new Color(0.95f, 0.78f, 0.6f));
                save.HairStyle = style;
                save.HairColour = new Color(0.12f, 0.1f, 0.1f);
                Player.GetComponent<CharacterIdentity>().Apply(save);
                for (int i = 0; i < 10; i++) yield return null;
            }

            /// <summary>The hair pushed (out of the shirt, into the board) or as modelled.</summary>
            public void Push(bool pushed)
            {
                if (pushed) _hairMaterial.EnableKeyword(HairPush.Keyword);
                else _hairMaterial.DisableKeyword(HairPush.Keyword);
                foreach (Renderer r in Player.GetComponentsInChildren<Renderer>())
                {
                    if (r.gameObject.name != CharacterHair.WornName) continue;
                    foreach (Material m in r.materials)
                    {
                        if (pushed) m.EnableKeyword(HairPush.Keyword);
                        else m.DisableKeyword(HairPush.Keyword);
                    }
                }
            }

            /// <summary>Puts the camera where a view stands, in the body's frame.</summary>
            public (Vector3 position, Vector3 lookAt) Look(Vector3 position, Vector3 lookAt)
            {
                Vector3 from = Player.transform.TransformPoint(position), at = Player.transform.TransformPoint(lookAt);
                Camera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
                return (from, at);
            }

            /// <summary>
            /// Where, pushed, skin shows in patches that the hair as modelled covered all round, as the body stands now: anywhere, or only on the head
            /// (<paramref name="scalpOnly"/>: putting it back, hair tips tucked into the board off the shoulder are fine, the scalp is not).
            /// </summary>
            public bool[] Bald(bool scalpOnly)
            {
                Push(false);
                bool[] covered = Inside(Where(WithHairMarked(Camera, Hair), IsMarked), CoveredAround);
                if (scalpOnly) covered = Both(covered, ScalpSeen(Camera, Player, Hair));
                Push(true);
                return Both(Inside(Where(WithHairMarked(Camera, Hair), IsSkin), CoveredAround), covered);
            }

            public void Destroy()
            {
                Push(true);
                foreach (GameObject go in new[] { Player, Item != null ? Item.gameObject : null, Camera.gameObject, _ground, _sun })
                {
                    if (go != null) Object.Destroy(go);
                }
            }
        }

        [UnityTest]
        public IEnumerator RenderHairUnderTheBoomerang()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Stage stage = Stage.Make();
            var measures = new List<string>();
            var failures = new List<string>();
            foreach (string style in Styles)
            {
                yield return stage.Wear(style);
                foreach ((string view, Vector3 position, Vector3 lookAt, bool atItsFace) in Views)
                {
                    (Vector3 from, Vector3 at) = stage.Look(position, lookAt);
                    yield return null;
                    stage.Push(false);
                    BuildingShapeCapture.Shoot(stage.Camera, BuildingShapeCapture.ShotsFolder, $"backclip-{style}-through-{view}", from, at);
                    stage.Push(true);
                    BuildingShapeCapture.Shoot(stage.Camera, BuildingShapeCapture.ShotsFolder, $"backclip-{style}-pushed-{view}", from, at);
                    bool[] baldAt = stage.Bald(scalpOnly: false);
                    bool[] spikesAt = atItsFace
                        ? Alone(Where(WithHairMarked(stage.Camera, stage.Hair), IsMarked), Inside(BoardSeen(stage.Camera, stage.Board), EdgeRoom))
                        : new bool[baldAt.Length];
                    int bald = baldAt.Count(b => b), spikes = spikesAt.Count(b => b);
                    if (bald > 0 || spikes > 0)
                    {
                        MarkWhere(stage.Camera, $"backclip-{style}-{view}-marked", spikesAt, baldAt);
                    }
                    measures.Add($"{style} {view}: {spikes} pixels of hair poking out of the board's face, {bald} pixels of skin where the hair covered it");
                    if (spikes > SpikePixelsAllowed) failures.Add($"{style} from the {view}: {spikes} pixels of hair poke out of the board's face");
                    if (bald > BaldPixelsAllowed) failures.Add($"{style} from the {view}: {bald} pixels of skin show where the hair covered it");
                }
            }
            File.WriteAllLines(Path.Combine(BuildingShapeCapture.ShotsFolder, "backclip-measures.txt"), measures);
            stage.Destroy();
            Assert.IsEmpty(failures, string.Join("; ", failures));
#else
            yield break;
#endif
        }

        /// <summary>Thrown off the back, caught and put back: every frame from the hand going out for it until it has lain on the back a while.</summary>
        [UnityTest]
        public IEnumerator NoFrameOfCatchingItAndPuttingItBackShowsTheScalp()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Stage stage = Stage.Make();
            var measures = new List<string>();
            var failures = new List<string>();
            foreach (string style in new[] { CharacterHair.KickHair, CharacterHair.Dreads })
            {
                yield return stage.Wear(style);
                stage.Item.Throw(stage.Player, stage.Player.transform.position + Vector3.up * 1.8f, stage.Player.transform.forward);
                for (float t = 0.0f; t < 8.0f && !stage.Thrower.GiantMoves.IsCatching; t += Time.deltaTime) yield return null;
                if (!stage.Thrower.GiantMoves.IsCatching)
                {
                    failures.Add($"{style}: the hand never went out for it");
                    continue;
                }
                int worst = 0, frame = 0;
                string worstAt = "";
                float settledAt = -1.0f;
                for (float t = 0.0f; t < 5.0f; t += Time.deltaTime, frame++)
                {
                    foreach ((string view, Vector3 position, Vector3 lookAt) in CatchViews)
                    {
                        (Vector3 from, Vector3 at) = stage.Look(position, lookAt);
                        if (style == CharacterHair.KickHair && frame % FramesBetweenShots == 0)
                        {
                            BuildingShapeCapture.Shoot(stage.Camera, BuildingShapeCapture.ShotsFolder, $"backclip-catch-{style}-{frame:000}-{view}", from, at);
                        }
                        bool[] baldAt = stage.Bald(scalpOnly: true);
                        int bald = baldAt.Count(b => b);
                        if (bald > BaldPixelsAllowed)
                        {
                            MarkWhere(stage.Camera, $"backclip-catch-{style}-{frame:000}-{view}-marked", new bool[baldAt.Length], baldAt);
                        }
                        if (bald > worst)
                        {
                            worst = bald;
                            worstAt = $"frame {frame} from the {view}";
                        }
                    }
                    bool home = !stage.Thrower.IsOut && stage.Thrower.GiantMoves.Current == GiantBoomerangPoseDriver.Move.None;
                    if (home && settledAt < 0.0f) settledAt = t;
                    if (settledAt >= 0.0f && t > settledAt + SettleSeconds) break;
                    yield return null;
                }
                measures.Add($"{style}: {frame} frames, back on the back after {settledAt:0.00} s; at worst {worst} pixels of scalp where the hair covered it ({worstAt})");
                if (settledAt < 0.0f) failures.Add($"{style}: it was not put back on the back");
                if (worst > BaldPixelsAllowed) failures.Add($"{style}: {worst} pixels of scalp show where the hair covered it, {worstAt}");
            }
            File.WriteAllLines(Path.Combine(BuildingShapeCapture.ShotsFolder, "backclip-catch-measures.txt"), measures);
            stage.Destroy();
            Assert.IsEmpty(failures, string.Join("; ", failures));
#else
            yield break;
#endif
        }

        /// <summary>The picture with the hair drawn magenta, which nothing else is.</summary>
        private static Color32[] WithHairMarked(Camera camera, CharacterHair hair)
        {
            Color colour = hair.Colour;
            hair.SetColour(Marked);
            Color32[] pixels = Pixels(camera);
            hair.SetColour(colour);
            return pixels;
        }

        private static bool IsMarked(Color32 c) => c.g * 2 < Mathf.Min(c.r, c.b) && c.r > 30 && c.b > 30;

        /// <summary>Skin, lit or in shade: warm and not dark (the leather wraps are), redder than the board's cream.</summary>
        private static bool IsSkin(Color32 c) => c.r - c.g >= 10 && c.g >= 0.68f * c.r && c.r - c.b >= 25 && 0.3f * c.r + 0.59f * c.g + 0.11f * c.b > 95.0f;

        /// <summary>Where the board is seen: the picture with it alone drawn against the one with nothing (whose background is not one flat colour).</summary>
        private static bool[] BoardSeen(Camera camera, Renderer[] board) =>
            Showing(camera, board, Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && !board.Contains(r)).ToArray());

        /// <summary>Where the head shows past everything but the hair: the scalp the hair covers.</summary>
        private static bool[] ScalpSeen(Camera camera, GameObject body, CharacterHair hair)
        {
            Renderer[] parts = body.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            return Showing(camera, parts.Where(r => r.gameObject.name == BodySurface.HeadName).ToArray(), parts.Where(r => r.gameObject.name == CharacterHair.WornName).ToArray());
        }

        /// <summary>Where <paramref name="these"/> make a difference to the picture, with <paramref name="hidden"/> not drawn.</summary>
        private static bool[] Showing(Camera camera, Renderer[] these, Renderer[] hidden)
        {
            foreach (Renderer r in hidden) r.enabled = false;
            Color32[] with = Pixels(camera);
            foreach (Renderer r in these) r.enabled = false;
            Color32[] without = Pixels(camera);
            foreach (Renderer r in these.Concat(hidden)) r.enabled = true;
            var seen = new bool[with.Length];
            for (int i = 0; i < with.Length; i++)
            {
                seen[i] = Mathf.Abs(with[i].r - without[i].r) + Mathf.Abs(with[i].g - without[i].g) + Mathf.Abs(with[i].b - without[i].b) > 12;
            }
            return seen;
        }

        private static Color32[] Pixels(Camera camera)
        {
            Texture2D picture = BuildingShapeCapture.Render(camera);
            Color32[] pixels = picture.GetPixels32();
            Object.Destroy(picture);
            return pixels;
        }

        private static bool[] Where(Color32[] pixels, System.Func<Color32, bool> what)
        {
            var seen = new bool[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) seen[i] = what(pixels[i]);
            return seen;
        }

        /// <summary>The pixels with all of the square <paramref name="reach"/> round them in <paramref name="mask"/>.</summary>
        private static bool[] Inside(bool[] mask, int reach)
        {
            const int w = BuildingShapeCapture.ShotWidth, h = BuildingShapeCapture.ShotHeight;
            var across = new bool[mask.Length];
            for (int y = 0; y < h; y++)
            {
                int run = 0;
                for (int x = 0; x < w; x++)
                {
                    run = mask[y * w + x] ? run + 1 : 0;
                    if (run > 2 * reach) across[y * w + x - reach] = true;
                }
            }
            var inside = new bool[mask.Length];
            for (int x = 0; x < w; x++)
            {
                int run = 0;
                for (int y = 0; y < h; y++)
                {
                    run = across[y * w + x] ? run + 1 : 0;
                    if (run > 2 * reach) inside[(y - reach) * w + x] = true;
                }
            }
            return inside;
        }

        /// <summary>
        /// The hair seen on the board's face that no unbroken run of hair joins to hair off it: a lone lock poking out. Hair joined to the hair beside the
        /// board lies over its edge, as hair wrapping round it, or hangs in front of it.
        /// </summary>
        private static bool[] Alone(bool[] hair, bool[] face)
        {
            const int w = BuildingShapeCapture.ShotWidth;
            var joined = new bool[hair.Length];
            var reached = new Queue<int>();
            for (int i = 0; i < hair.Length; i++)
            {
                if (hair[i] && !face[i])
                {
                    joined[i] = true;
                    reached.Enqueue(i);
                }
            }
            int[] steps = { -1, 1, -w, w };
            while (reached.Count > 0)
            {
                int i = reached.Dequeue();
                foreach (int step in steps)
                {
                    int next = i + step;
                    bool beside = next >= 0 && next < hair.Length && (next / w == i / w || next % w == i % w);
                    if (beside && hair[next] && !joined[next])
                    {
                        joined[next] = true;
                        reached.Enqueue(next);
                    }
                }
            }
            var alone = new bool[hair.Length];
            for (int i = 0; i < hair.Length; i++) alone[i] = hair[i] && face[i] && !joined[i];
            return alone;
        }

        private static bool[] Both(bool[] a, bool[] b)
        {
            var both = new bool[a.Length];
            for (int i = 0; i < a.Length; i++) both[i] = a[i] && b[i];
            return both;
        }

        /// <summary>The picture again with hair poking out of the board's face red and skin where the hair was green, saved as name.png.</summary>
        private static void MarkWhere(Camera camera, string name, bool[] spikes, bool[] bald)
        {
            Texture2D picture = BuildingShapeCapture.Render(camera);
            Color32[] pixels = picture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (spikes[i]) pixels[i] = new Color32(255, 0, 0, 255);
                else if (bald[i]) pixels[i] = new Color32(0, 255, 0, 255);
            }
            picture.SetPixels32(pixels);
            picture.Apply();
            BuildingShapeCapture.Save(picture, BuildingShapeCapture.ShotsFolder, name);
            Object.Destroy(picture);
        }
    }
}
