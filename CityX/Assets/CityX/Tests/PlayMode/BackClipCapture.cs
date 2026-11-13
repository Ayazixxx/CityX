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
    /// Renders long hairstyles with the Giant Boomerang on the back (Character/Body/HairPush, Items/BackClip; owner, 2026-11-12: "the hairstyles clip through the boomerang
    /// when its worn on the back", then "the shirt spikes stabbing through the shirt") to Logs/CityShots/backclip-*.png, each with the hair pushed out of the shirt
    /// and into the board and without, from five sides; and fails if, pushed, a lone lock pokes out of the board's face or the hair leaves a patch of skin
    /// showing where it covered it (owner: "I just dont want the scalp visible from any angles", "there should be no 'lone spikes' poking through", "they can
    /// clip through slightly around the edges of the boomerang"). Each picture is measured again with the hair drawn magenta, and with the board alone.
    /// The measures go to Logs/CityShots/backclip-measures.txt, and a picture with any is marked (backclip-*-marked.png: the hair on the face red, the
    /// skin green). Explicit; run by name with -Graphics.
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

        private static readonly Color Marked = Color.magenta;

        /// <summary>Where the camera stands, and whether it looks at the board's outer face (where a lock through it shows on it).</summary>
        private static readonly (string name, Vector3 position, Vector3 lookAt, bool atItsFace)[] Views =
        {
            ("behind", new Vector3(0.6f, 2.5f, -2.6f), new Vector3(0.0f, 2.0f, 0.0f), true),
            ("side", new Vector3(2.8f, 2.3f, -0.6f), new Vector3(0.0f, 2.0f, -0.2f), false),
            ("left", new Vector3(-2.8f, 2.3f, -0.6f), new Vector3(0.0f, 2.0f, -0.2f), false),
            ("head", new Vector3(-1.1f, 2.9f, -2.2f), new Vector3(0.0f, 2.35f, 0.0f), true),
            ("above", new Vector3(0.4f, 3.7f, -1.7f), new Vector3(0.0f, 2.3f, 0.0f), true),
        };

        [UnityTest]
        public IEnumerator RenderHairUnderTheBoomerang()
        {
            CaptureGuard.NeedsGraphics();
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            ground.transform.localScale = new Vector3(40.0f, 1.0f, 40.0f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.transform.rotation = Quaternion.Euler(45.0f, 150.0f, 0.0f);
            var camera = new GameObject("Cam").AddComponent<Camera>();
            camera.fieldOfView = 30.0f;
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.66f, 0.8f);

            GameObject player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                Vector3.zero, Quaternion.identity);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.AddComponent<BoomerangThrower>();
            var item = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Weapons/GiantBoomerang.prefab"))
                .GetComponent<GiantBoomerangItem>();
            item.OnEquip(player, ItemInstanceData.None);
            Material hairMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/CityX/Character/Materials/Hair.mat");
            var hair = player.GetComponent<CharacterHair>();
            if (hair == null)
            {
                hair = player.AddComponent<CharacterHair>();
            }
            foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "head") hair.Head = child;
            }
            // One mesh per style after "none", in CharacterHair.ValidStyles order: the long ones tried here.
            var meshes = new Mesh[CharacterHair.ValidStyles.Length - 1];
            foreach ((string style, string asset) in new[] { (CharacterHair.Dreads, "DreadsMesh"), (CharacterHair.Hyde, "HydeMesh"), (CharacterHair.AnimeHair, "AnimeHairMesh"), (CharacterHair.KickHair, "KickHairMesh") })
            {
                meshes[System.Array.IndexOf(CharacterHair.ValidStyles, style) - 1] = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/CityX/Character/Hair/{asset}.asset");
            }
            hair.StyleMeshes = meshes;
            hair.HairMaterial = hairMaterial;
            Renderer[] board = item.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            var measures = new List<string>();
            var failures = new List<string>();
            foreach (string style in new[] { CharacterHair.KickHair, CharacterHair.Dreads, CharacterHair.Hyde, CharacterHair.AnimeHair })
            {
                var save = CharacterSave.Create("Clip", new Color(0.95f, 0.78f, 0.6f));
                save.HairStyle = style;
                save.HairColour = new Color(0.12f, 0.1f, 0.1f);
                player.GetComponent<CharacterIdentity>().Apply(save);
                for (int i = 0; i < 10; i++) yield return null;
                var covered = new Dictionary<string, bool[]>();
                foreach (bool pushed in new[] { false, true })
                {
                    if (pushed) hairMaterial.EnableKeyword(HairPush.Keyword);
                    else hairMaterial.DisableKeyword(HairPush.Keyword);
                    foreach (Renderer r in player.GetComponentsInChildren<Renderer>())
                    {
                        if (r.gameObject.name == CharacterHair.WornName)
                        {
                            foreach (Material m in r.materials)
                            {
                                if (pushed) m.EnableKeyword(HairPush.Keyword);
                                else m.DisableKeyword(HairPush.Keyword);
                            }
                        }
                    }
                    foreach ((string view, Vector3 position, Vector3 lookAt, bool atItsFace) in Views)
                    {
                        camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                        yield return null;
                        BuildingShapeCapture.Shoot(camera, BuildingShapeCapture.ShotsFolder, $"backclip-{style}-{(pushed ? "pushed" : "through")}-{view}", position, lookAt);
                        Color32[] marked = WithHairMarked(camera, hair);
                        bool[] hairSeen = Where(marked, IsMarked);
                        if (!pushed)
                        {
                            covered[view] = Inside(hairSeen, CoveredAround);
                            continue;
                        }
                        bool[] baldAt = Both(Inside(Where(marked, IsSkin), CoveredAround), covered[view]);
                        bool[] spikesAt = atItsFace ? Alone(hairSeen, Inside(BoardSeen(camera, board), EdgeRoom)) : new bool[hairSeen.Length];
                        int bald = baldAt.Count(at => at), spikes = spikesAt.Count(at => at);
                        if (bald > 0 || spikes > 0)
                        {
                            MarkWhere(camera, $"backclip-{style}-{view}-marked", spikesAt, baldAt);
                        }
                        measures.Add($"{style} {view}: {spikes} pixels of hair poking out of the board's face, {bald} pixels of skin where the hair covered it");
                        if (spikes > SpikePixelsAllowed) failures.Add($"{style} from the {view}: {spikes} pixels of hair poke out of the board's face");
                        if (bald > BaldPixelsAllowed) failures.Add($"{style} from the {view}: {bald} pixels of skin show where the hair covered it");
                    }
                }
            }
            File.WriteAllLines(Path.Combine(BuildingShapeCapture.ShotsFolder, "backclip-measures.txt"), measures);
            hairMaterial.EnableKeyword(HairPush.Keyword);
            foreach (GameObject go in new[] { player, item.gameObject, camera.gameObject, ground, sun.gameObject })
            {
                if (go != null) Object.Destroy(go);
            }
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
        private static bool[] BoardSeen(Camera camera, Renderer[] board)
        {
            Renderer[] others = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && !board.Contains(r)).ToArray();
            foreach (Renderer r in others) r.enabled = false;
            Color32[] alone = Pixels(camera);
            foreach (Renderer r in board) r.enabled = false;
            Color32[] empty = Pixels(camera);
            foreach (Renderer r in board.Concat(others)) r.enabled = true;
            var seen = new bool[alone.Length];
            for (int i = 0; i < alone.Length; i++)
            {
                seen[i] = Mathf.Abs(alone[i].r - empty[i].r) + Mathf.Abs(alone[i].g - empty[i].g) + Mathf.Abs(alone[i].b - empty[i].b) > 12;
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
