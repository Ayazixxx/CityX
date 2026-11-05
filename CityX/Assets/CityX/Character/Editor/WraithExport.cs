using System.Globalization;
using System.IO;
using System.Text;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// The Wraith as the game builds it, written out for mending by hand (owner, 2026-11-05: "the lines around the arms and neck are a bit
    /// jagged ... you can tell its two models stitched together"). The model in the project has no arms: the player's are put on it by
    /// <see cref="WraithBuilder"/>, so the joins exist only in what that builds. This writes that, body and teeth, as an OBJ Blender opens.
    /// </summary>
    public static class WraithExport
    {
        /// <summary>Where it is written, under the Unity project's folder.</summary>
        public const string OutPath = "SourceArt/Wraith/WraithAsBuilt.obj";

        [MenuItem("CityX/Export Wraith For Mending")]
        public static void Export()
        {
            var body = AssetDatabase.LoadAssetAtPath<Mesh>(WraithBuilder.WraithBodyPath);
            var teeth = AssetDatabase.LoadAssetAtPath<Mesh>(WraithBuilder.TeethMeshPath);
            if (body == null || teeth == null)
            {
                Debug.LogError("CityX: no Wraith to export -- build it first (CityX > Rebuild Wraith).");
                return;
            }
            var text = new StringBuilder();
            text.Append("# The Wraith as the game builds it. Metres, Y up, standing at the origin (Blender's OBJ import with its usual axes).\n");
            text.Append("# Body: the model's head, torso and tail with the player's arms. Teeth: where they hang, for reference.\n");
            int written = Write(text, "Body", body, Vector3.zero, 0);
            Write(text, "Teeth", teeth, new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f) + WraithBuilder.JawAt, written);
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutPath);
            File.WriteAllText(path, text.ToString());
            Debug.Log($"CityX: the Wraith as built ({body.vertexCount} + {teeth.vertexCount} vertices) written to {path}");
        }

        /// <summary>One mesh as an OBJ object, moved by <paramref name="offset"/>; returns the count of vertices written so far.</summary>
        private static int Write(StringBuilder text, string name, Mesh mesh, Vector3 offset, int before)
        {
            CultureInfo plain = CultureInfo.InvariantCulture;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;
            text.Append("o ").Append(name).Append('\n');
            // Unity's frame is left-handed and an OBJ's right-handed: X is turned over, and each triangle's winding with it.
            foreach (Vector3 v in vertices)
            {
                Vector3 p = v + offset;
                text.Append("v ").Append((-p.x).ToString("0.######", plain)).Append(' ').Append(p.y.ToString("0.######", plain)).Append(' ')
                    .Append(p.z.ToString("0.######", plain)).Append('\n');
            }
            foreach (Vector3 n in normals)
            {
                text.Append("vn ").Append((-n.x).ToString("0.####", plain)).Append(' ').Append(n.y.ToString("0.####", plain)).Append(' ')
                    .Append(n.z.ToString("0.####", plain)).Append('\n');
            }
            bool hasNormals = normals.Length == vertices.Length;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                text.Append('f');
                foreach (int corner in new[] { triangles[i], triangles[i + 2], triangles[i + 1] })
                {
                    int index = before + corner + 1;
                    text.Append(' ').Append(index);
                    if (hasNormals)
                    {
                        text.Append("//").Append(index);
                    }
                }
                text.Append('\n');
            }
            return before + vertices.Length;
        }
    }
}
