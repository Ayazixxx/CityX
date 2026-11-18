using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// How far off each wall a walking body is kept: as far as its widest arm swing has needed, let back toward the wall only slowly. Kept
    /// off by each frame's arms alone, the body was pushed off as the near arm swung out and walked back in as it swung in, 13 cm each stride
    /// (owner, 2026-11-10: "stop at contact with the wall"). Each wall is remembered as a plane the body's root stays behind, raised at once to
    /// what any contact needs, eased toward the wall at <see cref="EasesInMetresPerSecond"/>, forgotten <see cref="ForgetSeconds"/> after it
    /// was last touched or once the body is well clear of it.
    /// </summary>
    public sealed class WallStandoff
    {
        /// <summary>How fast a remembered wall lets the body back toward it, metres a second on a body of the player's size: at 0.1 it let the body
        /// back between two swings as far as one swing had pushed it off (a 5.5 cm sawtooth each stride).</summary>
        public const float EasesInMetresPerSecond = 0.02f;

        /// <summary>How long a wall is remembered after it was last touched, seconds: longer than a stride.</summary>
        public const float ForgetSeconds = 1.2f;

        /// <summary>A body this far clear of a remembered wall has walked away from it, metres on a body of the player's size.</summary>
        public const float ClearOf = 0.3f;

        /// <summary>Two contacts whose ways out are this alike are the same wall.</summary>
        private const float SameWallDot = 0.94f;

        private struct Wall
        {
            public Vector3 Out;
            public float Behind;
            public float Untouched;
        }

        private readonly List<Wall> _walls = new List<Wall>();

        /// <summary>How many walls are remembered.</summary>
        public int Count => _walls.Count;

        public void Clear()
        {
            _walls.Clear();
        }

        /// <summary>
        /// Forgets the walls now ahead of the body or behind it (a wall's way out within <paramref name="headOnDot"/> of <paramref name="ahead"/>,
        /// either way): only a wall beside the body is held, as only a contact beside it is taken. A wall is remembered as a plane with no end,
        /// and a body that turned past the end of one walked into it: up a stair's flight beside the rail between its two lanes, it turned at
        /// the landing to cross past the rail's end and was held there, then pushed back down the flight (owner, 2026-11-12: "walking up this
        /// staircase is super glitchy and barely works").
        /// </summary>
        public void ForgetAheadAndBehind(Vector3 ahead, float headOnDot)
        {
            ahead.y = 0.0f;
            if (ahead.sqrMagnitude < 1e-8f)
            {
                return;
            }
            ahead.Normalize();
            _walls.RemoveAll(wall => Mathf.Abs(Vector3.Dot(wall.Out, ahead)) > headOnDot);
        }

        /// <summary>
        /// Forgets the walls now beside the body: the other half of <see cref="ForgetAheadAndBehind"/>, for the walls the hands are kept out of
        /// ahead (PlayerLocomotion.ReachClearance), which a body that turns along the wall has stopped reaching into.
        /// </summary>
        public void ForgetBeside(Vector3 ahead, float headOnDot)
        {
            ahead.y = 0.0f;
            if (ahead.sqrMagnitude < 1e-8f)
            {
                return;
            }
            ahead.Normalize();
            _walls.RemoveAll(wall => Mathf.Abs(Vector3.Dot(wall.Out, ahead)) <= headOnDot);
        }

        /// <summary>
        /// Forgets the walls no longer there beside the body (<paramref name="stillBeside"/>, asked with each wall's way out): a body that sidesteps
        /// past a rail's end is let go at once, not held by the rail it has left behind.
        /// </summary>
        public void ForgetUnless(System.Predicate<Vector3> stillBeside)
        {
            _walls.RemoveAll(wall => !stillBeside(wall.Out));
        }

        /// <summary>
        /// With the body's root at <paramref name="root"/> and this frame's contacts (each the way out of a wall and how far), how far it must
        /// go along each remembered wall's way out: written into <paramref name="needs"/>, returning how many.
        /// </summary>
        public int Needs(Vector3 root, Vector3[] contacts, int count, float deltaTime, float scale, Vector3[] needs)
        {
            for (int i = _walls.Count - 1; i >= 0; i--)
            {
                Wall wall = _walls[i];
                wall.Behind -= EasesInMetresPerSecond * scale * deltaTime;
                wall.Untouched += deltaTime;
                if (wall.Untouched > ForgetSeconds || Vector3.Dot(root, wall.Out) - wall.Behind > ClearOf * scale)
                {
                    _walls.RemoveAt(i);
                    continue;
                }
                _walls[i] = wall;
            }
            for (int c = 0; c < count; c++)
            {
                Vector3 way = contacts[c].normalized;
                float behind = Vector3.Dot(root, way) + contacts[c].magnitude;
                int same = -1;
                for (int i = 0; i < _walls.Count; i++)
                {
                    if (Vector3.Dot(_walls[i].Out, way) > SameWallDot)
                    {
                        same = i;
                        break;
                    }
                }
                if (same < 0)
                {
                    _walls.Add(new Wall { Out = way, Behind = behind });
                }
                else
                {
                    Wall wall = _walls[same];
                    wall.Behind = Mathf.Max(wall.Behind, behind);
                    wall.Untouched = 0.0f;
                    _walls[same] = wall;
                }
            }
            int written = 0;
            for (int i = 0; i < _walls.Count && written < needs.Length; i++)
            {
                float need = _walls[i].Behind - Vector3.Dot(root, _walls[i].Out);
                if (need > 0.0f)
                {
                    needs[written++] = _walls[i].Out * need;
                }
            }
            return written;
        }
    }
}
