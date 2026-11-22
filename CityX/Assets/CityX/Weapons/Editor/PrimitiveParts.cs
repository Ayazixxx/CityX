using UnityEngine;

namespace CityX.WeaponsEditor
{
    /// <summary>
    /// A stand-in model's parts from Unity's primitives (a box, a tube, a ball), each placed under a parent with its material and no collider of
    /// its own: what the code-drawn stand-ins (the gas can, the ammunition) are made of.
    /// </summary>
    public static class PrimitiveParts
    {
        public static Transform Box(Transform parent, string name, Vector3 centre, Quaternion rotation, Vector3 size, Material material) =>
            Place(GameObject.CreatePrimitive(PrimitiveType.Cube), parent, name, centre, rotation, size, material);

        /// <summary>Unity's primitive cylinder is 2 units tall with radius 1, along Y.</summary>
        public static Transform Cylinder(Transform parent, string name, Vector3 centre, Quaternion rotation, float radius, float length, Material material) =>
            Place(GameObject.CreatePrimitive(PrimitiveType.Cylinder), parent, name, centre, rotation, new Vector3(radius * 2.0f, length * 0.5f, radius * 2.0f), material);

        public static Transform Ball(Transform parent, string name, Vector3 centre, float radius, Material material) =>
            Place(GameObject.CreatePrimitive(PrimitiveType.Sphere), parent, name, centre, Quaternion.identity, Vector3.one * radius * 2.0f, material);

        private static Transform Place(GameObject primitive, Transform parent, string name, Vector3 centre, Quaternion rotation, Vector3 scale, Material material)
        {
            primitive.name = name;
            Object.DestroyImmediate(primitive.GetComponent<Collider>());
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = centre;
            primitive.transform.localRotation = rotation;
            primitive.transform.localScale = scale;
            primitive.GetComponent<MeshRenderer>().sharedMaterial = material;
            return primitive.transform;
        }
    }
}
