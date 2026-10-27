using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace CityX.Character
{
    /// <summary>One piece of graffiti: where it is on which wall, how big, and its paint (GraffitiCanvas, encoded).</summary>
    public struct GraffitiPiece
    {
        public int Id;
        public Vector3 Centre;
        public Vector3 Normal;
        public float Size;
        public byte[] Paint;
    }

    /// <summary>
    /// EVERY PIECE OF GRAFFITI ON THIS MACHINE, and the one place they are put on walls (owner, 2026-10-15: "have the drawing stay on that wall
    /// and visible to other players online. it doesnt need to stay after log off right now ... but plan for that in the future"). A piece is a
    /// <see cref="GraffitiPiece"/> -- plain numbers and the paint's bytes -- so the same thing goes to other players (GraffitiNetwork) and,
    /// later, to a save: persistence is writing <see cref="All"/> out and calling <see cref="Put"/> for each piece read back.
    ///
    /// A piece is shown by a <see cref="GraffitiPanel"/>: a quad laid on the wall a centimetre proud of it, lit like the wall (URP's Lit
    /// shader: the sun, the street lights and the room lights all fall on it, shadows too).
    /// </summary>
    public static class GraffitiWall
    {
        /// <summary>A piece's side, metres: about a storey of this city's 2.6 m people.</summary>
        public const float PieceSize = 2.6f;

        /// <summary>How far proud of the wall a piece is laid, metres.</summary>
        public const float Proud = 0.012f;

        /// <summary>A new piece this close to an old one on the same wall paints over the old one rather than beside it, metres.</summary>
        public const float SameSpotMetres = 0.9f;

        /// <summary>The material pieces are drawn with (set by the scene: GraffitiNetwork.PaintMaterial); a plain one is made if none is.</summary>
        public static Material PaintMaterial;

        private static readonly Dictionary<int, GraffitiPanel> Panels = new Dictionary<int, GraffitiPanel>();

        /// <summary>Raised when a piece is put up or changed on this machine (for tests).</summary>
        public static event System.Action<GraffitiPiece> Changed;

        public static int Count => Panels.Count;

        /// <summary>A stable id for a spot: the same wall spot always gets the same id, on every machine.</summary>
        public static int IdFor(Vector3 centre, Vector3 normal)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + Mathf.RoundToInt(centre.x * 10.0f);
                h = h * 31 + Mathf.RoundToInt(centre.y * 10.0f);
                h = h * 31 + Mathf.RoundToInt(centre.z * 10.0f);
                h = h * 31 + Mathf.RoundToInt(normal.x * 8.0f);
                h = h * 31 + Mathf.RoundToInt(normal.z * 8.0f);
                return h;
            }
        }

        /// <summary>The panel of the piece already on the wall at a spot (same wall, within SameSpotMetres), if any.</summary>
        public static bool TryFindNear(Vector3 point, Vector3 normal, out GraffitiPanel found)
        {
            found = null;
            float best = float.MaxValue;
            foreach (GraffitiPanel panel in Panels.Values)
            {
                if (panel == null)
                {
                    continue;
                }
                GraffitiPiece p = panel.Piece;
                Vector3 offset = point - p.Centre;
                float across = Vector3.ProjectOnPlane(offset, p.Normal).magnitude;
                if (Vector3.Dot(p.Normal.normalized, normal.normalized) > 0.95f && Mathf.Abs(Vector3.Dot(offset, p.Normal.normalized)) < 0.3f
                    && across < SameSpotMetres && across < best)
                {
                    best = across;
                    found = panel;
                }
            }
            return found != null;
        }

        /// <summary>The panel showing a piece, by its id (null when there is none).</summary>
        public static GraffitiPanel Find(int id) => Panels.TryGetValue(id, out GraffitiPanel panel) ? panel : null;

        /// <summary>Puts a piece up (or repaints the one with its id). False when the piece is not a sane one.</summary>
        public static bool Put(GraffitiPiece piece)
        {
            if (!IsSane(piece))
            {
                return false;
            }
            if (!Panels.TryGetValue(piece.Id, out GraffitiPanel panel) || panel == null)
            {
                panel = GraffitiPanel.Create(piece.Id);
                Panels[piece.Id] = panel;
            }
            if (!panel.Show(piece))
            {
                return false;
            }
            Changed?.Invoke(piece);
            return true;
        }

        /// <summary>Every piece up on this machine (what a joining player is sent; what a save will write).</summary>
        public static List<GraffitiPiece> All()
        {
            var all = new List<GraffitiPiece>(Panels.Count);
            foreach (GraffitiPanel panel in Panels.Values)
            {
                if (panel != null)
                {
                    all.Add(panel.Piece);
                }
            }
            return all;
        }

        /// <summary>Takes every piece down (a session ending, a test).</summary>
        public static void Clear()
        {
            foreach (GraffitiPanel panel in Panels.Values)
            {
                if (panel != null)
                {
                    Object.Destroy(panel.gameObject);
                }
            }
            Panels.Clear();
        }

        /// <summary>The wall mask's rays along a side: one every 2 cm of a 2.6 m piece (two canvas pixels each way).</summary>
        public const int MaskCells = GraffitiCanvas.Size / 2;

        /// <summary>
        /// The mask's rays start this far in front of the wall's face, metres (where the canvas's photo of the wall is taken from), so a pillar,
        /// a pipe or a sign standing out of the wall less than this is seen; a surface within MaskTolerance of the face is the face.
        /// </summary>
        public const float MaskRayStart = 0.6f;

        public const float MaskTolerance = 0.03f;

        /// <summary>A piece's two directions on its wall, as the wall is seen from the front: across (the canvas's +x) and up (its +y).</summary>
        public static Vector3 AcrossOf(Vector3 normal, out Vector3 up)
        {
            normal = normal.normalized;
            up = Vector3.ProjectOnPlane(Vector3.up, normal);
            if (up.sqrMagnitude < 1e-4f)
            {
                up = Vector3.ProjectOnPlane(Vector3.forward, normal);
            }
            up.Normalize();
            return Vector3.Cross(up, -normal);
        }

        /// <summary>
        /// WHERE PAINT CAN STICK on the square a piece would cover (GraffitiCanvas.Mask): a ray every 2 cm from 60 cm in front of the wall back
        /// into it. Where it first meets the wall's own face (within 3 cm of it, facing the same way) paint sticks; nowhere else -- not in the
        /// air past a corner, not on a pillar or a pipe standing out of the wall (the piece would be hidden behind it), not in a window's hole,
        /// not off the edge of a curved wall the flat piece stands away from. People are looked past: the painter's own arm and anyone standing
        /// at the wall do not leave holes. Sixteen thousand rays through the job system (RaycastCommand), once, as the canvas opens.
        /// </summary>
        public static byte[] WallMask(Vector3 centre, Vector3 normal, float size, Transform painter = null)
        {
            normal = normal.normalized;
            Vector3 across = AcrossOf(normal, out Vector3 up);
            const int count = MaskCells * MaskCells;
            var commands = new NativeArray<RaycastCommand>(count, Allocator.TempJob);
            var results = new NativeArray<RaycastHit>(count, Allocator.TempJob);
            try
            {
                var query = new QueryParameters(~0, false, QueryTriggerInteraction.Ignore, false);
                float reach = MaskRayStart + MaskTolerance + 0.05f;
                for (int cy = 0; cy < MaskCells; cy++)
                {
                    for (int cx = 0; cx < MaskCells; cx++)
                    {
                        float u = (cx + 0.5f) / MaskCells - 0.5f;
                        float v = (cy + 0.5f) / MaskCells - 0.5f;
                        Vector3 onFace = centre + across * (u * size) + up * (v * size);
                        commands[cy * MaskCells + cx] = new RaycastCommand(onFace + normal * MaskRayStart, -normal, query, reach);
                    }
                }
                RaycastCommand.ScheduleBatch(commands, results, 256, 1).Complete();

                var mask = new byte[GraffitiCanvas.Size * GraffitiCanvas.Size];
                const int scale = GraffitiCanvas.Size / MaskCells;
                Dictionary<Collider, bool> isPerson = null;
                for (int cy = 0; cy < MaskCells; cy++)
                {
                    for (int cx = 0; cx < MaskCells; cx++)
                    {
                        int cell = cy * MaskCells + cx;
                        RaycastHit hit = results[cell];
                        if (hit.normal == Vector3.zero || hit.distance > MaskRayStart + MaskTolerance)
                        {
                            continue;   // nothing (the air past a corner) or something behind the face (a window's hole)
                        }
                        if (hit.distance < MaskRayStart - MaskTolerance)
                        {
                            // Something in front of the face: a pillar (no paint), or a person (look past them, one ray at a time: few cells).
                            isPerson ??= new Dictionary<Collider, bool>();
                            if (!IsPerson(hit.collider, painter, isPerson) || !FaceBehindPeople(commands[cell], painter, isPerson, out hit))
                            {
                                continue;
                            }
                        }
                        if (Mathf.Abs(hit.distance - MaskRayStart) > MaskTolerance || Vector3.Dot(hit.normal, normal) <= 0.9f)
                        {
                            continue;
                        }
                        for (int y = 0; y < scale; y++)
                        {
                            for (int x = 0; x < scale; x++)
                            {
                                mask[(cy * scale + y) * GraffitiCanvas.Size + cx * scale + x] = 255;
                            }
                        }
                    }
                }
                return mask;
            }
            finally
            {
                commands.Dispose();
                results.Dispose();
            }
        }

        private static readonly RaycastHit[] PastPeople = new RaycastHit[8];

        /// <summary>A body (the painter's, anyone's, anything that walks or is thrown about), not part of the city.</summary>
        private static bool IsPerson(Collider c, Transform painter, Dictionary<Collider, bool> known)
        {
            if (c == null)
            {
                return false;
            }
            if (!known.TryGetValue(c, out bool person))
            {
                person = (painter != null && c.transform.IsChildOf(painter)) || c.GetComponentInParent<IDamageable>() != null
                    || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic);
                known[c] = person;
            }
            return person;
        }

        /// <summary>The first thing down a mask ray that is not a person.</summary>
        private static bool FaceBehindPeople(RaycastCommand ray, Transform painter, Dictionary<Collider, bool> known, out RaycastHit first)
        {
            first = default;
            int n = Physics.RaycastNonAlloc(ray.from, ray.direction, PastPeople, ray.distance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (PastPeople[i].distance < best && !IsPerson(PastPeople[i].collider, painter, known))
                {
                    best = PastPeople[i].distance;
                    first = PastPeople[i];
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>A piece fit to put up: finite, a real wall direction, a sane size, paint of a sane weight. Checked for pieces from the network.</summary>
        public static bool IsSane(GraffitiPiece piece)
        {
            return WireChecks.Finite(piece.Centre) && WireChecks.Finite(piece.Normal) && piece.Normal.sqrMagnitude > 0.5f && piece.Normal.sqrMagnitude < 1.5f
                && piece.Size >= 0.5f && piece.Size <= 6.0f && piece.Paint != null && piece.Paint.Length > 0
                && piece.Paint.Length <= GraffitiCanvas.MaxEncodedBytes;
        }
    }
}
