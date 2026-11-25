using System;
using System.Collections.Generic;
using CityX.Core;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>
    /// WHERE A PRINTED DESIGN IS FOUND (docs/HEAT_PRESS.md; owner, 2026-11-24: "make it use images off your computer and visible across computers").
    /// A shirt carries only its design's id; whatever shows the shirt asks here for the design's bytes. This machine's own store
    /// (<see cref="DesignStore.Local"/>) answers at once; a design it does not have is asked of the session's server (<see cref="DesignNetwork"/>),
    /// and every caller waiting on it is answered when it arrives (and it is kept here, so it is never fetched twice). A design pressed here is
    /// kept here and sent to the server (<see cref="Publish"/>), which keeps it for everyone, for good.
    /// </summary>
    public static class DesignLibrary
    {
        /// <summary>The store used (this machine's own; a test sets its own folder).</summary>
        public static DesignStore Store
        {
            get => _store ?? DesignStore.Local;
            set => _store = value;
        }

        private static DesignStore _store;

        private static readonly Dictionary<string, List<Action<byte[]>>> Waiting = new Dictionary<string, List<Action<byte[]>>>();

        /// <summary>Designs already read off disk this run, so a crowd in the same shirt reads it once.</summary>
        private static readonly Dictionary<string, byte[]> Read = new Dictionary<string, byte[]>();

        /// <summary>Whether a design is here now.</summary>
        public static bool Has(string designId) => Read.ContainsKey(designId ?? string.Empty) || Store.Has(designId);

        /// <summary>
        /// Calls <paramref name="ready"/> with the design's bytes: now when it is here, later when the server has sent it. Never called for an id
        /// that is not one, or a design nobody has.
        /// </summary>
        public static void Get(string designId, Action<byte[]> ready)
        {
            if (!DesignStore.IsValidId(designId) || ready == null)
            {
                return;
            }
            if (TryGet(designId, out byte[] blob))
            {
                ready(blob);
                return;
            }
            if (!Waiting.TryGetValue(designId, out List<Action<byte[]>> callers))
            {
                Waiting[designId] = callers = new List<Action<byte[]>>();
                DesignNetwork.Active?.Request(designId);
            }
            callers.Add(ready);
        }

        /// <summary>The design, if it is here now.</summary>
        public static bool TryGet(string designId, out byte[] blob)
        {
            if (Read.TryGetValue(designId ?? string.Empty, out blob))
            {
                return true;
            }
            if (Store.TryLoad(designId, out blob))
            {
                Read[designId] = blob;
                return true;
            }
            return false;
        }

        /// <summary>A design made here: kept here, sent to the server for everyone. Its id, or null when it is not a sane design.</summary>
        public static string Publish(byte[] blob)
        {
            string id = Store.Keep(blob);
            if (id != null)
            {
                Read[id] = blob;
                DesignNetwork.Active?.Upload(id, blob);
            }
            return id;
        }

        /// <summary>Makes sure the server has a design this machine shows on its own player (pressed offline, then worn into a session).</summary>
        public static void Share(string designId)
        {
            if (DesignNetwork.Active != null && TryGet(designId, out byte[] blob))
            {
                DesignNetwork.Active.Upload(designId, blob);
            }
        }

        /// <summary>A design arrived from the server: kept (when it is what its id says), and everyone waiting on it answered.</summary>
        public static void Arrived(string designId, byte[] blob)
        {
            if (!DesignStore.IsValidId(designId) || blob == null || DesignStore.IdOf(blob) != designId || Store.Keep(blob) == null)
            {
                return;
            }
            Read[designId] = blob;
            if (!Waiting.TryGetValue(designId, out List<Action<byte[]>> callers))
            {
                return;
            }
            Waiting.Remove(designId);
            foreach (Action<byte[]> ready in callers)
            {
                try
                {
                    ready(blob);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);   // one broken caller must not keep the design from the rest
                }
            }
        }

        /// <summary>Designs still being waited on (asked again when the server had not got one yet).</summary>
        public static IEnumerable<string> StillWaiting => Waiting.Keys;

        /// <summary>Forgets what was read and who waits (a test's fresh start; the store on disk is kept).</summary>
        public static void Reset()
        {
            Waiting.Clear();
            Read.Clear();
        }
    }
}
