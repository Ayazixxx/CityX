using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>One cell of ground (<see cref="FireRules.CellMetres"/> square) that can hold gasoline and burn.</summary>
    public sealed class FireCell
    {
        public readonly int Column;
        public readonly int Row;

        /// <summary>The floor at the cell's middle.</summary>
        public readonly Vector3 Centre;

        /// <summary>Gasoline on it now (it burns off with the fuel).</summary>
        public float Litres;

        /// <summary>Seconds of burning left, and how many it had when lit.</summary>
        public float FuelSeconds;

        public float FuelAtIgnition;

        /// <summary>How high its fire is, 0..1 (FireRules.HeatAt).</summary>
        public float Heat;

        /// <summary>Seconds since it was lit.</summary>
        public float Age;

        public bool Burning;

        /// <summary>Burned out: the black spot. Fresh gasoline on it makes it fuel again.</summary>
        public bool Burned;

        /// <summary>Lit as a gasoline cell (it spreads) or as bare ground (it does not).</summary>
        public bool Gasoline;

        /// <summary>Lit by another machine's event: it shows, and burns nobody here.</summary>
        public bool Cosmetic;

        /// <summary>Who lit it (a FireScene id; 0 for nobody): their blows, when it burns someone.</summary>
        public int Lighter;

        /// <summary>Doused: too wet to catch while above zero.</summary>
        public float WetSeconds;

        /// <summary>The rain on it when the fire last looked.</summary>
        public float Rain;

        public FireCell(int column, int row, Vector3 centre)
        {
            Column = column;
            Row = row;
            Centre = centre;
        }

        public bool HasGasoline => Litres >= FireRules.MinLitres;

        /// <summary>How much of its fuel has burned, 0..1.</summary>
        public float BurnedShare => FuelAtIgnition > 0.0f ? Mathf.Clamp01(1.0f - FuelSeconds / FuelAtIgnition) : (Burned ? 1.0f : 0.0f);
    }

    /// <summary>
    /// THE FIRE ON THE GROUND, with no scene (docs/FIRE.md): a sparse grid of cells keyed by column and floor height (a roof and the street
    /// under it are different cells), gasoline poured onto them, fire lit on them, spreading from gasoline cells to their neighbours, burning
    /// its fuel off and leaving burned cells. The floor is asked for through a probe (a ray down, in the scene; a flat function in tests), the
    /// rain and the water through two more. Whoever stands in it is the scene's business (FireScene): this only says which cell burns where.
    /// </summary>
    public sealed class FireField
    {
        /// <summary>The floor under a column near a height: false where there is none within reach (off a roof's edge, over nothing).</summary>
        public delegate bool FloorProbe(float x, float z, float nearY, out float floorY);

        private static readonly (int dx, int dz)[] Neighbours =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
        };

        private readonly Dictionary<(int, int), List<FireCell>> _columns = new Dictionary<(int, int), List<FireCell>>();
        private readonly List<FireCell> _cells = new List<FireCell>();
        private readonly List<FireCell> _burning = new List<FireCell>();
        private readonly System.Random _random;
        private readonly FloorProbe _floor;
        private readonly Func<Vector3, float> _rainAt;
        private readonly Func<Vector3, bool> _underWater;
        private float _spreadDue;

        /// <summary>A cell's gasoline changed.</summary>
        public event Action<FireCell> Poured;

        public event Action<FireCell> Ignited;

        public event Action<FireCell> BurnedOut;

        public event Action<FireCell> Doused;

        public FireField(System.Random random, FloorProbe floor, Func<Vector3, float> rainAt = null, Func<Vector3, bool> underWater = null)
        {
            _random = random ?? new System.Random();
            _floor = floor;
            _rainAt = rainAt;
            _underWater = underWater;
        }

        public IReadOnlyList<FireCell> Burning => _burning;

        public IReadOnlyList<FireCell> Cells => _cells;

        public int BurningCount => _burning.Count;

        /// <summary>How many cells have ever been lit.</summary>
        public int EverLit { get; private set; }

        public static int Index(float metres) => Mathf.FloorToInt(metres / FireRules.CellMetres);

        public static float Middle(int index) => (index + 0.5f) * FireRules.CellMetres;

        /// <summary>Pours <paramref name="litres"/> onto the cell under <paramref name="point"/>. False where there is no floor to take it.</summary>
        public bool Pour(Vector3 point, float litres)
        {
            FireCell cell = CellAt(Index(point.x), Index(point.z), point.y, FireRules.StepMetres, true);
            if (cell == null || litres <= 0.0f)
            {
                return false;
            }
            Wet(cell, litres);
            float spill = cell.Litres - FireRules.MaxLitres;
            if (spill > 0.0f)
            {
                cell.Litres = FireRules.MaxLitres;
                for (int i = 0; i < 4; i++)
                {
                    FireCell beside = CellAt(cell.Column + Neighbours[i].dx, cell.Row + Neighbours[i].dz, cell.Centre.y, FireRules.StepMetres, true);
                    if (beside != null)
                    {
                        Wet(beside, spill * 0.25f);
                        beside.Litres = Mathf.Min(beside.Litres, FireRules.MaxLitres);
                    }
                }
            }
            return true;
        }

        private void Wet(FireCell cell, float litres)
        {
            cell.Litres += litres;
            cell.Burned = false;   // fresh fuel on a burned spot
            if (cell.Burning)
            {
                // Poured onto a fire: it feeds it.
                cell.Gasoline = true;
                cell.FuelSeconds = Mathf.Max(cell.FuelSeconds, FireRules.FuelSecondsFor(cell.Litres));
                cell.FuelAtIgnition = Mathf.Max(cell.FuelAtIgnition, cell.FuelSeconds);
            }
            Poured?.Invoke(cell);
        }

        /// <summary>
        /// Lights the cells within <paramref name="radius"/> of <paramref name="point"/>: those with gasoline, and, unless
        /// <paramref name="onlyFuel"/>, the bare ground too (which burns briefly). Returns how many caught.
        /// </summary>
        public int Ignite(Vector3 point, float radius, bool onlyFuel, bool cosmetic = false, int lighter = 0)
        {
            int lit = 0;
            float reach = radius + FireRules.CellMetres * 0.5f;
            int c0 = Index(point.x - reach), c1 = Index(point.x + reach);
            int r0 = Index(point.z - reach), r1 = Index(point.z + reach);
            for (int c = c0; c <= c1; c++)
            {
                for (int r = r0; r <= r1; r++)
                {
                    float dx = Middle(c) - point.x, dz = Middle(r) - point.z;
                    if (dx * dx + dz * dz > reach * reach)
                    {
                        continue;
                    }
                    FireCell cell = CellAt(c, r, point.y, FireRules.BodyReachMetres, !onlyFuel);
                    if (cell == null)
                    {
                        continue;
                    }
                    if (cell.Burning)
                    {
                        if (cell.Cosmetic && !cosmetic)
                        {
                            cell.Cosmetic = false;   // this machine's own fire now: it burns, and counts as lit (other players are told)
                            cell.Lighter = lighter;
                            lit++;
                        }
                        continue;
                    }
                    if (cell.WetSeconds > 0.0f || (_underWater != null && _underWater(cell.Centre)))
                    {
                        continue;
                    }
                    if (!cell.HasGasoline && (onlyFuel || cell.Burned))
                    {
                        continue;
                    }
                    if (!cell.HasGasoline && _rainAt != null && _rainAt(cell.Centre) > FireRules.RainThatStopsBareFire)
                    {
                        continue;   // bare pavement does not catch in a downpour
                    }
                    if (Light(cell, cosmetic, lighter))
                    {
                        lit++;
                    }
                }
            }
            return lit;
        }

        private bool Light(FireCell cell, bool cosmetic, int lighter)
        {
            if (_burning.Count >= FireRules.MaxBurningCells)
            {
                return false;
            }
            cell.Burning = true;
            cell.Gasoline = cell.HasGasoline;
            cell.FuelSeconds = cell.Gasoline ? FireRules.FuelSecondsFor(cell.Litres) : FireRules.BareFuelSeconds;
            cell.FuelAtIgnition = cell.FuelSeconds;
            cell.Heat = 0.0f;
            cell.Age = 0.0f;
            cell.Cosmetic = cosmetic;
            cell.Lighter = lighter;
            _burning.Add(cell);
            EverLit++;
            Ignited?.Invoke(cell);
            return true;
        }

        /// <summary>Puts out the fire within <paramref name="radius"/> of <paramref name="point"/> and leaves the ground there too wet to catch for a while.</summary>
        public int Douse(Vector3 point, float radius)
        {
            int doused = 0;
            float reach = radius + FireRules.CellMetres * 0.5f;
            int c0 = Index(point.x - reach), c1 = Index(point.x + reach);
            int r0 = Index(point.z - reach), r1 = Index(point.z + reach);
            for (int c = c0; c <= c1; c++)
            {
                for (int r = r0; r <= r1; r++)
                {
                    float dx = Middle(c) - point.x, dz = Middle(r) - point.z;
                    if (dx * dx + dz * dz > reach * reach || !_columns.TryGetValue((c, r), out List<FireCell> stack))
                    {
                        continue;
                    }
                    foreach (FireCell cell in stack)
                    {
                        if (Mathf.Abs(cell.Centre.y - point.y) > FireRules.ProbeReachMetres)
                        {
                            continue;
                        }
                        cell.WetSeconds = FireRules.WetSeconds;
                        if (cell.Burning)
                        {
                            PutOut(cell);
                            doused++;
                        }
                    }
                }
            }
            return doused;
        }

        private void PutOut(FireCell cell)
        {
            // What has not burned off is still on the ground, to be lit again once it dries.
            cell.Litres *= cell.FuelAtIgnition > 0.0f ? Mathf.Clamp01(cell.FuelSeconds / cell.FuelAtIgnition) : 0.0f;
            cell.Burning = false;
            cell.Heat = 0.0f;
            _burning.Remove(cell);
            Doused?.Invoke(cell);
        }

        /// <summary>Advances the fire: fuel burns off, flames rise and die, cells light their neighbours on the spread ticks.</summary>
        public void Tick(float deltaTime)
        {
            for (int i = _burning.Count - 1; i >= 0; i--)
            {
                FireCell cell = _burning[i];
                cell.Age += deltaTime;
                cell.FuelSeconds -= deltaTime * FireRules.BurnFactorIn(cell.Rain);
                if (cell.Gasoline && cell.FuelAtIgnition > 0.0f)
                {
                    cell.Litres = Mathf.Min(cell.Litres, FireRules.MaxLitres * Mathf.Clamp01(cell.FuelSeconds / cell.FuelAtIgnition));
                }
                cell.Heat = FireRules.HeatAt(cell.Age, cell.FuelSeconds, cell.Gasoline);
                if (cell.FuelSeconds <= 0.0f)
                {
                    BurnOut(cell, i);
                }
            }
            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i].WetSeconds > 0.0f)
                {
                    _cells[i].WetSeconds -= deltaTime;
                }
            }
            _spreadDue += deltaTime;
            while (_spreadDue >= FireRules.SpreadTickSeconds)
            {
                _spreadDue -= FireRules.SpreadTickSeconds;
                Spread(FireRules.SpreadTickSeconds);
            }
        }

        private void BurnOut(FireCell cell, int index)
        {
            cell.Burning = false;
            cell.Burned = true;
            cell.Heat = 0.0f;
            cell.Litres = 0.0f;
            cell.FuelSeconds = 0.0f;
            _burning.RemoveAt(index);
            BurnedOut?.Invoke(cell);
        }

        /// <summary>One look round: each burning gasoline cell may light its eight neighbours; water that has risen over a cell puts it out.</summary>
        private void Spread(float seconds)
        {
            int count = _burning.Count;   // cells lit below join the next look
            for (int i = count - 1; i >= 0; i--)
            {
                FireCell cell = _burning[i];
                cell.Rain = _rainAt != null ? Mathf.Clamp01(_rainAt(cell.Centre)) : 0.0f;
                if (_underWater != null && _underWater(cell.Centre))
                {
                    PutOut(cell);
                    continue;
                }
                if (!cell.Gasoline || cell.Heat < FireRules.SpreadHeat)
                {
                    continue;
                }
                foreach ((int dx, int dz) in Neighbours)
                {
                    FireCell beside = CellAt(cell.Column + dx, cell.Row + dz, cell.Centre.y, FireRules.StepMetres, false);
                    if (beside != null && (beside.Burning || beside.Burned || beside.WetSeconds > 0.0f))
                    {
                        continue;
                    }
                    bool gasoline = beside != null && beside.HasGasoline;
                    if (!gasoline && cell.Rain > FireRules.RainThatStopsBareFire)
                    {
                        continue;
                    }
                    if (_random.NextDouble() >= FireRules.CatchChance(cell.Heat, gasoline, seconds))
                    {
                        continue;
                    }
                    if (_burning.Count >= FireRules.MaxBurningCells)
                    {
                        return;
                    }
                    FireCell target = beside ?? CellAt(cell.Column + dx, cell.Row + dz, cell.Centre.y, FireRules.StepMetres, true);
                    if (target == null || target.Burning || target.Burned || (_underWater != null && _underWater(target.Centre)))
                    {
                        continue;   // no floor there (a roof's edge), or water
                    }
                    Light(target, cell.Cosmetic, cell.Lighter);
                }
            }
        }

        /// <summary>The cell under a point (a body's feet, a stream's landing), if there is one, lit or not.</summary>
        public bool TryCellUnder(Vector3 point, out FireCell cell)
        {
            cell = CellAt(Index(point.x), Index(point.z), point.y, FireRules.BodyReachMetres, false);
            return cell != null;
        }

        /// <summary>The burning cell under a point, if there is one.</summary>
        public bool TryBurningUnder(Vector3 point, out FireCell cell)
        {
            return TryCellUnder(point, out cell) && cell.Burning;
        }

        /// <summary>The gasoline on the cell under a point (none where there is no cell).</summary>
        public float LitresUnder(Vector3 point)
        {
            return TryCellUnder(point, out FireCell cell) ? cell.Litres : 0.0f;
        }

        public int CountBurned()
        {
            int burned = 0;
            foreach (FireCell cell in _cells)
            {
                if (cell.Burned)
                {
                    burned++;
                }
            }
            return burned;
        }

        /// <summary>The whole field's extent of burning cells (for finding the bodies in it), false with none burning.</summary>
        public bool TryBurningBounds(out Bounds bounds, bool includeCosmetic)
        {
            bounds = default;
            bool any = false;
            foreach (FireCell cell in _burning)
            {
                if (cell.Cosmetic && !includeCosmetic)
                {
                    continue;
                }
                if (!any)
                {
                    bounds = new Bounds(cell.Centre, Vector3.zero);
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(cell.Centre);
                }
            }
            return any;
        }

        public void Clear()
        {
            _columns.Clear();
            _cells.Clear();
            _burning.Clear();
            _spreadDue = 0.0f;
        }

        /// <summary>
        /// The cell of this column within <paramref name="band"/> of a height, or, when asked to make one, a new cell on the floor the probe
        /// finds near that height (none where the probe finds no floor within the band).
        /// </summary>
        private FireCell CellAt(int column, int row, float nearY, float band, bool make)
        {
            if (_columns.TryGetValue((column, row), out List<FireCell> stack))
            {
                FireCell nearest = null;
                float best = band;
                foreach (FireCell cell in stack)
                {
                    float apart = Mathf.Abs(cell.Centre.y - nearY);
                    if (apart <= best)
                    {
                        best = apart;
                        nearest = cell;
                    }
                }
                if (nearest != null)
                {
                    return nearest;
                }
            }
            if (!make || _floor == null)
            {
                return null;
            }
            float x = Middle(column), z = Middle(row);
            if (!_floor(x, z, nearY, out float floorY) || Mathf.Abs(floorY - nearY) > band)
            {
                return null;
            }
            var made = new FireCell(column, row, new Vector3(x, floorY, z));
            if (stack == null)
            {
                stack = new List<FireCell>(1);
                _columns[(column, row)] = stack;
            }
            stack.Add(made);
            _cells.Add(made);
            return made;
        }
    }
}
