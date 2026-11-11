using System.Collections.Generic;
using System.Linq;
using CityX.Character;
using NUnit.Framework;
using UnityEngine;

namespace CityX.Tests
{
    /// <summary>
    /// THE PAINT BUCKET ON THE REAL BODY (owner, 2026-10-15: a bucket of colour on the torso left "a white sliver around the neck that visibly
    /// wasnt reached by the paint bucket, but is apart of the torso ... make the paint bucket color the whole torso unless being clicked in a
    /// contained area"; 2026-11-10: "make the paint bucket tool work across the entire body instead separated by limb"): a bucket on
    /// the chest fills the whole body, every part of the paint, across the seams between them; and inside an outline drawn on the chest it fills only the inside.
    /// </summary>
    public sealed class BodyPaintBucketTests
    {
        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uvs;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            _mesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/FusedBody.asset");
#endif
            Assert.IsNotNull(_mesh, "no body mesh");
            _vertices = _mesh.vertices;
            _normals = _mesh.normals;
            _uvs = _mesh.uv;
        }

        private static int RowOf(HumanBodyBones part) => System.Array.IndexOf(PaintAtlas.Parts, part);

        private int RowAt(int vertex) => Mathf.Clamp(Mathf.FloorToInt(_uvs[vertex] .y * PaintAtlas.PartCount), 0, PaintAtlas.PartCount - 1);

        /// <summary>The front-most vertex of a part, facing forward, among those <paramref name="where"/> allows.</summary>
        private Vector3 Front(HumanBodyBones part, System.Func<Vector3, bool> where)
        {
            int row = RowOf(part);
            int best = -1;
            for (int i = 0; i < _vertices.Length; i++)
            {
                if (RowAt(i) != row || _normals[i].z < 0.7f || !where(_vertices[i]))
                {
                    continue;
                }
                if (best < 0 || _vertices[i].z > _vertices[best].z)
                {
                    best = i;
                }
            }
            Assert.GreaterOrEqual(best, 0, $"no forward-facing {part} on the body");
            return _vertices[best];
        }

        private static Color At(BodyPaint paint, Vector3 point)
        {
            int triangle = paint.FindTriangle(point, 0.03f);
            Assert.GreaterOrEqual(triangle, 0, $"no triangle at {point}");
            return paint.SampleAt(point, triangle);
        }

        private static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.05f;

        /// <summary>Binds the body and its head as the creator does (CharacterCreator: the head in the body's space), returning the head.</summary>
        private Mesh BindWithTheHead(BodyPaint paint, out Matrix4x4 headToBody)
        {
#if UNITY_EDITOR
            var head = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/CityX/Character/Head.asset");
            var player = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CityX/Character/Player.prefab");
            SkinnedMeshRenderer body = player.GetComponentInChildren<SkinnedMeshRenderer>(true);
            MeshFilter onTheHead = player.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh == head);
            Assert.IsNotNull(onTheHead, "the player has no head mesh");
            headToBody = body.transform.worldToLocalMatrix * onTheHead.transform.localToWorldMatrix;
            paint.Bind(_mesh, head, headToBody);
            return head;
#else
            headToBody = Matrix4x4.identity;
            paint.Bind(_mesh);
            return null;
#endif
        }

        /// <summary>The front of the face, in the body's space.</summary>
        private static Vector3 HeadFront(Mesh head, Matrix4x4 headToBody)
        {
            Vector3[] vertices = head.vertices, normals = head.normals;
            Vector3 best = Vector3.zero;
            bool found = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 at = headToBody.MultiplyPoint3x4(vertices[i]);
                if (headToBody.MultiplyVector(normals[i]).normalized.z > 0.7f && (!found || at.z > best.z))
                {
                    best = at;
                    found = true;
                }
            }
            Assert.IsTrue(found, "no forward-facing head");
            return best;
        }

        [Test]
        public void ABucketOnTheChestFillsTheWholeBody()
        {
            var paint = new BodyPaint(Color.white);
            Mesh head = BindWithTheHead(paint, out Matrix4x4 headToBody);
            float middle = _mesh.bounds.center.y;
            Vector3 chest = Front(HumanBodyBones.Hips, v => Mathf.Abs(v.x) < 0.06f && v.y > middle);

            paint.BucketFillAt(chest, paint.FindTriangle(chest, 0.03f), Color.red);
            Assert.IsTrue(Near(At(paint, chest), Color.red), "the chest was not filled");
            Assert.IsTrue(Near(At(paint, Front(HumanBodyBones.Neck, v => Mathf.Abs(v.x) < 0.05f)), Color.red), "the neck above the torso was not reached: the white sliver");
            foreach (HumanBodyBones part in PaintAtlas.Parts)
            {
                Vector3 front = part == HumanBodyBones.Head ? HeadFront(head, headToBody) : Front(part, v => true);
                Assert.IsTrue(Near(At(paint, front), Color.red), $"a bucket on the torso did not reach the {part}");
            }
        }

        [Test]
        public void InsideAnOutlineDrawnOnTheChestTheBucketFillsOnlyTheInside()
        {
            var paint = new BodyPaint(Color.white);
            paint.Bind(_mesh);
            float middle = _mesh.bounds.center.y;
            Vector3 chest = Front(HumanBodyBones.Hips, v => Mathf.Abs(v.x) < 0.06f && v.y > middle);
            Vector3 neck = Front(HumanBodyBones.Neck, v => Mathf.Abs(v.x) < 0.05f);

            // A ring 9 cm round the chest point, through the body's own front vertices.
            int row = RowOf(HumanBodyBones.Hips);
            var ring = new List<Vector3>();
            for (int i = 0; i < _vertices.Length; i++)
            {
                Vector2 off = new Vector2(_vertices[i].x - chest.x, _vertices[i].y - chest.y);
                if (RowAt(i) == row && _normals[i].z > 0.6f && Mathf.Abs(off.magnitude - 0.09f) < 0.012f)
                {
                    ring.Add(_vertices[i]);
                }
            }
            Assert.Greater(ring.Count, 8, "not enough of the chest to draw a ring on");
            ring = ring.OrderBy(v => Mathf.Atan2(v.y - chest.y, v.x - chest.x)).ToList();
            for (int i = 0; i < ring.Count; i++)
            {
                Vector3 from = ring[i];
                Vector3 to = ring[(i + 1) % ring.Count];
                paint.PaintStroke(from, paint.FindTriangle(from, 0.03f), to, paint.FindTriangle(to, 0.03f), Color.blue, 2);
            }

            paint.BucketFillAt(chest, paint.FindTriangle(chest, 0.03f), Color.green);
            Assert.IsTrue(Near(At(paint, chest), Color.green), "inside the outline was not filled");
            Vector3 below = Front(HumanBodyBones.Hips, v => Mathf.Abs(v.x) < 0.06f && v.y < chest.y - 0.25f && v.y > chest.y - 0.4f);
            Assert.IsTrue(Near(At(paint, below), Color.white), "the fill got out of the outline");
            Assert.IsTrue(Near(At(paint, neck), Color.white), "the fill got out of the outline and up the neck");
        }
    }
}
