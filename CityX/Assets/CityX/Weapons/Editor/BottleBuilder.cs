using System.Collections.Generic;
using CityX.Character;
using CityX.Items;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds every glass bottle (Items/GlassBottleKinds, docs/GLASS_BOTTLE.md) from its shape (BottleShape): for each, the bottle in the
    /// hand and on the ground (whole, broken to a half with sharp points, broken down to its neck), the bottle in the air, and the pieces it
    /// bursts into. The whole bottle is turned from its profile, or is the owner's own model (the first bottle, welded and smoothed); the
    /// broken ones and the pieces are cut from that same bottle (BottleMesh), so a broken bottle is always the bottle it was.
    ///
    /// Everything is made in the HELD bottle's frame (the fist's middle on the neck at the origin, the bottle out along +Y to its base) and
    /// at its size in the game, so nothing is scaled or turned in the prefabs.
    /// </summary>
    public static class BottleBuilder
    {
        public const string Folder = "Assets/CityX/Weapons/Bottles";

        /// <summary>How finely the whole bottle is turned, and the bottle its broken halves and its pieces are cut from (the cuts need the finer).</summary>
        private const int Sides = 20;
        private const float RingEvery = 0.012f;
        private const int CutSides = 36;
        private const float CutRingEvery = 0.006f;

        /// <summary>How many pieces a bottle bursts into, and each one's weight, kg.</summary>
        private const int PieceCount = 14;
        private const float PieceMass = 0.03f;

        /// <summary>The label stands this far off the glass, metres of a real bottle, and a printed picture this far (under the label's edge).</summary>
        private const float LabelProud = 0.0007f;
        private const float PrintProud = 0.0004f;

        /// <summary>How finely a printed picture follows the glass: across it and up it.</summary>
        private const int PrintColumns = 18;
        private const int PrintRows = 12;

        /// <summary>Glass shines; paper does not; a cap is painted tin.</summary>
        private const float GlassSmoothness = 0.88f;
        private const float PaperSmoothness = 0.1f;
        private const float PrintSmoothness = 0.6f;
        private const float CapSmoothness = 0.6f;
        private const float CapMetallic = 0.5f;

        private const string ImpactCharacterSound = "Assets/CityX/Weapons/Audio/glass_hit_enemy.mp3";
        private const string ImpactTerrainSound = "Assets/CityX/Weapons/Audio/glass_hit_terrain.mp3";

        public static string ShatterPrefabPath(string itemId) => $"{Folder}/{itemId}_shatter.prefab";

        [MenuItem("CityX/Rebuild Glass Bottles")]
        public static void BuildAllFromMenu() => BuildAll();

        /// <summary>The first bottle's prefab (every bottle is built).</summary>
        public static GameObject Build()
        {
            BuildAll();
            return AssetDatabase.LoadAssetAtPath<GameObject>(GlassBottleKinds.PrefabPath(GlassBottleKinds.Classic));
        }

        /// <summary>Every bottle's prefab, for an inventory's list.</summary>
        public static Inventory.ItemPrefabEntry[] BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Bottles");
            }
            var entries = new List<Inventory.ItemPrefabEntry>();
            foreach (GlassBottleKinds.Kind kind in GlassBottleKinds.All)
            {
                GameObject prefab = BuildOne(BottleShape.For(kind.ItemId));
                if (prefab != null)
                {
                    entries.Add(new Inventory.ItemPrefabEntry { ItemId = kind.ItemId, Prefab = prefab });
                }
            }
            AssetDatabase.SaveAssets();
            return entries.ToArray();
        }

        /// <summary>What a bottle is drawn in.</summary>
        private sealed class Look
        {
            public Material Glass, OpenGlass, Label, Print, Cap;
        }

        private static GameObject BuildOne(BottleShape shape)
        {
            string id = shape.ItemId;
            var look = new Look
            {
                Glass = HeldModelBaker.LitMaterial($"{Folder}/{id}_glass.mat", shape.Glass, 0.0f, GlassSmoothness),
                // A broken bottle and its pieces are seen from inside too.
                OpenGlass = HeldModelBaker.LitMaterial($"{Folder}/{id}_glass_open.mat", shape.Glass, 0.0f, GlassSmoothness),
                Label = shape.HasLabel ? HeldModelBaker.LitMaterial($"{Folder}/{id}_label.mat", shape.Label, 0.0f, PaperSmoothness) : null,
                Cap = shape.Cap != null ? HeldModelBaker.LitMaterial($"{Folder}/{id}_cap.mat", shape.CapColour, CapMetallic, CapSmoothness) : null,
            };
            look.OpenGlass.SetFloat("_Cull", 0.0f);
            if (look.Label != null)
            {
                look.Label.SetFloat("_Cull", 0.0f);
            }
            if (shape.DecalPath != null)
            {
                look.Print = PrintMaterial($"{Folder}/{id}_print.mat", shape.DecalPath);
            }

            BottleMesh.Model whole = shape.Profile != null ? BottleMesh.Turned(shape.Profile, Sides, RingEvery) : Smoothed(shape.ModelPath);
            BottleMesh.Model forCuts = shape.Profile != null ? BottleMesh.Turned(shape.Profile, CutSides, CutRingEvery) : whole;
            BottleMesh.Model label = shape.HasLabel
                ? BottleMesh.Turned(new[]
                {
                    new Vector2(shape.BodyRadius + LabelProud, shape.LabelFrom), new Vector2(shape.BodyRadius + LabelProud, shape.LabelTo),
                }, CutSides, CutRingEvery)
                : null;
            // The printed picture, once on each side of the bottle: the sides that face ahead and back as it is carried (the fist's thumb and little-finger sides face across the body).
            BottleMesh.Model print = shape.DecalPath != null && shape.Profile != null
                ? BottleMesh.Joined(
                    BottleMesh.Printed(shape.Profile, shape.DecalFrom, shape.DecalTo, 90.0f, shape.DecalArcDegrees, PrintProud, PrintColumns, PrintRows, CutRingEvery),
                    BottleMesh.Printed(shape.Profile, shape.DecalFrom, shape.DecalTo, 270.0f, shape.DecalArcDegrees, PrintProud, PrintColumns, PrintRows, CutRingEvery))
                : null;
            BottleMesh.Model cap = shape.Cap != null ? BottleMesh.Turned(shape.Cap, Sides, RingEvery) : null;

            GameObject shatter = BuildShatter(shape, forCuts, look);
            GameObject thrown = BuildThrown(shape, whole, label, print, cap, look, shatter);

            var root = new GameObject(id);
            try
            {
                var intact = new GameObject("Intact");
                intact.transform.SetParent(root.transform, false);
                Part(intact.transform, "Glass", Saved(shape, "whole", whole), look.Glass);
                AddDressing(intact.transform, shape, "whole", label, print, cap, look);

                GameObject broken = Broken(root.transform, "Broken", shape, forCuts, label, print, cap, look, shape.BrokenAt, BottleShape.LongestPoint, "broken");
                GameObject neck = Broken(root.transform, "Shattered", shape, forCuts, null, null, cap, look, shape.ShatteredAt, BottleShape.LongestPoint * 0.6f, "neck");
                broken.SetActive(false);
                neck.SetActive(false);

                AddBody(root, shape);
                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = BottleWeapon.InteractRange;

                var bottle = root.AddComponent<BottleWeapon>();
                bottle.SetItemId(id);
                bottle.IntactVisual = intact;
                bottle.BrokenVisual = broken;
                bottle.ShatteredVisual = neck;
                bottle.ThrownBottlePrefab = thrown;
                bottle.ShatterPrefab = shatter;
                bottle.BreakSound = AssetDatabase.LoadAssetAtPath<AudioClip>(ImpactTerrainSound);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, GlassBottleKinds.PrefabPath(id));
                Debug.Log($"CityX: {id} written to {GlassBottleKinds.PrefabPath(id)} -- {whole.TriangleCount} triangles whole");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The owner's model as a bottle standing on its base: its points welded and its faces smoothed over (it came as loose triangles).</summary>
        private static BottleMesh.Model Smoothed(string modelPath)
        {
            HeldModelBaker.Load(modelPath, out Vector3[] vertices, out _, out int[] triangles);
            var model = new BottleMesh.Model();
            var at = new Dictionary<Vector3Int, int>();
            var remap = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3Int place = Vector3Int.RoundToInt(vertices[i] * 100000.0f);
                if (!at.TryGetValue(place, out int made))
                {
                    made = model.Vertices.Count;
                    model.Vertices.Add(vertices[i]);
                    model.Normals.Add(Vector3.zero);
                    at[place] = made;
                }
                remap[i] = made;
            }
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = remap[triangles[t]], b = remap[triangles[t + 1]], c = remap[triangles[t + 2]];
                if (a == b || b == c || a == c)
                {
                    continue;
                }
                Vector3 face = Vector3.Cross(model.Vertices[b] - model.Vertices[a], model.Vertices[c] - model.Vertices[a]);
                model.Normals[a] += face;
                model.Normals[b] += face;
                model.Normals[c] += face;
                model.Triangles.Add(a);
                model.Triangles.Add(b);
                model.Triangles.Add(c);
            }
            for (int i = 0; i < model.Normals.Count; i++)
            {
                model.Normals[i] = model.Normals[i].sqrMagnitude > 1e-20f ? model.Normals[i].normalized : Vector3.up;
            }
            return model;
        }

        /// <summary>A standing model saved as a mesh in the held bottle's frame, at the game's size.</summary>
        private static Mesh Saved(BottleShape shape, string part, BottleMesh.Model standing)
        {
            BottleMesh.Model held = BottleMesh.InHand(standing, shape.GripHeight, BottleShape.Scale);
            return HeldModelBaker.Save($"{Folder}/{shape.ItemId}_{part}.asset", $"{shape.ItemId}_{part}", held.Vertices.ToArray(), held.Normals.ToArray(),
                held.Triangles.ToArray(), held.HasUvs ? held.Uvs.ToArray() : null);
        }

        private static void Part(Transform parent, string name, Mesh mesh, Material material)
        {
            HeldModelBaker.Part(parent, name, mesh, material);
        }

        private static void AddDressing(Transform parent, BottleShape shape, string part, BottleMesh.Model label, BottleMesh.Model print,
            BottleMesh.Model cap, Look look)
        {
            if (print != null && print.TriangleCount > 0)
            {
                Part(parent, "Print", Saved(shape, part + "_print", print), look.Print);
            }
            if (label != null && label.TriangleCount > 0)
            {
                Part(parent, "Label", Saved(shape, part + "_label", label), look.Label);
            }
            if (cap != null)
            {
                Part(parent, "Cap", Saved(shape, "cap", cap), look.Cap);
            }
        }

        /// <summary>The bottle from its mouth down to a break at <paramref name="level"/>, with what is left of its label, and its cap.</summary>
        private static GameObject Broken(Transform parent, string name, BottleShape shape, BottleMesh.Model forCuts, BottleMesh.Model label,
            BottleMesh.Model print, BottleMesh.Model cap, Look look, float level, float longestPoint, string part)
        {
            var broken = new GameObject(name);
            broken.transform.SetParent(parent, false);
            Part(broken.transform, "Glass", Saved(shape, part, BottleMesh.BrokenOff(forCuts, level, longestPoint)), look.OpenGlass);
            // The label and the print torn along the same break as the glass under them.
            AddDressing(broken.transform, shape, part, label != null ? BottleMesh.BrokenOff(label, level, longestPoint) : null,
                print != null ? BottleMesh.BrokenOff(print, level, longestPoint) : null, cap, look);
            return broken;
        }

        /// <summary>
        /// A picture printed on the glass: the picture as it is where it is printed and nothing where it is clear (cut out, not blended: blended
        /// it is drawn after the glass and in no order with it), seen from inside too through a broken bottle, its sheen a little under the glass's.
        /// </summary>
        private static Material PrintMaterial(string path, string picturePath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(picturePath);
            if (importer != null && (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency))
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            Material material = HeldModelBaker.LitMaterial(path, Color.white, 0.0f, PrintSmoothness);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(picturePath));
            material.SetFloat("_AlphaClip", 1.0f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0.0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>A body and a capsule about the whole bottle.</summary>
        private static void AddBody(GameObject root, BottleShape shape)
        {
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = shape.BodyRadius * BottleShape.Scale;
            capsule.height = shape.Height * BottleShape.Scale;
            capsule.center = new Vector3(0.0f, (shape.GripHeight - shape.Height * 0.5f) * BottleShape.Scale, 0.0f);
            var body = root.AddComponent<Rigidbody>();
            body.mass = BottleWeapon.BottleMass;
        }

        private static GameObject BuildThrown(BottleShape shape, BottleMesh.Model whole, BottleMesh.Model label, BottleMesh.Model print, BottleMesh.Model cap, Look look,
            GameObject shatter)
        {
            var root = new GameObject(shape.ItemId + "_thrown");
            try
            {
                Part(root.transform, "Glass", Saved(shape, "whole", whole), look.Glass);
                AddDressing(root.transform, shape, "whole", label, print, cap, look);
                AddBody(root, shape);
                var thrown = root.AddComponent<ThrownBottle>();
                thrown.ShatterPrefab = shatter;
                thrown.ImpactCharacterSound = AssetDatabase.LoadAssetAtPath<AudioClip>(ImpactCharacterSound);
                thrown.ImpactTerrainSound = AssetDatabase.LoadAssetAtPath<AudioClip>(ImpactTerrainSound);
                return PrefabUtility.SaveAsPrefabAsset(root, GlassBottleKinds.ThrownPrefabPath(shape.ItemId));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The bottle in pieces (BottleShatter): each a body of its own where it was in the bottle, and a burst of glints.</summary>
        private static GameObject BuildShatter(BottleShape shape, BottleMesh.Model forCuts, Look look)
        {
            var root = new GameObject(shape.ItemId + "_shatter");
            try
            {
                List<BottleMesh.Piece> pieces = BottleMesh.Pieces(forCuts, PieceCount, shape.NeckBase, shape.ItemId.Length * 7919);
                var bodies = new List<Rigidbody>();
                var along = new List<float>();
                for (int i = 0; i < pieces.Count; i++)
                {
                    BottleMesh.Model model = pieces[i].Model;
                    var vertices = new Vector3[model.Vertices.Count];
                    var normals = new Vector3[model.Normals.Count];
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        vertices[v] = BottleMesh.HeldDirection(model.Vertices[v]) * BottleShape.Scale;
                        normals[v] = BottleMesh.HeldDirection(model.Normals[v]);
                    }
                    Mesh mesh = HeldModelBaker.Save($"{Folder}/{shape.ItemId}_piece{i}.asset", $"{shape.ItemId}_piece{i}", vertices, normals,
                        model.Triangles.ToArray());
                    Transform piece = HeldModelBaker.Part(root.transform, "Piece" + i, mesh, look.OpenGlass);
                    piece.localPosition = BottleMesh.Held(pieces[i].Centre, shape.GripHeight, BottleShape.Scale);
                    var box = piece.gameObject.AddComponent<BoxCollider>();
                    box.center = mesh.bounds.center;
                    box.size = Vector3.Max(mesh.bounds.size, Vector3.one * 0.02f);
                    var body = piece.gameObject.AddComponent<Rigidbody>();
                    body.mass = PieceMass;
                    bodies.Add(body);
                    along.Add(piece.localPosition.y);
                }
                var shatter = root.AddComponent<BottleShatter>();
                shatter.Pieces = bodies.ToArray();
                shatter.AlongBottle = along.ToArray();
                AddGlints(root.transform, shape);
                return PrefabUtility.SaveAsPrefabAsset(root, ShatterPrefabPath(shape.ItemId));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- the glints: a spray of bright specks of glass, gone in half a second ----

        private const string GlintMaterialPath = Folder + "/BottleGlints.mat";
        private const int Glints = 26;
        private const float GlintSeconds = 0.5f;
        private const float GlintSize = 0.035f;

        private static void AddGlints(Transform parent, BottleShape shape)
        {
            var glints = new GameObject("Glints");
            glints.transform.SetParent(parent, false);
            glints.transform.localPosition = new Vector3(0.0f, shape.Reach * 0.5f, 0.0f);
            ParticleSystem particles = glints.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.duration = GlintSeconds;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(GlintSeconds * 0.5f, GlintSeconds);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 6.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(GlintSize * 0.4f, GlintSize);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
            // The glass's own colour, most of the way to white: a glint is the light off it.
            Color bright = Color.Lerp(shape.Glass, Color.white, 0.65f);
            main.startColor = bright;
            main.gravityModifier = 1.2f;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Glints;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0.0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, Glints) });
            ParticleSystem.ShapeModule burst = particles.shape;
            burst.enabled = true;
            burst.shapeType = ParticleSystemShapeType.Sphere;
            burst.radius = shape.BodyRadius * BottleShape.Scale;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.0f));

            var material = AssetDatabase.LoadAssetAtPath<Material>(GlintMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(material, GlintMaterialPath);
                var speck = new Mesh { name = "Glint" };
                speck.SetVertices(new[] { new Vector3(-0.5f, -0.4f, 0.0f), new Vector3(0.5f, -0.2f, 0.0f), new Vector3(0.1f, 0.6f, 0.0f) });
                speck.SetTriangles(new[] { 0, 1, 2, 0, 2, 1 }, 0);
                speck.RecalculateNormals();
                speck.RecalculateBounds();
                AssetDatabase.AddObjectToAsset(speck, material);   // parked on the material, so it is not loose
            }
            material.color = Color.white;
            ParticleSystemRenderer renderer = glints.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(GlintMaterialPath))
            {
                if (sub is Mesh speck)
                {
                    renderer.mesh = speck;
                }
            }
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
