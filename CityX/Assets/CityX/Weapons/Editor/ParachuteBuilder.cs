using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the parachute prefab -- a simple folded pack on the back and a
    /// canopy that appears above the head while deployed, both scaled Unity
    /// primitives rather than a hand-authored mesh, matching JetpackBuilder's
    /// own established reasoning (a hand-copied mesh produced zero-bounds
    /// renderers here before; scaling a builtin primitive's own transform
    /// sidesteps that entirely). "Pretty basic" was the explicit ask for the
    /// mechanic itself, and the visual matches that scope on purpose.
    /// </summary>
    public static class ParachuteBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/Parachute.prefab";

        private static readonly Color PackColour = new Color(0.30f, 0.32f, 0.22f);
        private static readonly Color CanopyColour = new Color(0.82f, 0.32f, 0.08f);

        private const float PackWidth = 0.30f;
        private const float PackHeight = 0.38f;
        private const float PackDepth = 0.14f;

        /// <summary>Wide and flat -- a dome, not a ball, and high enough above the head to read as a real deployed canopy rather than a hat.</summary>
        private static readonly Vector3 CanopyScale = new Vector3(2.4f, 0.6f, 2.4f);

        private static readonly Vector3 CanopyLocalPosition = new Vector3(0.0f, 1.8f, 0.0f);

        [MenuItem("CityX/Rebuild Parachute")]
        public static GameObject Build()
        {
            var root = new GameObject("Parachute");
            try
            {
                Material packMaterial = CharacterSkin.Ensure("ParachutePack", PackColour);
                packMaterial.SetFloat("_Metallic", 0.0f);
                packMaterial.SetFloat("_Smoothness", 0.15f);

                var pack = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pack.name = "Pack";
                Object.DestroyImmediate(pack.GetComponent<Collider>());
                pack.transform.SetParent(root.transform, false);
                pack.transform.localScale = new Vector3(PackWidth, PackHeight, PackDepth);
                pack.GetComponent<MeshRenderer>().sharedMaterial = packMaterial;

                Material canopyMaterial = CharacterSkin.Ensure("ParachuteCanopy", CanopyColour);
                canopyMaterial.SetFloat("_Metallic", 0.0f);
                canopyMaterial.SetFloat("_Smoothness", 0.05f);

                var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.name = "Canopy";
                Object.DestroyImmediate(canopy.GetComponent<Collider>());
                canopy.transform.SetParent(root.transform, false);
                canopy.transform.localPosition = CanopyLocalPosition;
                canopy.transform.localScale = CanopyScale;
                canopy.GetComponent<MeshRenderer>().sharedMaterial = canopyMaterial;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = Parachute.InteractRange;

                var parachute = root.AddComponent<Parachute>();
                parachute.PackVisual = pack;
                parachute.CanopyVisual = canopy;
                // Off by default -- Parachute.Awake also enforces this at
                // runtime, but starting the prefab asset itself already
                // closed means opening the Editor never shows a canopy
                // floating over nothing.
                canopy.SetActive(false);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: parachute written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
