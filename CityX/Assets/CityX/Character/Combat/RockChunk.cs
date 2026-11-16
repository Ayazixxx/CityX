using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// A rough chunk: an octahedron pulled out of true, so tumbling it reads as rock, not a die. Made once; a grenade's debris and a fireball's
    /// glowing cinders are drawn with it (mesh particles).
    /// </summary>
    public static class RockChunk
    {
        private static Mesh _mesh;

        public static Mesh Mesh
        {
            get
            {
                if (_mesh != null)
                {
                    return _mesh;
                }
                _mesh = new Mesh { name = "RockChunk", hideFlags = HideFlags.DontSave };
                _mesh.SetVertices(new[]
                {
                    new Vector3(0.0f, 0.55f, 0.0f), new Vector3(0.0f, -0.45f, 0.08f), new Vector3(0.5f, 0.05f, 0.0f), new Vector3(-0.42f, -0.06f, 0.1f),
                    new Vector3(0.05f, 0.0f, 0.48f), new Vector3(-0.08f, 0.08f, -0.52f),
                });
                _mesh.SetTriangles(new[] { 0, 4, 2, 0, 2, 5, 0, 5, 3, 0, 3, 4, 1, 2, 4, 1, 5, 2, 1, 3, 5, 1, 4, 3 }, 0);
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();
                return _mesh;
            }
        }
    }
}
