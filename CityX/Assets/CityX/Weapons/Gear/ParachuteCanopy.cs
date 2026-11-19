using System.Collections.Generic;
using UnityEngine;

namespace CityX.Weapons
{
    /// <summary>
    /// THE CANOPY AND ITS LINES, as shown (docs/PARACHUTE.md). The body's ParachuteRider places it every frame, after the body is posed: over the
    /// shoulders, turned with the way the body flies and banked with its turns, opened out as far as the opening has got. The suspension lines run
    /// from the lower surface down to each shoulder, a steering line from each tip's tail to each hand. On another player's copy the
    /// item's behaviours are off; this is driven all the same, by that body's rider.
    /// </summary>
    public sealed class ParachuteCanopy : MonoBehaviour
    {
        [Tooltip("The lines' mesh (drawn as lines), set by ParachuteBuilder.")]
        public MeshFilter Lines;

        private Mesh _lineMesh;
        private readonly List<Vector3> _lineVertices = new List<Vector3>();
        private int[] _lineIndices;
        private List<Vector3> _leftAnchors;
        private List<Vector3> _rightAnchors;
        private List<Vector3> _leftSteering;
        private List<Vector3> _rightSteering;

        public bool Showing => gameObject.activeSelf;

        /// <summary>
        /// Out over the body: its lines meeting at <paramref name="hangPoint"/> (between the shoulders), turned to <paramref name="rotation"/>, opened
        /// out to <paramref name="opened"/> (x the span, y how far the lines have paid out, z the chord; 1 all the way), the lines down to the shoulders
        /// and the hands. <paramref name="size"/> is the body's own size (a giant's canopy is a giant's).
        /// </summary>
        public void Place(Vector3 hangPoint, Quaternion rotation, Vector3 opened, float size,
            Vector3 leftShoulder, Vector3 rightShoulder, Vector3 leftHand, Vector3 rightHand)
        {
            Show(true);
            transform.SetPositionAndRotation(hangPoint, rotation);
            SetWorldScale(opened * size);
            DrawLines(rotation, opened * size, leftShoulder, rightShoulder, leftHand, rightHand);
        }

        /// <summary>Let go of (cut away, collapsing): where it is now and its shape, with no lines.</summary>
        public void PlaceLoose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Show(true);
            transform.SetPositionAndRotation(position, rotation);
            SetWorldScale(scale);
            if (Lines != null)
            {
                Lines.gameObject.SetActive(false);
            }
        }

        public void Hide()
        {
            Show(false);
        }

        private void Show(bool on)
        {
            if (gameObject.activeSelf != on)
            {
                gameObject.SetActive(on);
            }
            if (Lines != null && Lines.gameObject.activeSelf != on)
            {
                Lines.gameObject.SetActive(on);
            }
        }

        private void SetWorldScale(Vector3 scale)
        {
            Vector3 parent = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(scale.x / Mathf.Max(parent.x, 1e-4f), scale.y / Mathf.Max(parent.y, 1e-4f), scale.z / Mathf.Max(parent.z, 1e-4f));
        }

        private void DrawLines(Quaternion rotation, Vector3 scale, Vector3 leftShoulder, Vector3 rightShoulder, Vector3 leftHand, Vector3 rightHand)
        {
            if (Lines == null)
            {
                return;
            }
            if (_lineMesh == null)
            {
                _lineMesh = new Mesh { name = "ParachuteLines" };
                _lineMesh.MarkDynamic();
                Lines.sharedMesh = _lineMesh;
                _leftAnchors = CanopyShape.LineAnchors(false);
                _rightAnchors = CanopyShape.LineAnchors(true);
                _leftSteering = CanopyShape.SteeringAnchors(false);
                _rightSteering = CanopyShape.SteeringAnchors(true);
            }
            _lineVertices.Clear();
            Side(_leftAnchors, _leftSteering, leftShoulder, leftHand, rotation, scale);
            Side(_rightAnchors, _rightSteering, rightShoulder, rightHand, rotation, scale);
            Transform space = Lines.transform;
            for (int i = 0; i < _lineVertices.Count; i++)
            {
                _lineVertices[i] = space.InverseTransformPoint(_lineVertices[i]);
            }
            _lineMesh.Clear();
            _lineMesh.SetVertices(_lineVertices);
            if (_lineIndices == null || _lineIndices.Length != _lineVertices.Count)
            {
                _lineIndices = new int[_lineVertices.Count];
                for (int i = 0; i < _lineIndices.Length; i++)
                {
                    _lineIndices[i] = i;
                }
            }
            _lineMesh.SetIndices(_lineIndices, MeshTopology.Lines, 0);
            _lineMesh.RecalculateBounds();
        }

        private void Side(List<Vector3> anchors, List<Vector3> steering, Vector3 shoulder, Vector3 hand, Quaternion rotation, Vector3 scale)
        {
            foreach (Vector3 anchor in anchors)
            {
                _lineVertices.Add(shoulder);
                _lineVertices.Add(transform.position + rotation * Vector3.Scale(anchor, scale));
            }
            foreach (Vector3 anchor in steering)
            {
                _lineVertices.Add(hand);
                _lineVertices.Add(transform.position + rotation * Vector3.Scale(anchor, scale));
            }
        }
    }
}
