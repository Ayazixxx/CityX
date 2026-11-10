using Unity.Collections;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// Painting the body while it idles (owner, 2026-11-10: "make it so you can draw on your character when its not tposed"). The brush and
    /// its atlas know the body as it stood when the creator woke; the ray has to find it as it stands now. So the ray goes against the
    /// body baked in this frame's pose, and the point it hits is carried over, by its place on its triangle, to that same triangle as it
    /// stood. From there a stroke is the same as any other.
    /// </summary>
    public sealed partial class CharacterCreator
    {
        private Mesh _bakedNow;
        private Vector3[] _verticesNow;
        private Vector3[] _verticesStill;
        private int[] _triangles;
        private float[] _boxesNow;
        private Vector3[] _headVertices;
        private int[] _headTriangles;
        private float[] _headBoxes;
        private Matrix4x4 _headToBodyStill = Matrix4x4.identity;
        private int _bakedFrame = -1;
        private Vector3 _hitNow;
        private Vector3 _normalNow;

        /// <summary>Called once the body has been bound as it stands: the same surfaces, for the ray to carry a hit back to.</summary>
        private void RememberStillSurface(Mesh posed, Mesh head, Matrix4x4 headToBody)
        {
            _verticesStill = posed.vertices;
            _triangles = posed.triangles;
            _verticesNow = new Vector3[_verticesStill.Length];
            _boxesNow = MeshRay.NewBoxes(_triangles.Length);
            if (head != null)
            {
                _headVertices = head.vertices;
                _headTriangles = head.triangles;
                _headBoxes = MeshRay.NewBoxes(_headTriangles.Length);
                MeshRay.FitBoxes(_headVertices, _headTriangles, _headBoxes);
                _headToBodyStill = headToBody;
            }
        }

        /// <summary>The body as drawn on the screen, once a frame and only while the pointer is asking.</summary>
        private void BakeTheBodyAsItIs()
        {
            if (_bakedFrame == Time.frameCount)
            {
                return;
            }
            _bakedFrame = Time.frameCount;
            if (_bakedNow == null)
            {
                _bakedNow = new Mesh { name = "CreatorBodyNow", hideFlags = HideFlags.DontSave };
                _bakedNow.MarkDynamic();
            }
            BodyRenderer.BakeMesh(_bakedNow);
            using (Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(_bakedNow))
            using (var vertices = new NativeArray<Vector3>(_verticesNow.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory))
            {
                data[0].GetVertices(vertices);
                vertices.CopyTo(_verticesNow);
            }
            MeshRay.FitBoxes(_verticesNow, _triangles, _boxesNow);
        }

        /// <summary>The ray against the body as it stands now. <paramref name="localPoint"/> and <paramref name="triangle"/> are as the brush knows them.</summary>
        private bool RaycastAsItStands(Ray ray, out Vector3 localPoint, out int triangle)
        {
            localPoint = Vector3.zero;
            triangle = -1;
            if (BodyRenderer == null || _verticesStill == null)
            {
                return false;
            }
            BakeTheBodyAsItIs();

            Transform body = BodyRenderer.transform;
            bool onBody = Cast(body, _verticesNow, _triangles, _boxesNow, ray, out float bodyDistance, out int bodyFace, out float bodyU, out float bodyV);
            float headDistance = float.MaxValue;
            int headFace = -1;
            float headU = 0.0f, headV = 0.0f;
            bool onHead = HeadRenderer != null && _headVertices != null
                && Cast(HeadRenderer.transform, _headVertices, _headTriangles, _headBoxes, ray, out headDistance, out headFace, out headU, out headV);
            if (!onBody && !onHead)
            {
                return false;
            }

            if (onHead && (!onBody || headDistance < bodyDistance))   // whichever is in front, as Raycast: they overlap at the neck
            {
                localPoint = _headToBodyStill.MultiplyPoint3x4(Blend(_headVertices, _headTriangles, headFace, headU, headV));
                triangle = _paint.BodyTriangleCount + headFace;
                NoteHit(HeadRenderer.transform, _headVertices, _headTriangles, headFace, headU, headV);
                return true;
            }
            localPoint = Blend(_verticesStill, _triangles, bodyFace, bodyU, bodyV);
            triangle = bodyFace;
            NoteHit(body, _verticesNow, _triangles, bodyFace, bodyU, bodyV);
            return true;
        }

        /// <summary><see cref="MeshRay.Nearest"/> from the world into <paramref name="space"/>, with the distance back in metres.</summary>
        private static bool Cast(Transform space, Vector3[] vertices, int[] triangles, float[] boxes, Ray ray, out float metres, out int face, out float u, out float v)
        {
            Vector3 origin = space.InverseTransformPoint(ray.origin);
            Vector3 direction = space.InverseTransformVector(ray.direction).normalized;
            bool hit = MeshRay.Nearest(vertices, triangles, boxes, origin, direction, out float distance, out face, out u, out v);
            metres = hit ? Vector3.Distance(ray.origin, space.TransformPoint(origin + direction * distance)) : float.MaxValue;
            return hit;
        }

        private static Vector3 Blend(Vector3[] vertices, int[] triangles, int face, float u, float v)
        {
            Vector3 a = vertices[triangles[face * 3]];
            Vector3 b = vertices[triangles[face * 3 + 1]];
            Vector3 c = vertices[triangles[face * 3 + 2]];
            return a + (b - a) * u + (c - a) * v;
        }

        /// <summary>Where the hit is on the screen's body, for the brush ring, which is drawn on that and not on the body as the brush knows it.</summary>
        private void NoteHit(Transform space, Vector3[] vertices, int[] triangles, int face, float u, float v)
        {
            Vector3 a = vertices[triangles[face * 3]];
            Vector3 b = vertices[triangles[face * 3 + 1]];
            Vector3 c = vertices[triangles[face * 3 + 2]];
            _hitNow = space.TransformPoint(a + (b - a) * u + (c - a) * v);
            _normalNow = space.TransformDirection(Vector3.Cross(b - a, c - a)).normalized;
        }
    }
}
