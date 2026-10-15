using System.Collections.Generic;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHERE TO HIDE: a spot near a fighter that a threat cannot see or shoot, and a way to run there. Rings of candidate spots round the
    /// fighter are tried, nearest first; a spot counts when the lines from the threat's eye to a crouching body there (its head and its chest)
    /// both meet something solid, and when it does not take the fighter much closer to the threat. It is reached in a straight line, or --
    /// the usual way in a city, round the corner of a building -- by way of one open spot on the inner rings that both legs are clear from
    /// (<see cref="CoverSearch.Via"/>). The nearest wins, with a preference for spots tucked in close behind what hides them. The world is
    /// asked through two questions (<see cref="Blocked"/>, <see cref="Ground"/>), so a test can answer them itself; the game answers them with
    /// rays.
    ///
    /// A SEARCH IS SPREAD OVER FRAMES (<see cref="CoverSearch.Step"/>): in the real city one ran to some 400 rays, about 4 ms, in a single
    /// frame -- a hitch every time a Shoota was hurt. A few dozen a frame finish it in a fraction of a second, which reads as him taking a
    /// moment to look round.
    /// </summary>
    public static class CoverPicker
    {
        /// <summary>True when the straight line from one point to another meets something solid (bodies do not count).</summary>
        public delegate bool Blocked(Vector3 from, Vector3 to);

        /// <summary>True, with the point, when there is ground under <paramref name="above"/>.</summary>
        public delegate bool Ground(Vector3 above, out Vector3 point);

        /// <summary>The rings tried, metres from the fighter, and how many spots round each.</summary>
        public static readonly float[] Rings = { 2.5f, 4.5f, 7.0f, 10.0f, 14.0f, 20.0f };

        public const int Directions = 16;

        /// <summary>How many of the rings, from the inside, give the open spots a path may turn at.</summary>
        public const int WaypointRings = 3;

        /// <summary>How many hidden spots with no straight way to them are tried for a way round (nearest first).</summary>
        public const int MaxDetours = 8;

        /// <summary>A spot more than this above or below the fighter is not one to run to (a roof, a pit).</summary>
        public const float MaxClimbMetres = 1.5f;

        /// <summary>How far up a spot reached by a way round must be clear to count as outdoors, metres.</summary>
        public const float OpenSkyMetres = 8.0f;

        /// <summary>A spot may take the fighter at most this much closer to the threat.</summary>
        public const float MaxCloserMetres = 1.5f;

        /// <summary>The most questions one search may ask the world in all: a cap on its cost, whatever the city round it.</summary>
        public const int MaxQueries = 1200;

        /// <summary>How many questions the last search finished by <see cref="TryPick(Vector3, float, Vector3, Blocked, Ground, out Vector3, out Vector3)"/> asked.</summary>
        public static int LastQueries { get; private set; }

        /// <summary>Picks cover, reached in a straight line only (see the other overload).</summary>
        public static bool TryPick(Vector3 self, float height, Vector3 threatEye, Blocked blocked, Ground ground, out Vector3 cover)
        {
            bool found = TryPick(self, height, threatEye, blocked, ground, out cover, out Vector3 via);
            return found && via == cover;
        }

        /// <summary>A whole search at once (tests; the game steps a <see cref="CoverSearch"/> a little each frame).</summary>
        public static bool TryPick(Vector3 self, float height, Vector3 threatEye, Blocked blocked, Ground ground, out Vector3 cover, out Vector3 via)
        {
            var search = new CoverSearch();
            search.Begin(self, height, threatEye, blocked, ground);
            while (!search.Step(MaxQueries))
            {
            }
            LastQueries = search.Queries;
            cover = search.Cover;
            via = search.Via;
            return search.Found;
        }

        /// <summary>Whether a fighter at <paramref name="feet"/> is hidden from <paramref name="threatEye"/> crouching there.</summary>
        public static bool IsHidden(Vector3 feet, float height, Vector3 threatEye, Blocked blocked)
        {
            return blocked != null && blocked(threatEye, feet + Vector3.up * (height * 0.55f)) && blocked(threatEye, feet + Vector3.up * (height * 0.35f));
        }

        internal static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0.0f, v.z);
    }

    /// <summary>One search for cover (see <see cref="CoverPicker"/>), stepped a few questions at a time.</summary>
    public sealed class CoverSearch
    {
        private struct Spot
        {
            public Vector3 Point;
            public float Radius;
        }

        private readonly List<Spot> _open = new List<Spot>();
        private readonly List<Spot> _detours = new List<Spot>();
        private Vector3 _self;
        private float _height;
        private Vector3 _threatEye;
        private CoverPicker.Blocked _blocked;
        private CoverPicker.Ground _ground;
        private float _nowToThreat;
        private float _bestScore;
        private int _ring;
        private int _direction;
        private int _detour;
        private int _turn;
        private bool _ringsDone;

        /// <summary>A search is under way.</summary>
        public bool Running { get; private set; }

        public bool Found { get; private set; }

        public Vector3 Cover { get; private set; }

        /// <summary>Where to run first: the cover itself for a straight run, or the corner to turn at.</summary>
        public Vector3 Via { get; private set; }

        /// <summary>The questions asked so far.</summary>
        public int Queries { get; private set; }

        /// <summary>The open spots a way round may turn at, and the hidden spots tried for a way round (for tests and the capture's log).</summary>
        public int OpenSpots => _open.Count;

        public int DetourSpots => _detours.Count;

        public void Begin(Vector3 self, float height, Vector3 threatEye, CoverPicker.Blocked blocked, CoverPicker.Ground ground)
        {
            _self = self;
            _height = height;
            _threatEye = threatEye;
            _blocked = blocked;
            _ground = ground;
            _nowToThreat = CoverPicker.Flat(self - threatEye).magnitude;
            _bestScore = float.MaxValue;
            _ring = 0;
            _direction = 0;
            _detour = 0;
            _turn = 0;
            _ringsDone = false;
            _open.Clear();
            _detours.Clear();
            Found = false;
            Cover = self;
            Via = self;
            Queries = 0;
            Running = blocked != null && ground != null;
        }

        public void Cancel()
        {
            Running = false;
        }

        /// <summary>Asks up to about <paramref name="budget"/> more questions; true once the search is over (look at <see cref="Found"/>).</summary>
        public bool Step(int budget)
        {
            if (!Running)
            {
                return true;
            }
            int stop = Queries + Mathf.Max(1, budget);
            while (Queries < stop)
            {
                if (Queries >= CoverPicker.MaxQueries || (_ringsDone ? !NextDetour() : !NextProbe()))
                {
                    Running = false;
                    return true;
                }
            }
            return false;
        }

        private Vector3 Knee => Vector3.up * (_height * 0.2f);

        private Vector3 Chest => Vector3.up * (_height * 0.5f);

        private Vector3 CrouchedHead => Vector3.up * (_height * 0.55f);

        private Vector3 CrouchedChest => Vector3.up * (_height * 0.35f);

        private bool Ask(Vector3 from, Vector3 to)
        {
            Queries++;
            return _blocked(from, to);
        }

        /// <summary>One candidate spot on the rings; false when the rings are done (then the detours start, from the next step).</summary>
        private bool NextProbe()
        {
            if (_ring >= CoverPicker.Rings.Length)
            {
                _ringsDone = true;
                return true;
            }
            float radius = CoverPicker.Rings[_ring];
            float angle = (_direction + (_ring % 2) * 0.5f) / CoverPicker.Directions * Mathf.PI * 2.0f;
            int ring = _ring;
            if (++_direction >= CoverPicker.Directions)
            {
                _direction = 0;
                _ring++;
                // Tucked cover on this ring: nothing further out can beat it.
                if (Found && _bestScore <= radius)
                {
                    _ring = CoverPicker.Rings.Length;
                }
            }

            Vector3 probe = _self + new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * radius;
            Queries++;
            if (!_ground(probe + Vector3.up * (CoverPicker.MaxClimbMetres + 1.0f), out Vector3 spot) || Mathf.Abs(spot.y - _self.y) > CoverPicker.MaxClimbMetres)
            {
                return true;
            }
            float thereToThreat = CoverPicker.Flat(spot - _threatEye).magnitude;
            bool closer = thereToThreat < _nowToThreat - CoverPicker.MaxCloserMetres;
            bool hidden = !closer && Ask(_threatEye, spot + CrouchedHead) && Ask(_threatEye, spot + CrouchedChest);
            // Whether it can be run to straight: asked of every inner spot (a path may turn there) and of every hidden one.
            bool straight = (hidden || ring < CoverPicker.WaypointRings) && !Ask(_self + Knee, spot + Knee) && !Ask(_self + Chest, spot + Chest);
            if (straight && ring < CoverPicker.WaypointRings)
            {
                _open.Add(new Spot { Point = spot, Radius = radius });
            }
            if (!hidden)
            {
                return true;   // seen from there (or toward the threat)
            }
            if (!straight)
            {
                // Worth a way round only under the open sky: a hidden spot with a ceiling over it is nearly always inside a building (the
                // ground floor's own floor under it, its walls round it), with no way in -- and those used to fill every detour.
                if (_detours.Count < CoverPicker.MaxDetours && !Ask(spot + Vector3.up * 0.3f, spot + Vector3.up * CoverPicker.OpenSkyMetres))
                {
                    _detours.Add(new Spot { Point = spot, Radius = radius });
                }
                return true;
            }
            float score = radius + Untucked(spot);
            if (score < _bestScore)
            {
                _bestScore = score;
                Cover = spot;
                Via = spot;
                Found = true;
            }
            return true;
        }

        /// <summary>One turn tried for one hidden spot with no straight way to it; false when every detour is done.</summary>
        private bool NextDetour()
        {
            while (_detour < _detours.Count)
            {
                Spot detour = _detours[_detour];
                if (detour.Radius >= _bestScore || _turn >= _open.Count)
                {
                    _detour++;
                    _turn = 0;
                    continue;
                }
                Spot turn = _open[_turn++];
                float length = turn.Radius + Vector3.Distance(CoverPicker.Flat(turn.Point), CoverPicker.Flat(detour.Point));
                if (length >= _bestScore)
                {
                    continue;
                }
                if (Ask(turn.Point + Knee, detour.Point + Knee) || Ask(turn.Point + Chest, detour.Point + Chest))
                {
                    return true;
                }
                float score = length + Untucked(detour.Point);
                if (score < _bestScore)
                {
                    _bestScore = score;
                    Cover = detour.Point;
                    Via = turn.Point;
                    Found = true;
                }
                return true;
            }
            return false;
        }

        /// <summary>0 when one step toward the threat from the spot meets what hides it (tucked in), 3 when it does not.</summary>
        private float Untucked(Vector3 spot)
        {
            Vector3 towardThreat = CoverPicker.Flat(_threatEye - spot).normalized;
            return Ask(spot + CrouchedChest, spot + CrouchedChest + towardThreat * 1.5f) ? 0.0f : 3.0f;
        }
    }
}
