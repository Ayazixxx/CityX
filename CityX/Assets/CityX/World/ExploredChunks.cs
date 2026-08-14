using System.Collections.Generic;
using UnityEngine;

namespace CityX.World
{
    /// <summary>
    /// Which chunks the player has actually been to -- Minecraft-style fog of
    /// war for the map panel. Ported from city.gd's own _explored_chunks.
    ///
    /// PURE BOOKKEEPING AROUND A HASHSET, kept out of CityStreamer entirely so
    /// "does marking a centre also mark the ring around it" and "does standing
    /// still stop rewriting the same keys" are things a test can check without
    /// building a single chunk mesh.
    ///
    /// NOT PERSISTED, matching Godot's own comment on _explored_chunks: this
    /// resets when the session ends rather than becoming a character_save.gd
    /// field.
    /// </summary>
    public sealed class ExploredChunks
    {
        /// <summary>Godot's own EXPLORE_RADIUS -- a 3x3 block around wherever the player actually stood.</summary>
        public const int Radius = 1;

        private readonly HashSet<Vector2Int> _chunks = new HashSet<Vector2Int>();
        private Vector2Int _lastCentre;
        private bool _hasCentre;

        public int Count => _chunks.Count;

        public bool Contains(Vector2Int coord) => _chunks.Contains(coord);

        /// <summary>
        /// A snapshot, not a live view -- mirrors city.gd's own explored_chunks()
        /// returning a fresh Array so a caller drawing the map can't be tripped
        /// up by the player crossing a chunk boundary mid-loop.
        /// </summary>
        public Vector2Int[] ToArray()
        {
            var array = new Vector2Int[_chunks.Count];
            _chunks.CopyTo(array);
            return array;
        }

        /// <summary>
        /// Marks <paramref name="centre"/> and everything within Radius of it
        /// as seen. A NO-OP if centre is the same one last marked -- Godot's
        /// own reasoning: avoids rewriting the same handful of keys sixty
        /// times a second while standing still. Call this only when the
        /// player's own chunk actually changes, same as city.gd's own
        /// _physics_process does.
        /// </summary>
        public void Mark(Vector2Int centre)
        {
            if (_hasCentre && centre == _lastCentre)
            {
                return;
            }
            _hasCentre = true;
            _lastCentre = centre;

            for (int dz = -Radius; dz <= Radius; dz++)
            {
                for (int dx = -Radius; dx <= Radius; dx++)
                {
                    _chunks.Add(new Vector2Int(centre.x + dx, centre.y + dz));
                }
            }
        }

        /// <summary>Drops everything explored. For a world-seed change, where the old coordinates mean nothing.</summary>
        public void Clear()
        {
            _chunks.Clear();
            _hasCentre = false;
        }
    }
}
