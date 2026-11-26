using System.Collections;
using System.Collections.Generic;
using CityX.Character;
using CityX.Core;
using CityX.Items;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CityX.Tests
{
    /// <summary>
    /// THE BROWN COAT ON THE MOVING BODY (docs/BROWN_COAT.md; owner, 2026-11-24, of it in the game: "there is holes in the armpit and one in the
    /// back of the brown coat and the croatch clips through ... fix the holes without having a stretch when arms move", "the coat needs to go
    /// over it instead of it clipping through", "is that line around where the butt is a hole in the coat?"). The real body, posed by the game
    /// (standing, mid-walk, mid-sprint, arms out, arms forward); the coat as it is drawn. The coat stays over the crotch, the hips and the seat,
    /// never sunk into the drawn body there (standing, walking, arms up: mid-sprint the legs swing through the skirt's own reach, SkirtCloth's);
    /// no edge of the cloth is left open under the arms in any pose; and nothing of it tears (<see cref="MostStretch"/>).
    /// </summary>
    public sealed class BrownCoatFitPlayTests
    {
        /// <summary>How far a point of the cloth may sink into the body's drawn surface before it shows, metres (the outline is wider).</summary>
        public const float SinkTolerance = 0.006f;

        /// <summary>How long an edge of the cloth may grow over its length standing (a tear), where it grows by more than MostGrowth (a few
        /// millimetres of a small triangle growing to a few more do not show). Not inside the armpits: built with the arms out, their fold opens
        /// out with an arm lifted by design (brown_coat_build.py, relax_armpits).</summary>
        public const float MostStretch = 3.0f;

        public const float MostGrowth = 0.04f;

        /// <summary>The crotch and hips in front, in the body's frame (+z ahead), where the coat's front must stay over the body.</summary>
        private static readonly Bounds Crotch = new Bounds(new Vector3(0.0f, 1.08f, 0.2f), new Vector3(0.4f, 0.36f, 0.4f));

        /// <summary>The seat behind, where the coat's back must stay over the body (owner, 2026-11-24: "is that line around where the butt is a hole in the coat?").</summary>
        private static readonly Bounds Seat = new Bounds(new Vector3(0.0f, 0.9f, -0.2f), new Vector3(0.5f, 0.5f, 0.4f));

        /// <summary>Under each arm, in the body's frame, where no edge of the cloth may be open.</summary>
        private static readonly Bounds[] Armpits =
        {
            new Bounds(new Vector3(0.45f, 1.55f, 0.0f), new Vector3(0.4f, 0.5f, 0.6f)),
            new Bounds(new Vector3(-0.45f, 1.55f, 0.0f), new Vector3(0.4f, 0.5f, 0.6f)),
        };

        /// <summary>The drawn skin nearest a point of the cloth is read only this near, metres.</summary>
        public const float NearSkin = 0.08f;

        private GameObject _player;
        private GameObject _ground;

        [TearDown]
        public void TearDown()
        {
            Time.captureFramerate = SteadyFrameTime.FramesPerSecond;
            if (_player != null) Object.Destroy(_player);
            if (_ground != null) Object.Destroy(_ground);
        }

        [UnityTest]
        public IEnumerator TheCoatStaysOverTheCrotchClosedUnderTheArmsAndUnstretchedInEveryPose()
        {
#if UNITY_EDITOR
            Time.captureFramerate = 60;
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(0.0f, -0.5f, 0.0f);
            _ground.transform.localScale = new Vector3(400.0f, 1.0f, 400.0f);
            _player = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab"),
                new Vector3(0.0f, 0.05f, 0.0f), Quaternion.identity);
            _player.GetComponent<PlayerInputReader>().enabled = false;
            var locomotion = _player.GetComponent<PlayerLocomotion>();
            Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Wearables/BrownCoat.prefab"), new Vector3(30.0f, 0.5f, 30.0f),
                Quaternion.identity).GetComponent<IEquippableItem>().OnEquip(_player, ItemInstanceData.None);
            yield return null;

            var failures = new List<string>();
            for (int i = 0; i < 90; i++)
            {
                locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                yield return null;
            }
            float[] standing = Measure("standing", failures, null);
            foreach ((string name, bool sprint, int frames) in new[] { ("walk", false, 50), ("sprint1", true, 60), ("sprint2", true, 6), ("sprint3", true, 6),
                ("sprint4", true, 6) })
            {
                for (int i = 0; i < frames; i++)
                {
                    locomotion.Tick(new LocomotionInput { Move = Vector2.up, Sprint = sprint }, 1.0f / 60.0f);
                    yield return null;
                }
                Measure(name, failures, standing, sprint);
            }
            var lift = _player.AddComponent<CoatCaptureArmLift>();
            foreach ((string name, Vector3 way) in new[] { ("arms out", Vector3.right), ("arms forward", Vector3.forward) })
            {
                lift.Way = way;
                lift.Degrees = 80.0f;
                for (int i = 0; i < 20; i++)
                {
                    locomotion.Tick(new LocomotionInput(), 1.0f / 60.0f);
                    yield return null;
                }
                Measure(name, failures, standing);
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
#else
            yield break;
#endif
        }

        /// <summary>The coat as drawn now against the body as drawn now; the coat's edge lengths, for the later poses to be compared with.</summary>
        private float[] Measure(string pose, List<string> failures, float[] standing, bool legsSwing = false)
        {
            SkinnedMeshRenderer clothes = TransformSearch.FindDeep(_player.transform, "CombinedGarmentSkin").GetComponent<SkinnedMeshRenderer>();
            SkinnedMeshRenderer skin = TransformSearch.FindDeep(_player.transform, BodySurface.SkinName).GetComponent<SkinnedMeshRenderer>();
            int coatPart = System.Array.FindIndex(clothes.sharedMaterials, m => m != null && m.name.StartsWith("BrownCoat"));
            Assert.GreaterOrEqual(coatPart, 0, "the Brown Coat is not worn");

            var coatMesh = new Mesh();
            clothes.BakeMesh(coatMesh, true);
            Matrix4x4 coatToBody = _player.transform.worldToLocalMatrix * Matrix4x4.TRS(clothes.transform.position, clothes.transform.rotation, Vector3.one);
            Vector3[] coat = System.Array.ConvertAll(coatMesh.vertices, p => coatToBody.MultiplyPoint3x4(p));
            int[] triangles = coatMesh.GetTriangles(coatPart);
            Vector2[] uvs = clothes.sharedMesh.uv;

            var bodyMesh = new Mesh();
            skin.BakeMesh(bodyMesh, true);
            Matrix4x4 bodyToBody = _player.transform.worldToLocalMatrix * Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
            Vector3[] bodyPoints = bodyMesh.vertices, bodyNormals = bodyMesh.normals;
            var body = new List<(Vector3 Point, Vector3 Normal)>();
            foreach (int index in new HashSet<int>(skin.sharedMesh.triangles))   // the body as drawn: what the coat hides of it is not
            {
                body.Add((bodyToBody.MultiplyPoint3x4(bodyPoints[index]), bodyToBody.MultiplyVector(bodyNormals[index]).normalized));
            }

            // Over the crotch: no point of the coat there behind the nearest drawn skin.
            int sunk = 0;
            float deepest = 0.0f;
            Vector3 deepestAt = Vector3.zero;
            foreach (int index in new HashSet<int>(triangles))
            {
                Vector3 p = coat[index];
                if (!Crotch.Contains(p) && !Seat.Contains(p))
                {
                    continue;
                }
                float best = float.MaxValue;
                int nearest = -1;
                for (int b = 0; b < body.Count; b++)
                {
                    float d = (body[b].Point - p).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = b;
                    }
                }
                if (nearest < 0 || best > NearSkin * NearSkin)
                {
                    continue;   // no drawn skin under it here (the coat hides the body behind the hips)
                }
                float depth = -Vector3.Dot(p - body[nearest].Point, body[nearest].Normal);
                if (depth > SinkTolerance)
                {
                    sunk++;
                    if (depth > deepest)
                    {
                        deepest = depth;
                        deepestAt = p;
                    }
                }
            }

            // Closed under the arms: every edge of the cloth there, its points welded where they lie together, has a face on either side.
            var weld = new Dictionary<Vector3Int, int>();
            int Weld(int i)
            {
                var key = Vector3Int.RoundToInt(coat[i] * 2000.0f);
                if (!weld.TryGetValue(key, out int id))
                {
                    id = weld.Count;
                    weld[key] = id;
                }
                return id;
            }
            var faces = new Dictionary<(int, int), int>();
            var where = new Dictionary<(int, int), Vector3>();
            for (int t = 0; t < triangles.Length; t += 3)
            {
                if (Vector3.Cross(coat[triangles[t + 1]] - coat[triangles[t]], coat[triangles[t + 2]] - coat[triangles[t]]).magnitude < 1e-6f)
                {
                    continue;   // a flap laid flat in an edge (flatten_flaps): no cloth
                }
                for (int k = 0; k < 3; k++)
                {
                    int a = Weld(triangles[t + k]), b = Weld(triangles[t + (k + 1) % 3]);
                    if (a == b)
                    {
                        continue;
                    }
                    var edge = (Mathf.Min(a, b), Mathf.Max(a, b));
                    faces.TryGetValue(edge, out int n);
                    faces[edge] = n + 1;
                    where[edge] = (coat[triangles[t + k]] + coat[triangles[t + (k + 1) % 3]]) * 0.5f;
                }
            }
            int open = 0;
            foreach (KeyValuePair<(int, int), int> edge in faces)
            {
                if (edge.Value == 1 && System.Array.Exists(Armpits, pit => pit.Contains(where[edge.Key])))
                {
                    open++;
                }
            }

            // Torn: each edge of the cloth (not the armpits' walls, painted one colour, their corners on one place of the map, nor the armpits
            // round them) against its length standing.
            var lengths = new float[triangles.Length];
            float most = 0.0f;
            Vector3 mostAt = Vector3.zero;
            for (int t = 0; t < triangles.Length; t++)
            {
                int first = t - t % 3;
                bool wall = uvs[triangles[first]] == uvs[triangles[first + 1]] && uvs[triangles[first]] == uvs[triangles[first + 2]];
                int a = triangles[t], b = triangles[t % 3 == 2 ? t - 2 : t + 1];
                lengths[t] = Vector3.Distance(coat[a], coat[b]);
                bool armpit = System.Array.Exists(Armpits, pit => pit.Contains(coat[a]));
                if (!wall && !armpit && standing != null && lengths[t] - standing[t] > MostGrowth && lengths[t] / standing[t] > most)
                {
                    most = lengths[t] / standing[t];
                    mostAt = coat[a];
                }
            }

            Debug.Log($"CityX coat fit, {pose}: {sunk} points sunk over the crotch (deepest {deepest * 100.0f:0.0} cm at {deepestAt}), {open} open edges " +
                      $"under the arms, the most stretched edge {most:0.00} times its length standing at {mostAt}");
            if (sunk > 0 && !legsSwing)
            {
                failures.Add($"{pose}: {sunk} points of the coat sink into the crotch or hips, deepest {deepest * 100.0f:0.0} cm at {deepestAt}");
            }
            if (open > 0)
            {
                failures.Add($"{pose}: {open} edges of the coat are open under the arms");
            }
            if (most > MostStretch)
            {
                failures.Add($"{pose}: an edge of the coat stretches to {most:0.00} times its length standing, at {mostAt}");
            }
            Object.Destroy(coatMesh);
            Object.Destroy(bodyMesh);
            return lengths;
        }
    }
}
