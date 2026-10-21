using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Exports the real player body shape, at fused_body.gd's own "arms
    /// down" rest, to a plain .obj file -- Requested, after the black
    /// puffer jacket's geometry-only auto-rig didn't fit well enough by
    /// uniform scale/offset alone: "give me the file for the standard
    /// playermodel so i can shape the outfit to him in blender."
    ///
    /// REGENERATED FROM THE SAME PIPELINE THE REAL BODY MESH USES
    /// (FusedBodyShape.LimbCapsules + FusedBodyMesh.Extract), NOT BAKED
    /// FROM A POSED SkinnedMeshRenderer -- the first version instantiated
    /// Player.prefab and bent arm_l/forearm_l by hand-tuned Euler angles
    /// (78/12 degrees) to approximate "arms down" from the live rig's own
    /// T-pose bind (BodyRig.Bones() -- see its own doc comment for why the
    /// LIVE, Animator-facing rig has to be a T-pose). That was two removed
    /// steps away from correct: bone rotation only approximates the
    /// intended straight-line diagonal fused_body.gd actually draws, and
    /// bending a rigid SkinnedMeshRenderer through linear blend skinning
    /// at a large swing angle visibly pinched the shoulder -- reported
    /// directly, comparing this export against the Godot original's own:
    /// "i also like how the godot one doesn't have those armpit gaps," on
    /// top of "the arms a longer in the godot version. i said from the
    /// beginning to create the same sized model for unity." Measured: the
    /// old export's arms-down horizontal reach was 1.103m against Godot's
    /// 1.336m, a ~21% shortfall entirely from that approximation -- NOT
    /// from FusedBodyShape/BodyRig, which were already exactly Godot's own
    /// numbers (confirmed directly: BodyRigBuilder.Segments/LimbCapsules
    /// already read BodyRig.HipY/ShoulderY/ShoulderX/HandX/HandY, which
    /// are fused_body.gd's own constants, unscaled).
    ///
    /// BodyRig.RestBones() is the exact fix: "the arm-and-torso-relevant
    /// project bones at fused_body.gd's OWN rest," already used to bind
    /// every existing garment (see its own doc comment) for exactly this
    /// reason. Re-running FusedBodyMesh.Extract at THAT pose instead of
    /// the T-pose reproduces fused_body.gd's own straight shoulder-to-hand
    /// diagonal with a fresh SDF blend at the shoulder -- the same shape
    /// Godot's own marching-tetrahedra extraction produces, with no LBS
    /// deformation (and so no LBS pinching) involved at all.
    ///
    /// PLAIN OBJ, NOT FBX/GLTF -- no bones, no material, just position/
    /// normal geometry. Blender only needs this as a visual fitting guide;
    /// the jacket itself keeps going through the normal GLB pipeline
    /// afterward.
    ///
    /// INCLUDES THE HEAD, which the first version did not (it only baked
    /// the "Skin" SkinnedMeshRenderer -- the head is a separate, non-
    /// skinned mesh, see HeadMesh.cs) -- reported directly: "the head is
    /// missing from both of the files. having the head... in the default
    /// resting spot would be useful for fitting clothes." BodyRig.
    /// HeadCentreY already gives its exact rest position; HeadMesh.Build()
    /// is centred on the origin, so placing it needs nothing more than
    /// that one offset.
    /// </summary>
    public static class PlayerBodyReferenceExporter
    {
        private const string OutputPath = "Logs/PlayerBodyReference.obj";

        [MenuItem("CityX/Export Player Body Reference (OBJ)")]
        public static void Export()
        {
            // THE SAVED BODY ITSELF, not a fresh extraction: since 2026-10-13 the shipped mesh has its hands extracted on a finer grid
            // and stitched on (FusedBodyMesh.ExtractBody), so re-extracting here would give a reference with mitten hands. The asset's
            // vertices are in the arms-down rest pose already (that is what the bind pose maps from).
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/FusedBody.asset");
            if (saved == null)
            {
                Debug.LogError("CityX: no FusedBody.asset -- rebuild the player rig first.");
                return;
            }
            var bodyVertices = new List<Vector3>(saved.vertices);
            var bodyTriangles = new List<int>(saved.triangles);
            Vector3[] bodyNormals = saved.normals;

            var body = new Mesh { name = "PlayerBodyReferenceBody", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            body.SetVertices(bodyVertices);
            body.SetTriangles(bodyTriangles, 0);
            body.SetNormals(bodyNormals);

            // The head bone sits exactly at its sphere's own centre (see
            // HeadMesh.CentreOnBone's own doc comment), so placing the
            // origin-centred head mesh needs nothing more than this offset.
            Mesh headMesh = HeadMesh.Build();
            var headOffset = new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f);
            Vector3[] headVerticesRaw = headMesh.vertices;
            var headVertices = new Vector3[headVerticesRaw.Length];
            for (int i = 0; i < headVerticesRaw.Length; i++)
            {
                headVertices[i] = headVerticesRaw[i] + headOffset;
            }

            Mesh combined = Combine(body, headVertices, headMesh.normals, headMesh.triangles);

            string fullPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, ToObj(combined));

            Debug.Log($"CityX: wrote player body reference to {fullPath} " +
                      $"({combined.vertexCount} vertices: {bodyVertices.Count} body + " +
                      $"{headVertices.Length} head, arms-down rest pose).");

            // A quick unlit render, same reasoning GodotBodyImporter's own
            // RenderVerificationShot gives: a vertex count and a bounding
            // box are not proof anything actually looks right, and this
            // tool's whole job is producing something a person will look
            // at in Blender.
            string screenshotPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, "Logs/CityShots/player-body-reference-verify.png");
            Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath));
            RenderVerificationShot(combined, screenshotPath);

            Object.DestroyImmediate(body);
            Object.DestroyImmediate(headMesh);
            Object.DestroyImmediate(combined);
        }

        private static void RenderVerificationShot(Mesh mesh, string path)
        {
            var root = new GameObject("PlayerBodyVerifyRig");
            RenderTexture target = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                var meshObject = new GameObject("Body");
                meshObject.transform.SetParent(root.transform, false);
                meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    color = new Color(0.85f, 0.55f, 0.35f),
                };
                meshObject.AddComponent<MeshRenderer>().sharedMaterial = material;

                var sunObject = new GameObject("Sun");
                sunObject.transform.SetParent(root.transform, false);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(30.0f, 150.0f, 0.0f);

                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(root.transform, false);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.10f, 0.11f, 0.15f);
                camera.fieldOfView = 32.0f;
                camera.nearClipPlane = 0.05f;
                cameraObject.transform.position = new Vector3(0.6f, 1.2f, -4.5f);
                cameraObject.transform.LookAt(new Vector3(0.0f, 1.1f, 0.0f));

                const int width = 700;
                const int height = 900;
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Debug.Log($"CityX: wrote verification screenshot to {path}");
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (target != null)
                {
                    target.Release();
                    Object.DestroyImmediate(target);
                }
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Appends a second mesh's already-positioned vertices/normals/triangles onto a copy of the first.</summary>
        private static Mesh Combine(Mesh body, Vector3[] extraVertices, Vector3[] extraNormals, int[] extraTriangles)
        {
            Vector3[] bodyVerts = body.vertices;
            Vector3[] bodyNormals = body.normals;
            int[] bodyTris = body.triangles;

            var combinedVerts = new Vector3[bodyVerts.Length + extraVertices.Length];
            var combinedNormals = new Vector3[bodyNormals.Length + extraNormals.Length];
            bodyVerts.CopyTo(combinedVerts, 0);
            bodyNormals.CopyTo(combinedNormals, 0);
            extraVertices.CopyTo(combinedVerts, bodyVerts.Length);
            extraNormals.CopyTo(combinedNormals, bodyNormals.Length);

            var combinedTris = new int[bodyTris.Length + extraTriangles.Length];
            bodyTris.CopyTo(combinedTris, 0);
            for (int i = 0; i < extraTriangles.Length; i++)
            {
                combinedTris[bodyTris.Length + i] = extraTriangles[i] + bodyVerts.Length;
            }

            var mesh = new Mesh
            {
                indexFormat = combinedVerts.Length > 65535
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.vertices = combinedVerts;
            mesh.normals = combinedNormals;
            mesh.triangles = combinedTris;
            return mesh;
        }

        /// <summary>Minimal ASCII .obj writer -- position/normal and triangle faces, nothing else.</summary>
        private static string ToObj(Mesh mesh)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# CityX player body reference -- arms-down rest pose");

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;

            foreach (Vector3 v in vertices)
            {
                // Unity is left-handed, OBJ convention is right-handed --
                // flipping X here is the same correction every Unity->OBJ
                // exporter needs, or the mesh imports mirrored.
                sb.AppendLine(FormattableLine("v", -v.x, v.y, v.z));
            }
            bool hasNormals = normals != null && normals.Length == vertices.Length;
            if (hasNormals)
            {
                foreach (Vector3 n in normals)
                {
                    sb.AppendLine(FormattableLine("vn", -n.x, n.y, n.z));
                }
            }

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] triangles = mesh.GetTriangles(sub);
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    // OBJ indices are 1-based. The X-flip above mirrors
                    // winding too, so corners b/c are swapped to keep faces
                    // pointing the right way.
                    int a = triangles[t] + 1;
                    int b = triangles[t + 1] + 1;
                    int c = triangles[t + 2] + 1;
                    sb.AppendLine($"f {FaceCorner(a, hasNormals)} " +
                                  $"{FaceCorner(c, hasNormals)} {FaceCorner(b, hasNormals)}");
                }
            }

            return sb.ToString();
        }

        private static string FaceCorner(int index, bool hasNormals)
        {
            return hasNormals
                ? $"{index}//{index}"
                : index.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormattableLine(string tag, float x, float y, float z)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1:F6} {2:F6} {3:F6}", tag, x, y, z);
        }
    }
}
