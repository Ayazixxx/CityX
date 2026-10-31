using CityX.World;
using UnityEngine;

namespace CityX.View
{
    /// <summary>
    /// Old Guy's front door behind its planks (BoardedPlank): shut while any plank is left, and once the last one is torn off it swings open into the
    /// hall. It follows the shared record of what is broken (TutorialHouseDamage), so it opens on every machine, and stands open for a player who
    /// joins after.
    /// </summary>
    public sealed class BoardedDoor : MonoBehaviour
    {
        /// <summary>How far the leaf swings, and how long it takes.</summary>
        public const float OpenDegrees = 100.0f, OpenSeconds = 0.6f;

        /// <summary>The leaf's hinge.</summary>
        public Transform Leaf;

        /// <summary>Which way about +y opens the leaf into the house (set by the builder from the wall's outside).</summary>
        public float OpenSign = 1.0f;

        public bool Open => _target > 0.0f;

        private Quaternion _shut;
        private float _angle;
        private float _target;

        private void OnEnable()
        {
            TutorialHouseDamage.Shared.PieceBroken += OnPieceBroken;
        }

        private void OnDisable()
        {
            TutorialHouseDamage.Shared.PieceBroken -= OnPieceBroken;
        }

        private void Start()
        {
            _shut = Leaf.localRotation;
            if (AllPlanksGone())
            {
                _target = _angle = OpenDegrees;   // opened before this machine built the house
                Pose();
            }
        }

        private void Update()
        {
            if (Mathf.Approximately(_angle, _target))
            {
                return;
            }
            _angle = Mathf.MoveTowards(_angle, _target, OpenDegrees / OpenSeconds * Time.deltaTime);
            Pose();
        }

        private void OnPieceBroken(int piece)
        {
            if (piece < OldGuysHouse.FrontDoorPlanks && AllPlanksGone())
            {
                _target = OpenDegrees;
            }
        }

        private static bool AllPlanksGone()
        {
            for (int i = 0; i < OldGuysHouse.FrontDoorPlanks; i++)
            {
                if (!TutorialHouseDamage.Shared.IsBroken(i))
                {
                    return false;
                }
            }
            return true;
        }

        private void Pose()
        {
            Leaf.localRotation = _shut * Quaternion.Euler(0, OpenSign * _angle, 0);
        }
    }
}
