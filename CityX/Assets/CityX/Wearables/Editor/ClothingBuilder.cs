using CityX.Character;
using CityX.CharacterEditor;
using CityX.Wearables;
using UnityEditor;
using UnityEngine;

namespace CityX.WearablesEditor
{
    /// <summary>
    /// Builds the t-shirt and hoodie prefabs from wearables/t_shirt.gd's and
    /// hoodie.gd's own piece/bone structure -- torso and collar on the
    /// spine, sleeves on the arm bones, a hood on the neck for the hoodie.
    /// Exact radii/lengths are this port's own reasonable numbers (the
    /// Godot source's tuning constants matter less here than the
    /// piece/bone attachment structure itself, per the plan this was built
    /// against).
    ///
    /// EVERY ARM BONE LIES FLAT ALONG X IN THIS RIG'S T-POSE (see
    /// BodyRig.Bones() -- shoulder, elbow, wrist and fingertip all share
    /// the same Y), so "toward the next joint" is always exactly
    /// Vector3.left/right for every arm-bone piece -- no per-segment
    /// direction math needed.
    /// </summary>
    public static class ClothingBuilder
    {
        private const string TShirtPrefabPath = "Assets/CityX/Wearables/TShirt.prefab";
        private const string HoodiePrefabPath = "Assets/CityX/Wearables/Hoodie.prefab";

        private static readonly Color ShirtColour = new Color(0.86f, 0.86f, 0.88f);
        private static readonly Color HoodieColour = new Color(0.22f, 0.24f, 0.28f);

        [MenuItem("CityX/Rebuild T-Shirt")]
        public static GameObject BuildTShirt()
        {
            return Build(TShirtPrefabPath, "TShirt", ShirtColour, "ShirtFabric", hooded: false);
        }

        [MenuItem("CityX/Rebuild Hoodie")]
        public static GameObject BuildHoodie()
        {
            return Build(HoodiePrefabPath, "Hoodie", HoodieColour, "HoodieFabric", hooded: true);
        }

        private static GameObject Build(string prefabPath, string rootName, Color colour,
            string materialName, bool hooded)
        {
            Material fabric = CharacterSkin.Ensure(materialName, colour);

            var root = new GameObject(rootName);
            try
            {
                var item = hooded ? (ClothingItem)root.AddComponent<Hoodie>() : root.AddComponent<TShirt>();

                float torsoLength = (BodyRig.ShoulderY - BodyRig.HipY) + 0.15f;
                var torso = AddTube(root.transform, "Torso", fabric,
                    radius: 0.36f, length: torsoLength, localPosition: Vector3.zero, direction: Vector3.up);
                var collar = AddTube(root.transform, "Collar", fabric,
                    radius: 0.22f, length: 0.08f,
                    localPosition: new Vector3(0.0f, BodyRig.ShoulderY - Mathf.Lerp(BodyRig.HipY, BodyRig.ShoulderY, BodyRig.SpineT), 0.0f),
                    direction: Vector3.up);

                float upperSleeveLength = hooded
                    ? BodyRig.ArmLength * BodyRig.ElbowT
                    : BodyRig.ArmLength * 0.35f;
                var sleeveUpperL = AddTube(root.transform, "SleeveUpperL", fabric,
                    radius: hooded ? 0.15f : 0.17f, length: upperSleeveLength,
                    localPosition: Vector3.left * (upperSleeveLength * 0.5f), direction: Vector3.left);
                var sleeveUpperR = AddTube(root.transform, "SleeveUpperR", fabric,
                    radius: hooded ? 0.15f : 0.17f, length: upperSleeveLength,
                    localPosition: Vector3.right * (upperSleeveLength * 0.5f), direction: Vector3.right);

                var pieces = new System.Collections.Generic.List<ClothingItem.BonePiece>
                {
                    Piece(HumanBodyBones.Spine, torso),
                    Piece(HumanBodyBones.Spine, collar),
                    Piece(HumanBodyBones.LeftUpperArm, sleeveUpperL),
                    Piece(HumanBodyBones.RightUpperArm, sleeveUpperR),
                };

                if (hooded)
                {
                    float lowerSleeveLength = BodyRig.ArmLength * (0.78f - BodyRig.ElbowT);
                    var sleeveLowerL = AddTube(root.transform, "SleeveLowerL", fabric,
                        radius: 0.135f, length: lowerSleeveLength,
                        localPosition: Vector3.left * (lowerSleeveLength * 0.5f), direction: Vector3.left);
                    var sleeveLowerR = AddTube(root.transform, "SleeveLowerR", fabric,
                        radius: 0.135f, length: lowerSleeveLength,
                        localPosition: Vector3.right * (lowerSleeveLength * 0.5f), direction: Vector3.right);
                    var hood = AddSphere(root.transform, "Hood", fabric,
                        radius: 0.22f, localPosition: new Vector3(0.0f, 0.05f, -0.05f));

                    pieces.Add(Piece(HumanBodyBones.LeftLowerArm, sleeveLowerL));
                    pieces.Add(Piece(HumanBodyBones.RightLowerArm, sleeveLowerR));
                    pieces.Add(Piece(HumanBodyBones.Neck, hood));
                }

                item.Pieces = pieces.ToArray();

                // A small ground-lying pickup trigger, matching the bottle's own shape.
                var pickupTrigger = root.AddComponent<SphereCollider>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = ClothingItem.InteractRange;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: {rootName} written to {prefabPath}");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static ClothingItem.BonePiece Piece(HumanBodyBones bone, Transform piece) =>
            new ClothingItem.BonePiece
            {
                Bone = bone,
                Piece = piece,
                LocalPosition = piece.localPosition,
                LocalEulerAngles = piece.localEulerAngles,
            };

        /// <summary>
        /// A cylinder pointing along <paramref name="direction"/>, parented
        /// loose under <paramref name="parent"/> for now -- ClothingItem's
        /// own OnEquip is what reparents it onto the actual body bone at
        /// equip time.
        /// </summary>
        private static Transform AddTube(Transform parent, string name, Material material,
            float radius, float length, Vector3 localPosition, Vector3 direction)
        {
            GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = name;
            Object.DestroyImmediate(tube.GetComponent<Collider>());
            tube.transform.SetParent(parent, false);
            tube.transform.localPosition = localPosition;
            tube.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            // The primitive cylinder is 2 units tall at scale 1, radius 0.5.
            tube.transform.localScale = new Vector3(radius * 2.0f, length * 0.5f, radius * 2.0f);
            tube.GetComponent<MeshRenderer>().sharedMaterial = material;
            return tube.transform;
        }

        private static Transform AddSphere(Transform parent, string name, Material material,
            float radius, Vector3 localPosition)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = localPosition;
            sphere.transform.localScale = Vector3.one * (radius * 2.0f);
            sphere.GetComponent<MeshRenderer>().sharedMaterial = material;
            return sphere.transform;
        }
    }
}
