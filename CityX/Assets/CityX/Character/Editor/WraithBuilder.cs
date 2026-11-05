using System.Collections.Generic;
using CityX.Character;
using UnityEditor;
using UnityEngine;

namespace CityX.CharacterEditor
{
    /// <summary>
    /// Builds Wraith.prefab. Owner, 2026-11-05, of their model: "make the head replaced with this model's head, I also like the body of the model attached better but want it to whisp away
    /// into nothing towards the bottom of it", and "the model's arms are all fucked up ... use the arms you have now that are the same as the playermodel":
    ///
    ///  * THE BODY is the owner's model (SourceArt/WraithBody: its Meshy arms taken off, stood in the body's frame with its head the size and place of the player's), joined to THE ARMS
    ///    of the player's own fused body at the shoulders. All one mesh skinned to the player's skeleton (BodyRigBuilder.BuildSkeleton) put into the arms-down rest the body is bound in
    ///    (BodyRig.RestBindMatrix), so the Wraith can pose it without an Animator.
    ///  * THE GAS, three things: the tail's vertex alpha falls from <see cref="TailSolidY"/> to nothing at <see cref="TailGoneY"/>, above its tip, and the ghost shader wears the surface
    ///    away into streaks where it does; the tail writhes (the shader's sway); and soft puffs come off it, drift and thin to nothing.
    ///  * THE FACE: the model's teeth, cut off it as their own solid white mesh under <see cref="JawName"/>, and two solid red eyes in the model's sockets.
    ///  * THE SURFACE is Shaders/CityXGhost. A point light glows from its chest, and a capsule collider stands in for its torso.
    ///
    /// Spawned only by /spawn wraith.
    /// </summary>
    public static class WraithBuilder
    {
        public const string PrefabPath = "Assets/CityX/Character/Wraith.prefab";
        private const string BodyMeshPath = "Assets/CityX/Character/FusedBody.asset";
        public const string ModelPath = "Assets/CityX/Character/WraithModel/PhantomMawBody.glb";
        public const string WraithBodyPath = "Assets/CityX/Character/WraithBody.asset";
        private const string GhostMaterialPath = "Assets/CityX/Character/Materials/WraithGhost.mat";
        private const string GasMaterialPath = "Assets/CityX/Character/Materials/WraithGas.mat";
        private const string SmokeMaterialPath = "Assets/CityX/Weapons/FlamethrowerSmoke.mat";
        public const string ShaderName = "CityX/Ghost";

        /// <summary>The gas's object (under the hips bone) and the glow's.</summary>
        public const string GasName = "Gas";
        public const string GlowName = "Glow";

        // THE FACE, in the head's frame (its centre, facing +z): the parts hang under one "Face" object on the head bone, so they shrink away together when it dies.
        public const string FaceName = "Face";
        public const string EyeLeftName = "EyeLeft";
        public const string EyeRightName = "EyeRight";
        public const string JawName = "Jaw";
        public const string TeethName = "Teeth";

        /// <summary>The eyes, in the middle of the model's sockets (measured by wraith_body.py), filling them. "eyes dont need to glow just make them solid red" (owner, 2026-10-26).</summary>
        public const float EyeOffsetX = 0.22f;
        public const float EyeY = 0.067f;
        public const float EyeZ = 0.205f;
        public const float EyeRadius = 0.058f;
        private const int EyeLatitudes = 10;
        private const int EyeLongitudes = 16;

        /// <summary>The middle of the teeth: what goes on the prey's neck.</summary>
        public static readonly Vector3 JawAt = new Vector3(0.0f, -0.20f, 0.27f);

        public static readonly Color EyeColour = new Color(0.78f, 0.04f, 0.05f);
        public static readonly Color ToothColour = new Color(0.96f, 0.96f, 0.94f);
        private const float EyeSmoothness = 0.7f;
        private const float ToothSmoothness = 0.55f;

        private const string EyeMaterialPath = "Assets/CityX/Character/Materials/WraithEye.mat";
        private const string ToothMaterialPath = "Assets/CityX/Character/Materials/WraithTeeth.mat";
        public const string EyeMeshPath = "Assets/CityX/Character/WraithEye.asset";
        public const string TeethMeshPath = "Assets/CityX/Character/WraithTeeth.asset";

        /// <summary>The model's body is whole above here, metres up a standing body, and worn away to nothing at the other, which is above the tip of its tail: no visible end.</summary>
        public const float TailSolidY = 1.15f;
        public const float TailGoneY = 0.3f;

        /// <summary>The tail writhes from here down to its tip, this far at the tip, metres.</summary>
        public const float TailSwayTopY = 1.0f;
        public const float TailTipY = 0.13f;
        public const float TailSway = 0.14f;

        /// <summary>A point of the model this near the head's centre (shares of its radius) rides the head bone; the rest the spine above here and the hips below there, blended.</summary>
        public const float HeadBoneWithin = 1.06f;
        public const float SpineFullY = 1.45f;
        public const float HipsFullY = 1.0f;

        // Its colour: white with the faintest cold cast (owner: "make him a little less blue make him whiter"), the rim barely brighter, the
        // smoke inside the body a little stronger than the shader's default.
        public static readonly Color BodyColour = new Color(0.88f, 0.93f, 0.98f);
        public static readonly Color RimColour = new Color(1.05f, 1.05f, 1.08f);
        private const float BodyFlow = 0.45f;

        // The gas: soft puffs filling the tail's height, slow, growing as they thin.
        private static readonly Vector3 GasCentre = new Vector3(0.0f, 0.62f, 0.02f);
        private static readonly Vector3 GasSize = new Vector3(0.36f, 0.85f, 0.28f);
        public const float GasPerSecond = 30.0f;
        private const float GasPeakAlpha = 0.18f;
        private const int GasMost = 110;

        public static readonly Color GlowColour = new Color(0.8f, 0.9f, 1.0f);
        public const float GlowRange = 6.0f;
        private static readonly Vector3 GlowAt = new Vector3(0.0f, 1.55f, 0.1f);

        // The collider: the torso, hips to the top of the head.
        private static readonly Vector3 ColliderCentre = new Vector3(0.0f, 1.65f, 0.0f);
        private const float ColliderRadius = 0.4f;
        private const float ColliderHeight = 1.95f;

        [MenuItem("CityX/Rebuild Wraith")]
        public static GameObject Build()
        {
            var fused = AssetDatabase.LoadAssetAtPath<Mesh>(BodyMeshPath);
            if (fused == null)
            {
                Debug.LogError("CityX: no fused body mesh -- rebuild the player rig before the Wraith.");
                return null;
            }
            if (!ModelMeshes(out Mesh model, out Matrix4x4 modelPlace, out Mesh modelTeeth, out Matrix4x4 teethPlace))
            {
                return null;
            }

            GameObject root = BodyRigBuilder.BuildSkeleton("Wraith", out Dictionary<string, Transform> named);
            try
            {
                RestPose(named);
                Material ghost = GhostMaterial(GhostMaterialPath, "WraithGhost", TailSway, UnityEngine.Rendering.CullMode.Back);
                ghost.SetFloat("_Flow", BodyFlow);

                List<HumanBodyBones> order = BodyRigBuilder.SkinBoneOrder();
                // The fused body's bones, and the head's after them: the player's head is a mesh of its own on its bone, so the fused body has none.
                var bones = new Transform[order.Count + 1];
                for (int i = 0; i < order.Count; i++)
                {
                    bones[i] = named[NameOf(order[i])];
                }
                bones[order.Count] = named["head"];
                Mesh body = SaveMesh(WraithBodyPath, WholeBody(fused, order, model, modelPlace, named["head"].worldToLocalMatrix));
                var skin = new GameObject(BodySurface.SkinName);
                skin.transform.SetParent(root.transform, false);
                var skinRenderer = skin.AddComponent<SkinnedMeshRenderer>();
                skinRenderer.sharedMesh = body;
                skinRenderer.bones = bones;
                skinRenderer.rootBone = named["hips"];
                skinRenderer.sharedMaterial = ghost;
                skinRenderer.quality = SkinQuality.Bone4;
                skinRenderer.updateWhenOffscreen = true;
                NoShadow(skinRenderer);

                Transform face = BuildFace(named["head"], modelTeeth, teethPlace, out Transform jaw);
                ParticleSystem gas = BuildGas(named["hips"]);

                var glowObject = new GameObject(GlowName);
                glowObject.transform.SetParent(root.transform, false);
                glowObject.transform.localPosition = GlowAt;
                Light glow = glowObject.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = GlowColour;
                glow.intensity = Wraith.GlowIntensity;
                glow.range = GlowRange;
                glow.shadows = LightShadows.None;

                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.center = ColliderCentre;
                collider.radius = ColliderRadius;
                collider.height = ColliderHeight;

                var wraith = root.AddComponent<Wraith>();
                wraith.Hips = named["hips"];
                wraith.Spine = named["spine"];
                wraith.Head = named["head"];
                wraith.ArmLeft = named["arm_l"];
                wraith.ArmRight = named["arm_r"];
                wraith.ForearmLeft = named["forearm_l"];
                wraith.ForearmRight = named["forearm_r"];
                wraith.HandLeft = named["hand_l"];
                wraith.HandRight = named["hand_r"];
                wraith.Renderers = new Renderer[] { skinRenderer };
                wraith.MainCollision = collider;
                wraith.Glow = glow;
                wraith.Face = face;
                wraith.Jaw = jaw;
                wraith.Gas = gas;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"CityX: the Wraith written to {PrefabPath} -- {body.triangles.Length / 3} body triangles");
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>The model's two meshes, and where the file puts each in the body's frame.</summary>
        private static bool ModelMeshes(out Mesh body, out Matrix4x4 bodyPlace, out Mesh teeth, out Matrix4x4 teethPlace)
        {
            body = teeth = null;
            bodyPlace = teethPlace = Matrix4x4.identity;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError($"CityX: no {ModelPath} -- the Wraith has no body.");
                return false;
            }
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name.Contains("Teeth"))
                {
                    teeth = filter.sharedMesh;
                    teethPlace = filter.transform.localToWorldMatrix;
                }
                else
                {
                    body = filter.sharedMesh;
                    bodyPlace = filter.transform.localToWorldMatrix;
                }
            }
            if (body == null || teeth == null)
            {
                Debug.LogError($"CityX: {ModelPath} should hold a body and its teeth.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Every bone into the arms-down rest the body is bound in (BodyRig.RestBindMatrix): the skeleton is built in the T-pose a humanoid
        /// avatar needs, which the Wraith has no use for. Parents first, as BodyRig.Bones lists them.
        /// </summary>
        private static void RestPose(Dictionary<string, Transform> named)
        {
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                if (bone.Human == HumanBodyBones.LastBone || !named.TryGetValue(bone.Name, out Transform joint))
                {
                    continue;
                }
                Matrix4x4 rest = BodyRig.RestBindMatrix(bone.Human);
                joint.SetPositionAndRotation(rest.GetPosition(), rest.rotation);
            }
        }

        private static string NameOf(HumanBodyBones human)
        {
            foreach (BodyRig.Bone bone in BodyRig.Bones())
            {
                if (bone.Human == human)
                {
                    return bone.Name;
                }
            }
            throw new KeyNotFoundException($"CityX: the rig has no bone for {human}");
        }

        /// <summary>The player's arms with their own skinning, and the owner's model, skinned here: one mesh, on the fused body's bones.</summary>
        private static Mesh WholeBody(Mesh fused, List<HumanBodyBones> order, Mesh model, Matrix4x4 modelPlace, Matrix4x4 headBind)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var colours = new List<Color>();
            var weights = new List<BoneWeight>();
            var triangles = new List<int>();

            // The arms, whole: every triangle of the fused body wholly on them.
            Vector3[] fusedVertices = fused.vertices;
            Vector3[] fusedNormals = fused.normals;
            BoneWeight[] fusedWeights = fused.boneWeights;
            int[] fusedTriangles = fused.triangles;
            var remap = new int[fusedVertices.Length];
            for (int i = 0; i < remap.Length; i++)
            {
                remap[i] = -1;
            }
            for (int t = 0; t + 2 < fusedTriangles.Length; t += 3)
            {
                if (!IsArmBone(order[fusedWeights[fusedTriangles[t]].boneIndex0]) || !IsArmBone(order[fusedWeights[fusedTriangles[t + 1]].boneIndex0])
                    || !IsArmBone(order[fusedWeights[fusedTriangles[t + 2]].boneIndex0]))
                {
                    continue;
                }
                for (int k = 0; k < 3; k++)
                {
                    int i = fusedTriangles[t + k];
                    if (remap[i] < 0)
                    {
                        remap[i] = vertices.Count;
                        vertices.Add(fusedVertices[i]);
                        normals.Add(fusedNormals[i]);
                        uv.Add(Vector2.zero);
                        colours.Add(Color.white);
                        weights.Add(fusedWeights[i]);
                    }
                    triangles.Add(remap[i]);
                }
            }

            // The owner's model: its height decides how solid it is and how it sways.
            int head = order.Count, spine = order.IndexOf(HumanBodyBones.Spine), hips = order.IndexOf(HumanBodyBones.Hips);
            int offset = vertices.Count;
            Vector3[] modelVertices = model.vertices;
            Vector3[] modelNormals = model.normals;
            for (int i = 0; i < modelVertices.Length; i++)
            {
                Vector3 at = modelPlace.MultiplyPoint3x4(modelVertices[i]);
                vertices.Add(at);
                normals.Add(modelNormals.Length > i ? modelPlace.MultiplyVector(modelNormals[i]).normalized : Vector3.up);
                uv.Add(new Vector2(0.0f, TailSwayAt(at.y)));
                colours.Add(new Color(1.0f, 1.0f, 1.0f, BodyAlphaAt(at.y)));
                weights.Add(ModelWeight(at, head, spine, hips));
            }
            foreach (int index in model.triangles)
            {
                triangles.Add(offset + index);
            }

            var mesh = new Mesh
            {
                name = "WraithBody",
                indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colours);
            mesh.boneWeights = weights.ToArray();
            mesh.SetTriangles(triangles, 0);
            var binds = new List<Matrix4x4>(fused.bindposes) { headBind };
            mesh.bindposes = binds.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool IsArmBone(HumanBodyBones bone)
        {
            return bone == HumanBodyBones.LeftUpperArm || bone == HumanBodyBones.RightUpperArm || bone == HumanBodyBones.LeftLowerArm || bone == HumanBodyBones.RightLowerArm
                || bone == HumanBodyBones.LeftHand || bone == HumanBodyBones.RightHand || (bone >= HumanBodyBones.LeftThumbProximal && bone < HumanBodyBones.LastBone);
        }

        private static BoneWeight ModelWeight(Vector3 at, int head, int spine, int hips)
        {
            if ((at - new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f)).magnitude <= FusedBodyShape.HeadRadius * HeadBoneWithin)
            {
                return new BoneWeight { boneIndex0 = head, weight0 = 1.0f };
            }
            float upper = Mathf.InverseLerp(HipsFullY, SpineFullY, at.y);
            return new BoneWeight { boneIndex0 = spine, weight0 = upper, boneIndex1 = hips, weight1 = 1.0f - upper };
        }

        /// <summary>How solid the model is at this height (its vertex alpha): whole above the tail, worn away to nothing above its tip.</summary>
        public static float BodyAlphaAt(float y) => Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(TailGoneY, TailSolidY, y));

        /// <summary>How much of the sway reaches this height (uv.y, as the ghost shader reads it).</summary>
        public static float TailSwayAt(float y) => Mathf.InverseLerp(TailSwayTopY, TailTipY, y);

        /// <summary>
        /// Soft puffs coming off the tail, which is what reads as gas: in the world, so they stay where they came off as it drifts, slow, each growing as it thins to nothing. Thrown ribbons
        /// were tried and rejected ("that just looks like a tornado sitting beneath him").
        /// </summary>
        private static ParticleSystem BuildGas(Transform hips)
        {
            var go = new GameObject(GasName);
            go.transform.SetParent(hips, false);
            go.transform.localPosition = GasCentre - hips.localPosition;
            ParticleSystem particles = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0.0f, Mathf.PI * 2.0f);
            main.startColor = new Color(BodyColour.r, BodyColour.g, BodyColour.b, 1.0f);
            main.gravityModifier = -0.015f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = GasMost;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = GasPerSecond;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = GasSize;

            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.0f, 0.0f), new GradientAlphaKey(GasPeakAlpha, 0.25f), new GradientAlphaKey(0.0f, 1.0f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.SizeOverLifetimeModule grow = particles.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 0.7f, 1.0f, 1.6f));

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.6f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = GasMaterial();
            NoShadow(renderer);
            return particles;
        }

        /// <summary>The flamethrower's smoke puff, as its own material: nothing else to keep.</summary>
        private static Material GasMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GasMaterialPath);
            if (material != null)
            {
                return material;
            }
            Material smoke = AssetDatabase.LoadAssetAtPath<Material>(SmokeMaterialPath);
            if (smoke == null)
            {
                Debug.LogError($"CityX: no {SmokeMaterialPath} -- build the flamethrower before the Wraith.");
                return null;
            }
            material = new Material(smoke) { name = "WraithGas" };
            AssetDatabase.CreateAsset(material, GasMaterialPath);
            return material;
        }

        /// <summary>
        /// The face: two solid red eyes in the model's sockets, and the model's teeth under the point that goes on the prey's neck. Opaque and lit (CityX/Lit, cel shaded and outlined
        /// like everything solid), gone with the body when it dies (Wraith shrinks the face away).
        /// </summary>
        private static Transform BuildFace(Transform head, Mesh modelTeeth, Matrix4x4 teethPlace, out Transform jaw)
        {
            var face = new GameObject(FaceName);
            face.transform.SetParent(head, false);
            face.transform.localPosition = HeadMesh.CentreOnBone;

            Material eyeMaterial = SolidMaterial(EyeMaterialPath, "WraithEye", EyeColour, EyeSmoothness);
            Mesh eyeMesh = SaveMesh(EyeMeshPath, Sphere("WraithEye", EyeRadius, EyeLatitudes, EyeLongitudes));
            foreach ((string name, float side) in new[] { (EyeLeftName, -1.0f), (EyeRightName, 1.0f) })
            {
                var eye = new GameObject(name);
                eye.transform.SetParent(face.transform, false);
                eye.transform.localPosition = new Vector3(side * EyeOffsetX, EyeY, EyeZ);
                eye.AddComponent<MeshFilter>().sharedMesh = eyeMesh;
                MeshRenderer renderer = eye.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = eyeMaterial;
                NoShadow(renderer);
            }

            var jawObject = new GameObject(JawName);
            jawObject.transform.SetParent(face.transform, false);
            jawObject.transform.localPosition = JawAt;
            var teeth = new GameObject(TeethName);
            teeth.transform.SetParent(jawObject.transform, false);
            teeth.AddComponent<MeshFilter>().sharedMesh = SaveMesh(TeethMeshPath, TeethAtJaw(modelTeeth, teethPlace));
            MeshRenderer teethRenderer = teeth.AddComponent<MeshRenderer>();
            teethRenderer.sharedMaterial = SolidMaterial(ToothMaterialPath, "WraithTeeth", ToothColour, ToothSmoothness);
            NoShadow(teethRenderer);

            jaw = jawObject.transform;
            return face.transform;
        }

        /// <summary>The model's teeth, measured from the point they hang under.</summary>
        private static Mesh TeethAtJaw(Mesh modelTeeth, Matrix4x4 place)
        {
            Vector3 origin = new Vector3(0.0f, BodyRig.HeadCentreY, 0.0f) + JawAt;
            Vector3[] vertices = modelTeeth.vertices;
            Vector3[] normals = modelTeeth.normals;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = place.MultiplyPoint3x4(vertices[i]) - origin;
                if (normals.Length > i)
                {
                    normals[i] = place.MultiplyVector(normals[i]).normalized;
                }
            }
            var mesh = new Mesh { name = "WraithTeeth" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(modelTeeth.triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A plain sphere of <paramref name="radius"/>, as coarse as an eye needs.</summary>
        private static Mesh Sphere(string name, float radius, int latitudes, int longitudes)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int lat = 0; lat <= latitudes; lat++)
            {
                float polar = Mathf.PI * lat / latitudes;
                for (int lon = 0; lon <= longitudes; lon++)
                {
                    float round = 2.0f * Mathf.PI * lon / longitudes;
                    var normal = new Vector3(Mathf.Sin(polar) * Mathf.Cos(round), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(round));
                    vertices.Add(normal * radius);
                    normals.Add(normal);
                }
            }
            int row = longitudes + 1;
            for (int lat = 0; lat < latitudes; lat++)
            {
                for (int lon = 0; lon < longitudes; lon++)
                {
                    // At the poles one of a quad's two triangles has two corners on the pole: nothing, left out.
                    int a = lat * row + lon;
                    if (lat > 0)
                    {
                        triangles.AddRange(new[] { a, a + 1, a + row });
                    }
                    if (lat < latitudes - 1)
                    {
                        triangles.AddRange(new[] { a + 1, a + row + 1, a + row });
                    }
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A plain lit, opaque material at <paramref name="path"/> (CityX/Lit, cel shaded), kept where it exists.</summary>
        private static Material SolidMaterial(string path, string name, Color colour, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(CityX.Core.LitShader.Find()) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Writes the mesh at <paramref name="path"/>, updated in place when it exists so the prefab keeps its reference.</summary>
        private static Mesh SaveMesh(string path, Mesh mesh)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.colors = mesh.colors;
            existing.boneWeights = mesh.boneWeights;
            existing.triangles = mesh.triangles;
            existing.bindposes = mesh.bindposes;
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        /// <summary>A ghost material at <paramref name="path"/>, kept where it exists so the prefab keeps its reference.</summary>
        private static Material GhostMaterial(string path, string name, float sway, UnityEngine.Rendering.CullMode cull)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"CityX: no {ShaderName} shader -- the Wraith will be pink.");
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }
            material.SetFloat("_Sway", sway);
            material.SetFloat("_Cull", (float)cull);
            material.SetColor("_BaseColor", BodyColour);
            material.SetColor("_RimColor", RimColour);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void NoShadow(Renderer renderer)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
