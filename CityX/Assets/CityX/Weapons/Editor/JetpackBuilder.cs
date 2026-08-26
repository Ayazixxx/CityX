using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the jetpack prefab -- two tanks on a backplate, a downward
    /// flame ParticleSystem, all procedural (matching BottleBuilder/
    /// GrapplingHookBuilder's own reasoning: no glTF importer exists in
    /// this project for a hand-authored jetpack mesh, so nothing here waits
    /// on one). No Godot source to port numbers from -- see Jetpack.cs's
    /// own doc comment.
    /// </summary>
    public static class JetpackBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/Jetpack.prefab";

        private static readonly Color TankColour = new Color(0.55f, 0.56f, 0.58f);
        private static readonly Color PlateColour = new Color(0.12f, 0.12f, 0.13f);
        private static readonly Color FlameColour = new Color(1.0f, 0.55f, 0.1f);

        private const float TankRadius = 0.09f;
        private const float TankHeight = 0.42f;
        private const float TankSpacing = 0.13f;
        private const float PlateWidth = 0.30f;
        private const float PlateHeight = 0.40f;
        private const float PlateThickness = 0.05f;

        [MenuItem("CityX/Rebuild Jetpack")]
        public static GameObject Build()
        {
            var root = new GameObject("Jetpack");
            try
            {
                Material plateMaterial = CharacterSkin.Ensure("JetpackPlate", PlateColour);
                plateMaterial.SetFloat("_Metallic", 0.6f);
                plateMaterial.SetFloat("_Smoothness", 0.35f);

                // A scaled Unity primitive, not a hand-copied mesh --
                // GrapplingHookBuilder/BottleBuilder's own BoxMesh/
                // CylinderMesh technique (new Mesh + copy the primitive's
                // vertices/triangles by hand) produced renderers with
                // exactly zero bounds here, confirmed directly by dumping
                // MeshRenderer.bounds after the build -- the jetpack was
                // parented correctly (worldPos matched expectations) but
                // rendered nothing, on every part, every time. Scaling the
                // primitive's own transform instead sidesteps whatever that
                // was: Unity's builtin cube/cylinder meshes are known-good,
                // so there is nothing left to copy wrong.
                var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plate.name = "Backplate";
                Object.DestroyImmediate(plate.GetComponent<Collider>());
                plate.transform.SetParent(root.transform, false);
                plate.transform.localScale = new Vector3(PlateWidth, PlateHeight, PlateThickness);
                plate.GetComponent<MeshRenderer>().sharedMaterial = plateMaterial;

                Material tankMaterial = CharacterSkin.Ensure("JetpackTank", TankColour);
                tankMaterial.SetFloat("_Metallic", 0.85f);
                tankMaterial.SetFloat("_Smoothness", 0.55f);

                Transform leftTank = BuildTank(root.transform, tankMaterial, -TankSpacing);
                Transform rightTank = BuildTank(root.transform, tankMaterial, TankSpacing);

                // Where the flame comes from -- the bottom of the tanks,
                // pointed straight down, matching the thrust direction
                // Jetpack.Tick actually applies (pure vertical velocity).
                var flameObject = new GameObject("Flame");
                flameObject.transform.SetParent(root.transform, false);
                flameObject.transform.localPosition = new Vector3(0.0f, -TankHeight * 0.5f - 0.02f, PlateThickness * 0.5f);
                flameObject.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
                ParticleSystem flame = BuildFlame(flameObject);

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = Jetpack.InteractRange;

                var jetpack = root.AddComponent<Jetpack>();
                jetpack.FlameEffect = flame;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: jetpack written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Transform BuildTank(Transform parent, Material material, float xOffset)
        {
            GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.name = xOffset < 0.0f ? "TankLeft" : "TankRight";
            Object.DestroyImmediate(tank.GetComponent<Collider>());
            tank.transform.SetParent(parent, false);
            tank.transform.localPosition = new Vector3(xOffset, 0.0f, 0.0f);
            tank.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
            // Unity's primitive cylinder is 2 units tall with radius 1.
            tank.transform.localScale = new Vector3(TankRadius * 2.0f, TankHeight * 0.5f, TankRadius * 2.0f);
            tank.GetComponent<MeshRenderer>().sharedMaterial = material;
            return tank.transform;
        }

        /// <summary>
        /// A small, cheap cone of orange particles -- Jetpack.Tick starts/
        /// stops this by calling Play()/Stop() directly, so looping is what
        /// keeps it going for as long as thrust is held rather than firing
        /// once per call.
        /// </summary>
        private static ParticleSystem BuildFlame(GameObject flameObject)
        {
            ParticleSystem particles = flameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.25f;
            main.startSpeed = 3.0f;
            main.startSize = 0.06f;
            main.startColor = FlameColour;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 60.0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12.0f;
            shape.radius = TankRadius * 0.6f;

            var renderer = flameObject.GetComponent<ParticleSystemRenderer>();
            renderer.material = CharacterSkin.Ensure("JetpackFlameParticle", FlameColour);
            renderer.sharedMaterial.SetFloat("_Surface", 1.0f); // transparent, matching an emissive flame rather than a lit solid
            renderer.sharedMaterial.SetFloat("_Metallic", 0.0f);
            renderer.sharedMaterial.SetFloat("_Smoothness", 0.0f);

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }
    }
}
