using CityX.CharacterEditor;
using CityX.Weapons;
using UnityEditor;
using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// Builds the grappling hook prefab, from items/grappling_hook.gd's own
    /// numbers -- body + barrel + muzzle marker, all procedural (matching
    /// BottleBuilder's own reasoning: no glTF importer exists in this
    /// project, so nothing here waits on one).
    ///
    /// THE ROPE/HOOK-HEAD MATERIALS ARE BUILT HERE AND WIRED AS REFERENCES,
    /// not created at runtime -- GrapplingHookItem.ShowRope only ever
    /// APPLIES RopeMaterial/HookHeadMaterial to a fresh rope tube mesh/cube
    /// it builds while playing, matching BottleWeapon's own "materials
    /// built at edit time by the builder" convention.
    /// </summary>
    public static class GrapplingHookBuilder
    {
        private const string PrefabPath = "Assets/CityX/Weapons/GrapplingHook.prefab";

        [MenuItem("CityX/Rebuild Grappling Hook")]
        public static GameObject Build()
        {
            var root = new GameObject("GrapplingHook");
            try
            {
                Material bodyMaterial = CharacterSkin.Ensure("GrappleBody", GrapplingHookItem.BodyColour);
                bodyMaterial.SetFloat("_Metallic", 0.4f);
                bodyMaterial.SetFloat("_Smoothness", 0.45f);

                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                var bodyFilter = body.AddComponent<MeshFilter>();
                bodyFilter.sharedMesh = BoxMesh(new Vector3(
                    GrapplingHookItem.BodyRadius * 2.0f,
                    GrapplingHookItem.BodyRadius * 2.4f,
                    GrapplingHookItem.BodyLength));
                body.AddComponent<MeshRenderer>().sharedMaterial = bodyMaterial;

                Material barrelMaterial = CharacterSkin.Ensure("GrappleBarrel", GrapplingHookItem.BarrelColour);
                barrelMaterial.SetFloat("_Metallic", 0.8f);
                barrelMaterial.SetFloat("_Smoothness", 0.7f);

                var barrel = new GameObject("Barrel");
                barrel.transform.SetParent(root.transform, false);
                // Laid along -Z (the aiming direction) rather than the
                // cylinder's own +Y -- matching the Godot original's own
                // barrel.rotation.
                barrel.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
                barrel.transform.localPosition = new Vector3(0.0f, 0.0f,
                    -(GrapplingHookItem.BodyLength * 0.5f + GrapplingHookItem.BarrelLength * 0.5f));
                var barrelFilter = barrel.AddComponent<MeshFilter>();
                barrelFilter.sharedMesh = CylinderMesh(
                    GrapplingHookItem.BarrelRadius, GrapplingHookItem.BarrelLength);
                barrel.AddComponent<MeshRenderer>().sharedMaterial = barrelMaterial;

                // Where the rope is drawn FROM -- a plain marker, no
                // geometry, so its own origin tracks the barrel tip through
                // whatever animation the hand happens to be playing.
                var muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(root.transform, false);
                muzzle.transform.localPosition = new Vector3(0.0f, 0.0f,
                    -(GrapplingHookItem.BodyLength * 0.5f + GrapplingHookItem.BarrelLength));

                var collision = root.AddComponent<CapsuleCollider>();
                collision.radius = GrapplingHookItem.BodyRadius;
                collision.height = GrapplingHookItem.BodyLength + GrapplingHookItem.BarrelLength;
                collision.direction = 2; // Z axis, matching the barrel's own aim axis.

                var rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.mass = GrapplingHookItem.HookMass;

                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = GrapplingHookItem.InteractRange;

                var item = root.AddComponent<GrapplingHookItem>();
                item.Muzzle = muzzle.transform;
                item.RopeMaterial = CharacterSkin.Ensure("GrappleRope", GrapplingHookItem.RopeColour);
                item.RopeMaterial.SetFloat("_Smoothness", 0.1f);
                item.HookHeadMaterial = CharacterSkin.Ensure("GrappleHookHead", GrapplingHookItem.HookColour);
                item.HookHeadMaterial.SetFloat("_Metallic", 0.8f);
                item.HookHeadMaterial.SetFloat("_Smoothness", 0.65f);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: grappling hook written to {PrefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Mesh BoxMesh(Vector3 size)
        {
            var mesh = new Mesh { name = "GrappleBodyMesh" };
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh source = box.GetComponent<MeshFilter>().sharedMesh;
            mesh.vertices = ScaleVertices(source.vertices, size);
            mesh.normals = source.normals;
            mesh.uv = source.uv;
            mesh.triangles = source.triangles;
            mesh.RecalculateBounds();
            Object.DestroyImmediate(box);
            return mesh;
        }

        private static Vector3[] ScaleVertices(Vector3[] vertices, Vector3 size)
        {
            var scaled = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                scaled[i] = Vector3.Scale(vertices[i], size);
            }
            return scaled;
        }

        private static Mesh CylinderMesh(float radius, float height)
        {
            var mesh = new Mesh { name = "GrappleBarrelMesh" };
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh source = cylinder.GetComponent<MeshFilter>().sharedMesh;
            // Unity's primitive cylinder is 2 units tall with radius 1.
            mesh.vertices = ScaleVertices(source.vertices, new Vector3(radius * 2.0f, height * 0.5f, radius * 2.0f));
            mesh.normals = source.normals;
            mesh.uv = source.uv;
            mesh.triangles = source.triangles;
            mesh.RecalculateBounds();
            Object.DestroyImmediate(cylinder);
            return mesh;
        }
    }
}
