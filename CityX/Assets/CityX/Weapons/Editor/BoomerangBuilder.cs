using System.Collections.Generic;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the boomerangs (docs/BOOMERANG.md), stand-ins drawn in code until the owner's models, each a V lofted along its length:
    ///  * the Wooden Boomerang: a flat carved V, its tips painted so its spin reads. Boomerang.prefab is the one in the hand (BoomerangItem), ThrownBoomerang.prefab
    ///    the one in the air. 70 cm tip to tip, well over the city's 1.3 times life, so that it reads in the air at its range;
    ///  * the Giant Boomerang: a wide, thick, bone-white blade as tall as the player (2.6 m tip to tip), leather wrapped at its grip near one end, round its elbow
    ///    and near its other tip. GiantBoomerang.prefab is the one carried (GiantBoomerangItem: its origin is the grip, so the hand and the back hold it there),
    ///    ThrownGiantBoomerang.prefab the one in the air;
    ///  * the Iron Spiked Giant Boomerang: the owner's Meshy model (<see cref="BuildIronSpiked"/>), its arms as long as the bone one's, carried and thrown as it is but turned over (its spikes away from the camera on the back).
    /// The thrown ones lie flat with their middle at the origin, which is what they spin about.
    /// </summary>
    public static class BoomerangBuilder
    {
        private const string Folder = "Assets/CityX/Weapons/Boomerang";
        public const string PrefabPath = "Assets/CityX/Weapons/Boomerang.prefab";
        public const string ThrownPrefabPath = "Assets/CityX/Weapons/ThrownBoomerang.prefab";
        public const string GiantPrefabPath = "Assets/CityX/Weapons/GiantBoomerang.prefab";
        public const string ThrownGiantPrefabPath = "Assets/CityX/Weapons/ThrownGiantBoomerang.prefab";

        private static readonly Color Wood = new Color(0.55f, 0.34f, 0.17f);
        private static readonly Color Paint = new Color(0.72f, 0.16f, 0.1f);
        private static readonly Color Bone = new Color(0.84f, 0.80f, 0.68f);
        private static readonly Color Leather = new Color(0.33f, 0.20f, 0.11f);

        /// <summary>
        /// A boomerang's shape: each arm's length and how far it opens from straight down the middle, how round the elbow is (a share of an arm), its width at
        /// the elbow and at the tips, its thickness, and the sides of each slice.
        /// </summary>
        private readonly struct Shape
        {
            public readonly float Arm, OpenDegrees, Roundness, WidthAtElbow, WidthAtTip, Thickness;
            public readonly int Sides;

            public Shape(float arm, float openDegrees, float roundness, float widthAtElbow, float widthAtTip, float thickness, int sides)
            {
                Arm = arm;
                OpenDegrees = openDegrees;
                Roundness = roundness;
                WidthAtElbow = widthAtElbow;
                WidthAtTip = widthAtTip;
                Thickness = thickness;
                Sides = sides;
            }
        }

        private static readonly Shape WoodenShape = new Shape(0.45f, 50.0f, 0.12f, 0.115f, 0.075f, 0.03f, 6);

        /// <summary>2.6 m tip to tip: two arms of 1.4 m opened wide.</summary>
        private static readonly Shape GiantShape = new Shape(1.4f, 68.0f, 0.1f, 0.42f, 0.3f, 0.11f, 8);

        /// <summary>Slices along it, and where along each arm (0 elbow, 1 tip) the wooden one's paint starts.</summary>
        private const int Slices = 24;

        private const int GiantSlices = 40;

        private const float PaintFrom = 0.72f;

        /// <summary>The paint and the wraps stand this proud of the body.</summary>
        private const float PaintProud = 0.0015f;

        private const float WrapProud = 0.008f;

        /// <summary>Where the giant one is held, along it (-1 one tip, 1 the other), and its leather wraps (from, to).</summary>
        public const float GiantGripAt = 0.7f;

        private static readonly Vector2[] GiantWraps =
        {
            new Vector2(0.6f, 0.8f), new Vector2(-0.07f, -0.02f), new Vector2(0.02f, 0.07f), new Vector2(-0.86f, -0.81f),
        };

        [MenuItem("CityX/Rebuild Boomerang")]
        public static GameObject Build()
        {
            EnsureFolder();
            Material wood = HeldModelBaker.LitMaterial($"{Folder}/BoomerangWood.mat", Wood, 0.0f, 0.35f);
            Material paint = HeldModelBaker.LitMaterial($"{Folder}/BoomerangPaint.mat", Paint, 0.0f, 0.45f);
            Vector3 middle = MiddleOf(WoodenShape);
            Mesh body = Loft($"{Folder}/BoomerangBody.asset", "BoomerangBody", WoodenShape, middle, Slices, new[] { new Vector2(-1.0f, 1.0f) }, 0.0f);
            Mesh tips = Loft($"{Folder}/BoomerangTips.asset", "BoomerangTips", WoodenShape, middle, 6,
                new[] { new Vector2(-1.0f, -PaintFrom), new Vector2(PaintFrom, 1.0f) }, PaintProud);

            var thrown = new GameObject("ThrownBoomerang");
            var held = new GameObject("Boomerang");
            try
            {
                var spinner = new GameObject("Spinner");
                spinner.transform.SetParent(thrown.transform, false);
                AddModel(spinner.transform, body, tips, wood, paint);
                thrown.AddComponent<ThrownBoomerang>().Spinner = spinner.transform;
                PrefabUtility.SaveAsPrefabAsset(thrown, ThrownPrefabPath);

                AddModel(held.transform, body, tips, wood, paint);
                var box = held.AddComponent<BoxCollider>();
                box.size = new Vector3(0.7f, WoodenShape.Thickness * 2.0f, 0.4f);
                held.AddComponent<BoomerangItem>();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(held, PrefabPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(thrown);
                Object.DestroyImmediate(held);
            }
        }

        [MenuItem("CityX/Rebuild Giant Boomerang")]
        public static GameObject BuildGiant()
        {
            EnsureFolder();
            Material bone = HeldModelBaker.LitMaterial($"{Folder}/GiantBoomerangBone.mat", Bone, 0.0f, 0.3f);
            Material leather = HeldModelBaker.LitMaterial($"{Folder}/GiantBoomerangLeather.mat", Leather, 0.0f, 0.25f);
            Vector3 middle = MiddleOf(GiantShape);
            Mesh body = Loft($"{Folder}/GiantBoomerangBody.asset", "GiantBoomerangBody", GiantShape, middle, GiantSlices, new[] { new Vector2(-1.0f, 1.0f) }, 0.0f);
            Mesh wraps = Loft($"{Folder}/GiantBoomerangWraps.asset", "GiantBoomerangWraps", GiantShape, middle, 4, GiantWraps, WrapProud);
            return SaveGiant("ThrownGiantBoomerang", ThrownGiantPrefabPath, "GiantBoomerang", GiantPrefabPath, GiantBoomerangModel.Bone, BoneOutline(),
                new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.6f, GiantShape.Thickness * 2.0f, 2.4f), parent => AddGiantModel(parent, body, wraps, bone, leather));
        }

        /// <summary>The bone one's shape in its thrown frame.</summary>
        private static GiantOutline BoneOutline()
        {
            Vector3 middle = MiddleOf(GiantShape);
            return new GiantOutline(AlongTheV(GiantShape, GiantGripAt) - middle, AlongTheV(GiantShape, 0.0f) - middle, AlongTheV(GiantShape, -1.0f) - middle,
                AlongTheV(GiantShape, 1.0f) - middle, GiantShape.WidthAtElbow * 0.5f, GiantShape.WidthAtTip * 0.5f);
        }

        /// <summary>From a giant boomerang's thrown frame into its carried one: the grip at the origin, its length from there toward the elbow along +z.</summary>
        private static Quaternion CarriedTurn(in GiantOutline shape) => Quaternion.Inverse(Quaternion.LookRotation((shape.Elbow - shape.Grip).normalized, Vector3.up));

        private static Vector3 Carried(in GiantOutline shape, Vector3 p) => CarriedTurn(shape) * (p - shape.Grip);

        public const string IronSpikedPrefabPath = "Assets/CityX/Weapons/IronSpikedGiantBoomerang.prefab";
        public const string ThrownIronSpikedPrefabPath = "Assets/CityX/Weapons/ThrownIronSpikedGiantBoomerang.prefab";
        private const string IronSpikedSourcePath = "Assets/CityX/Weapons/IronSpikedBoomerang.glb";
        private const string IronSpikedColourPath = "Assets/CityX/Weapons/IronSpikedBoomerang.jpg";
        private const string IronSpikedPointsPath = "Assets/CityX/Weapons/IronSpikedBoomerang.points.json";

        /// <summary>
        /// Its steel and leather: Meshy's paint baked onto one map; a little metallic (more and the cel light draws steel nearly black: docs/FIREARMS.md, the
        /// AK-47 remodelled).
        /// </summary>
        private const float IronSpikedMetallic = 0.3f;

        private const float IronSpikedSmoothness = 0.4f;

        /// <summary>How much wider than its arms' middles the hair is laid flat under it on the back: its spikes stand out past them.</summary>
        private const float IronSpikedOutlineSpare = 1.15f;

        /// <summary>
        /// Where it hangs on the back, from the spine bone (GiantBoomerangItem.HangPosition, HangEuler): set by the owner by hand on the live boomerang in the
        /// editor (2026-11-20, of it leant off the back: "honestly just let me set the position myself", then "this is the new positioning i want for the
        /// boomerang"): upside down across the back, its elbow up between the shoulders.
        /// </summary>
        private static readonly Vector3 IronSpikedHangPosition = new Vector3(-0.92f, 0.15f, -0.36f);

        private static readonly Vector3 IronSpikedHangEuler = new Vector3(516.5f, -90.0f, 90.0f);

        /// <summary>
        /// Where the right hand holds it, from the hand bone (GiantBoomerangItem.HoldPosition, HoldEuler; owner, 2026-11-20: "i want it to grab the boomerang
        /// by the rope and maybe have the fingers clipped into the rope strategically so it looks like its got a grip on the rope", "an above grip like how
        /// sasuke holds the demon wind shuriken"): the palm on top of the wrap of the arm that hangs behind the right shoulder blade, sunk 1.5 cm into the
        /// leather so the folded fingers go into it, in the wrap's middle, that arm lying 40 degrees round from the fingers so its bladed end trails outside the forearm, clear
        /// of the body. Worked out and printed by SourceArt/IronSpikedBoomerang/iron_spiked_hold.py (its IRON_HOLD), which also shows it in the moves' check
        /// renders: change it there.
        /// </summary>
        private static readonly Vector3 IronSpikedHoldPosition = new Vector3(0.9009f, -0.0680f, -0.5062f);

        private static readonly Quaternion IronSpikedHoldRotation = new Quaternion(0.0f, -0.30774f, 0.0f, 0.95147f);

        /// <summary>The points SourceArt/IronSpikedBoomerang/iron_spiked_boomerang_build.py writes beside the model, in the thrown frame.</summary>
        [System.Serializable]
        private sealed class IronSpikedPoints
        {
            public float[] grip, elbow, upper_end, lower_end;
            public float upper_half_width, lower_half_width;

            public static Vector3 V(float[] p) => new Vector3(p[0], p[1], p[2]);
        }

        /// <summary>
        /// The Iron Spiked Giant Boomerang (owner, 2026-11-19, with Meshy's "Twin Fang Crescent": "make this another giant boomerang weapon called "Iron
        /// Spiked Giant Boomerang" make it the roughly the same size as the other giant boomerang and shrink the y axis to roughly the same size"): a steel
        /// crescent with spiked blades at both ends and leather wraps, laid, sized and cut by SourceArt/IronSpikedBoomerang/iron_spiked_boomerang_build.py
        /// (its arms as long as the bone one's, 13 cm thick as it is, turned over so its spikes face away from the camera on the back, 6,000 triangles, one 1024 map). Held by the wrap on its upper arm; carried, drawn, thrown and
        /// caught as the bone one is.
        /// </summary>
        [MenuItem("CityX/Rebuild Iron Spiked Giant Boomerang")]
        public static GameObject BuildIronSpiked()
        {
            EnsureFolder();
            var pointsAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(IronSpikedPointsPath);
            if (pointsAsset == null || AssetDatabase.LoadAssetAtPath<GameObject>(IronSpikedSourcePath) == null)
            {
                Debug.LogError($"CityX: no {IronSpikedSourcePath} or its points -- run SourceArt/IronSpikedBoomerang/iron_spiked_boomerang_build.py.");
                return null;
            }
            var points = JsonUtility.FromJson<IronSpikedPoints>(pointsAsset.text);
            HeldModelBaker.Load(IronSpikedSourcePath, out Vector3[] vertices, out Vector3[] normals, out int[] triangles, out Vector2[] uv);
            Mesh mesh = HeldModelBaker.Save($"{Folder}/IronSpikedBoomerang.asset", "IronSpikedBoomerang", vertices, normals, triangles, uv);
            Material steel = HeldModelBaker.LitMaterial($"{Folder}/IronSpikedBoomerang.mat", Color.white, IronSpikedMetallic, IronSpikedSmoothness);
            steel.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(IronSpikedColourPath));
            EditorUtility.SetDirty(steel);

            float elbowHalf = Mathf.Max(points.upper_half_width, points.lower_half_width) * IronSpikedOutlineSpare;
            var shape = new GiantOutline(IronSpikedPoints.V(points.grip), IronSpikedPoints.V(points.elbow), IronSpikedPoints.V(points.lower_end),
                IronSpikedPoints.V(points.upper_end), elbowHalf, elbowHalf);
            Bounds bounds = mesh.bounds;
            var impact = AssetDatabase.LoadAssetAtPath<AudioClip>(IronSpikedImpactSoundPath);
            if (impact == null)
            {
                Debug.LogWarning($"CityX: no {IronSpikedImpactSoundPath} -- the Iron Spiked Giant Boomerang strikes with the bone one's made sounds.");
            }
            // On the back it lies flat as the bone one does, its tip-to-tip line along the bone one's line through its grip, turned over (its hollow and
            // spikes the other way from the bone one's); hung where the owner set it by hand in the editor (IronSpikedHangPosition).
            GiantOutline bone = BoneOutline();
            Vector3 boneNear = Carried(bone, bone.NearTip), boneFar = Carried(bone, bone.FarTip);
            Vector3 ironNear = Carried(shape, shape.NearTip), ironFar = Carried(shape, shape.FarTip);
            Quaternion layTurn = Quaternion.FromToRotation(ironFar - ironNear, boneFar - boneNear);
            Vector3 layShift = Carried(bone, Vector3.zero) - layTurn * Carried(shape, Vector3.zero);
            GameObject saved = SaveGiant("ThrownIronSpikedGiantBoomerang", ThrownIronSpikedPrefabPath, "IronSpikedGiantBoomerang", IronSpikedPrefabPath,
                GiantBoomerangModel.IronSpiked, shape, new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.6f, bounds.size.y * 2.0f, 2.4f),
                parent => HeldModelBaker.Part(parent, "Blade", mesh, steel), impact, layTurn, layShift, heldTurnedBack: true, fliesFaceDown: true);
            var item = saved.GetComponent<GiantBoomerangItem>();
            item.HasOwnHang = true;
            item.HangPosition = IronSpikedHangPosition;
            item.HangEuler = IronSpikedHangEuler;
            item.HasOwnHold = true;
            item.HoldPosition = IronSpikedHoldPosition;
            item.HoldEuler = IronSpikedHoldRotation.eulerAngles;
            PrefabUtility.SavePrefabAsset(saved);
            return saved;
        }

        /// <summary>A giant boomerang turned over in its thrown frame (its face down, its x the other way): about its z, which the build turns it over about.</summary>
        private static readonly Quaternion TurnedOver = Quaternion.AngleAxis(180.0f, Vector3.forward);

        private static GiantOutline TurnedBackOver(in GiantOutline shape)
        {
            Vector3 Over(Vector3 p) => TurnedOver * p;
            return new GiantOutline(Over(shape.Grip), Over(shape.Elbow), Over(shape.FarTip), Over(shape.NearTip), shape.HalfWidthAtElbow, shape.HalfWidthAtTip);
        }

        /// <summary>
        /// Its own sound on striking an enemy or anything else (owner, 2026-11-19, with GiantMetalBoomerangImpact.mp3: "make this the noise it plays when it
        /// lands on an enemy ... or terrain for now").
        /// </summary>
        public const string IronSpikedImpactSoundPath = "Assets/CityX/Weapons/Audio/iron_spiked_boomerang_impact.mp3";

        /// <summary>
        /// A giant boomerang's shape in its thrown frame (lying flat, its middle at the origin): where it is held, its elbow, the far tip (the other
        /// arm's) and the near one (the held arm's), and each arm's half width at the elbow and at its tip.
        /// </summary>
        private readonly struct GiantOutline
        {
            public readonly Vector3 Grip, Elbow, FarTip, NearTip;
            public readonly float HalfWidthAtElbow, HalfWidthAtTip;

            public GiantOutline(Vector3 grip, Vector3 elbow, Vector3 farTip, Vector3 nearTip, float halfWidthAtElbow, float halfWidthAtTip)
            {
                Grip = grip;
                Elbow = elbow;
                FarTip = farTip;
                NearTip = nearTip;
                HalfWidthAtElbow = halfWidthAtElbow;
                HalfWidthAtTip = halfWidthAtTip;
            }
        }

        /// <summary>
        /// A giant boomerang's two prefabs: the one in the air, and the one carried (its item) by its grip. With <paramref name="backTurn"/>, on the back its
        /// model lies turned by it and moved by <paramref name="backShift"/> in the carried frame, rather than where the hand holds it. With
        /// <paramref name="heldTurnedBack"/>, the hand holds it turned back over (as it was before the build turned it over for the back): the Iron Spiked one,
        /// whose curled near blade, turned over, swung into the head in the aim. With <paramref name="fliesFaceDown"/> it flies with its face down: the Iron
        /// Spiked one, held with the palm on the face that lay against the back, so that it leaves the hand, and comes back into it, the way up it is held.
        /// </summary>
        private static GameObject SaveGiant(string thrownName, string thrownPath, string heldName, string heldPath, GiantBoomerangModel kind, GiantOutline shape,
            Vector3 boxCentre, Vector3 boxSize, System.Action<Transform> addModel, AudioClip impactSound = null, Quaternion? backTurn = null,
            Vector3 backShift = default, bool heldTurnedBack = false, bool fliesFaceDown = false)
        {
            var thrown = new GameObject(thrownName);
            var held = new GameObject(heldName);
            try
            {
                var spinner = new GameObject("Spinner");
                spinner.transform.SetParent(thrown.transform, false);
                addModel(spinner.transform);
                if (fliesFaceDown)
                {
                    foreach (Transform part in spinner.transform)
                    {
                        part.localRotation = TurnedOver;
                    }
                }
                var flying = thrown.AddComponent<ThrownBoomerang>();
                flying.Spinner = spinner.transform;
                flying.Giant = true;
                flying.ImpactSound = impactSound;
                PrefabUtility.SaveAsPrefabAsset(thrown, thrownPath);

                // Carried by its grip: the model moved so the grip is the origin, its length from there toward the elbow along +z, its flat face +y.
                var model = new GameObject(GiantBoomerangItem.ModelName);
                model.transform.SetParent(held.transform, false);
                Quaternion turn = CarriedTurn(shape);
                Quaternion inTheHand = heldTurnedBack ? CarriedTurn(TurnedBackOver(shape)) * TurnedOver : turn;
                model.transform.localRotation = inTheHand;
                model.transform.localPosition = -(inTheHand * shape.Grip);
                addModel(model.transform);
                var box = held.AddComponent<BoxCollider>();
                box.center = boxCentre;
                box.size = boxSize;
                var item = held.AddComponent<GiantBoomerangItem>();
                item.Model = kind;
                Quaternion lies = backTurn ?? Quaternion.identity;
                Vector3 shift = backTurn.HasValue ? backShift : Vector3.zero;
                if (backTurn.HasValue)
                {
                    item.HasBackPose = true;
                    item.BackModelRotation = lies * turn;
                    item.BackModelPosition = lies * -(turn * shape.Grip) + shift;
                }
                // Its outline as it lies on the back, in the carried frame, for the hair under it (Items/BackClip): a V from tip to elbow to tip, as wide as its middle.
                Vector3 OnIt(Vector3 p) => lies * Carried(shape, p) + shift;
                Vector3 tipA = OnIt(shape.FarTip), elbow = OnIt(shape.Elbow), tipB = OnIt(shape.NearTip);
                item.OutlineTipAndElbow = new Vector4(tipA.x, tipA.z, elbow.x, elbow.z);
                item.OutlineTipAndWidths = new Vector4(tipB.x, tipB.z, shape.HalfWidthAtElbow, shape.HalfWidthAtTip);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(held, heldPath);
                AssetDatabase.SaveAssets();
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(thrown);
                Object.DestroyImmediate(held);
            }
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/CityX/Weapons", "Boomerang");
            }
        }

        private static void AddModel(Transform parent, Mesh body, Mesh tips, Material wood, Material paint)
        {
            HeldModelBaker.Part(parent, "Body", body, wood);
            HeldModelBaker.Part(parent, "Tips", tips, paint);
        }

        private static void AddGiantModel(Transform parent, Mesh body, Mesh wraps, Material bone, Material leather)
        {
            HeldModelBaker.Part(parent, "Blade", body, bone);
            HeldModelBaker.Part(parent, "Wraps", wraps, leather);
        }

        /// <summary>Where along the V, s from -1 (one tip) to 1 (the other): flat in XZ, the elbow rounded by a soft absolute value.</summary>
        private static Vector3 AlongTheV(in Shape shape, float s)
        {
            float softAbs = Mathf.Sqrt(s * s + shape.Roundness * shape.Roundness) - shape.Roundness;
            float open = shape.OpenDegrees * Mathf.Deg2Rad;
            return new Vector3(s * shape.Arm * Mathf.Sin(open), 0.0f, -softAbs * shape.Arm * Mathf.Cos(open));
        }

        /// <summary>The middle of the whole V: what it spins about.</summary>
        private static Vector3 MiddleOf(in Shape shape)
        {
            Vector3 middle = Vector3.zero;
            for (int i = 0; i <= Slices; i++)
            {
                middle += AlongTheV(shape, i * 2.0f / Slices - 1.0f);
            }
            return middle / (Slices + 1);
        }

        /// <summary>The V's stretches <paramref name="spans"/> (each from, to along it) lofted, <paramref name="proud"/> proud of its surface, about <paramref name="middle"/>.</summary>
        private static Mesh Loft(string path, string name, in Shape shape, Vector3 middle, int slices, Vector2[] spans, float proud)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (Vector2 span in spans)
            {
                AddLoft(vertices, triangles, shape, span.x, span.y, slices, middle, proud);
            }

            var worked = new Mesh();
            worked.SetVertices(vertices);
            worked.SetTriangles(triangles, 0);
            worked.RecalculateNormals();
            Vector3[] normals = worked.normals;
            Object.DestroyImmediate(worked);
            return HeldModelBaker.Save(path, name, vertices.ToArray(), normals, triangles.ToArray());
        }

        private static void AddLoft(List<Vector3> vertices, List<int> triangles, in Shape shape, float from, float to, int slices, Vector3 middle, float proud)
        {
            int sides = shape.Sides;
            int start = vertices.Count;
            for (int i = 0; i <= slices; i++)
            {
                float s = Mathf.Lerp(from, to, i / (float)slices);
                Vector3 centre = AlongTheV(shape, s) - middle;
                Vector3 ahead = AlongTheV(shape, Mathf.Min(1.0f, s + 0.01f)) - AlongTheV(shape, Mathf.Max(-1.0f, s - 0.01f));
                Vector3 across = Vector3.Cross(Vector3.up, ahead.normalized);

                // Rounded to a point over the last of each arm.
                float edge = Mathf.Abs(s);
                float round = Mathf.Sqrt(Mathf.Max(0.0f, 1.0f - Mathf.Pow(Mathf.InverseLerp(0.88f, 1.0f, edge), 2.0f)));
                float half = Mathf.Lerp(shape.WidthAtElbow, shape.WidthAtTip, edge) * 0.5f * Mathf.Max(0.05f, round) + proud;
                float high = shape.Thickness * 0.5f * Mathf.Max(0.05f, round) + proud;
                if (sides == 6)
                {
                    // The wooden one's six-sided slice, as it always was.
                    vertices.Add(centre - across * half);
                    vertices.Add(centre - across * (half * 0.5f) + Vector3.up * high);
                    vertices.Add(centre + across * (half * 0.5f) + Vector3.up * high);
                    vertices.Add(centre + across * half);
                    vertices.Add(centre + across * (half * 0.5f) - Vector3.up * high);
                    vertices.Add(centre - across * (half * 0.5f) - Vector3.up * high);
                }
                else
                {
                    // A rounder slice: an ellipse, the blade's edges thinned to an edge.
                    for (int k = 0; k < sides; k++)
                    {
                        float a = Mathf.PI - k * 2.0f * Mathf.PI / sides;
                        float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                        float thin = Mathf.Lerp(1.0f, 0.55f, Mathf.Abs(c));
                        vertices.Add(centre + across * (c * half) + Vector3.up * (sn * high * thin));
                    }
                }
            }
            for (int i = 0; i < slices; i++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int next = (side + 1) % sides;
                    int a = start + i * sides + side, b = start + i * sides + next;
                    int c = start + (i + 1) * sides + side, d = start + (i + 1) * sides + next;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }
            for (int side = 1; side < sides - 1; side++)
            {
                triangles.Add(start); triangles.Add(start + side); triangles.Add(start + side + 1);
                int end = start + slices * sides;
                triangles.Add(end); triangles.Add(end + side + 1); triangles.Add(end + side);
            }
        }
    }
}
