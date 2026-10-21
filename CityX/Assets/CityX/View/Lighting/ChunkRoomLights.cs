using System.Collections.Generic;
using CityX.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace CityX.View
{
    /// <summary>
    /// The powered rooms of one streamed chunk (RoomLightGroup), on the chunk itself: which of them are lit (World.RoomLights), and what the lit
    /// ones show -- the glowing faces of their fittings (RoomGlow.mat, the room's colour x3 so it blooms) and the veil of light on their windows
    /// seen from the street (Shaders/CityXWindowLight.shader). Those are drawn as ONE mesh per light colour for the whole chunk (a room's colour
    /// comes from its kind of lighting, World.RoomLighting.ColourOf: five in all), redrawn when a switch in the chunk changes. A room whose switch
    /// is off costs nothing but the few numbers that say where its lights would go. The real lights are RoomLightDirector's to lend.
    ///
    /// Made by CityStreamer as the chunk is assembled (<see cref="Attach"/>), switched off with it while its collider cooks; it starts watching
    /// the switches when the chunk goes on screen, and stops when the chunk goes.
    /// </summary>
    public sealed class ChunkRoomLights : MonoBehaviour
    {
        private RoomLightGroup[] _rooms = System.Array.Empty<RoomLightGroup>();
        private int[] _buildings = System.Array.Empty<int>();
        private Material _glowMaterial;
        private Material _windowMaterial;
        private readonly List<ColourDrawing> _drawings = new List<ColourDrawing>();
        private bool _watching;
        private bool _redrawAsked;

        /// <summary>Every powered room in the chunk.</summary>
        public IReadOnlyList<RoomLightGroup> Rooms => _rooms;

        /// <summary>How many lit rooms are drawn now (their glow and windows), for tests.</summary>
        public int RoomsDrawn
        {
            get
            {
                int drawn = 0;
                foreach (ColourDrawing drawing in _drawings)
                {
                    drawn += drawing.Rooms.Count;
                }
                return drawn;
            }
        }

        /// <summary>The renderers of the lit rooms' glow and windows that are switched on now, for tests.</summary>
        public List<Renderer> DrawnRenderers()
        {
            var drawn = new List<Renderer>();
            foreach (ColourDrawing drawing in _drawings)
            {
                if (drawing.Glow != null && drawing.Glow.enabled) drawn.Add(drawing.Glow);
                if (drawing.Windows != null && drawing.Windows.enabled) drawn.Add(drawing.Windows);
            }
            return drawn;
        }

        /// <summary>
        /// The powered rooms CityMeshBuilder collected for a chunk (<paramref name="glows"/>; the list is copied, the rooms in it are kept), on the
        /// chunk. Null if it has none: most chunks, since only one building in ten has power.
        /// </summary>
        public static ChunkRoomLights Attach(GameObject chunk, IReadOnlyList<CityMeshBuilder.RoomGlow> glows, Material glowMaterial, Material windowMaterial)
        {
            if (glows == null || glows.Count == 0)
            {
                return null;
            }
            ChunkRoomLights lights = chunk.AddComponent<ChunkRoomLights>();
            lights.Bind(glows, glowMaterial, windowMaterial);
            return lights;
        }

        private void Bind(IReadOnlyList<CityMeshBuilder.RoomGlow> glows, Material glowMaterial, Material windowMaterial)
        {
            _glowMaterial = glowMaterial;
            _windowMaterial = windowMaterial;
            _rooms = new RoomLightGroup[glows.Count];
            var buildings = new List<int>(2);
            for (int i = 0; i < glows.Count; i++)
            {
                _rooms[i] = new RoomLightGroup(this, glows[i]);
                if (!buildings.Contains(glows[i].Key.Building))
                {
                    buildings.Add(glows[i].Key.Building);
                }
            }
            _buildings = buildings.ToArray();
            if (isActiveAndEnabled && Application.isPlaying)
            {
                Watch();   // on a chunk already on screen (the synchronous path): OnEnable ran before there was anything to watch
            }
        }

        private void OnEnable()
        {
            if (_rooms.Length > 0 && Application.isPlaying)
            {
                Watch();
            }
        }

        private void OnDisable()
        {
            if (!_watching)
            {
                return;
            }
            _watching = false;
            RoomLights.Changed -= OnSwitch;
            foreach (RoomLightGroup room in _rooms)
            {
                if (room.IsLit)
                {
                    room.IsLit = false;
                    RoomLightDirector.Dark(room);   // its lights go back to the pool now, not when the chunk is destroyed
                }
            }
        }

        /// <summary>Starts following the switches: the rooms already switched on light up (their lights the director's next pass, this frame).</summary>
        private void Watch()
        {
            if (_watching)
            {
                return;
            }
            _watching = true;
            RoomLights.Changed += OnSwitch;
            RoomLightDirector.Ensure();
            bool anyLit = false;
            foreach (RoomLightGroup room in _rooms)
            {
                if (RoomLights.IsOn(room.Key))
                {
                    room.IsLit = true;
                    RoomLightDirector.Lit(room);
                    anyLit = true;
                }
            }
            if (anyLit || _drawings.Count > 0)
            {
                AskRedraw();   // its lit rooms, or what was drawn before it went off screen (which may be out of date); usually neither
            }
        }

        private void OnSwitch(RoomKey key, bool on)
        {
            if (System.Array.IndexOf(_buildings, key.Building) < 0)
            {
                return;
            }
            foreach (RoomLightGroup room in _rooms)
            {
                if (!room.Key.Equals(key))
                {
                    continue;
                }
                if (room.IsLit != on)
                {
                    room.IsLit = on;
                    if (on)
                    {
                        RoomLightDirector.Lit(room);
                    }
                    else
                    {
                        RoomLightDirector.Dark(room);
                    }
                    AskRedraw();
                }
                return;
            }
        }

        private void AskRedraw()
        {
            if (_redrawAsked)
            {
                return;
            }
            _redrawAsked = true;
            RoomLightDirector.Redraw(this);
        }

        /// <summary>Draws the lit rooms again, one mesh of glow and one of windows per light colour. RoomLightDirector's, once a frame at most.</summary>
        internal void Redraw()
        {
            _redrawAsked = false;
            foreach (ColourDrawing drawing in _drawings)
            {
                drawing.Rooms.Clear();
            }
            if (_watching)
            {
                foreach (RoomLightGroup room in _rooms)
                {
                    if (room.IsLit)
                    {
                        DrawingFor(room.Colour).Rooms.Add(room);
                    }
                }
            }
            foreach (ColourDrawing drawing in _drawings)
            {
                drawing.Draw();
            }
        }

        private ColourDrawing DrawingFor(Color colour)
        {
            foreach (ColourDrawing drawing in _drawings)
            {
                if (drawing.Colour == colour)
                {
                    return drawing;
                }
            }
            var made = new ColourDrawing(transform, colour, _glowMaterial, _windowMaterial);
            _drawings.Add(made);
            return made;
        }

        private void OnDestroy()
        {
            // The meshes are this object's (CityStreamer.DestroyChunk frees every mesh under a chunk; this is for one destroyed some other way).
            foreach (ColourDrawing drawing in _drawings)
            {
                drawing.Free();
            }
            _drawings.Clear();
        }

        /// <summary>The lit rooms of one light colour in the chunk: one renderer for their fittings' glow, one for their windows.</summary>
        private sealed class ColourDrawing
        {
            // Shared scratch for building the meshes (main thread only): nothing is allocated per redraw once they have grown.
            private static readonly List<Vector3> Vertices = new List<Vector3>();
            private static readonly List<Vector3> Normals = new List<Vector3>();
            private static readonly List<Vector2> Uvs = new List<Vector2>();
            private static readonly List<int> Triangles = new List<int>();
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private static MaterialPropertyBlock _block;

            public readonly Color Colour;
            public readonly List<RoomLightGroup> Rooms = new List<RoomLightGroup>();
            public readonly MeshRenderer Glow;
            public readonly MeshRenderer Windows;
            private readonly Mesh _glowMesh;
            private readonly Mesh _windowMesh;

            public ColourDrawing(Transform chunk, Color colour, Material glowMaterial, Material windowMaterial)
            {
                Colour = colour;
                _block ??= new MaterialPropertyBlock();

                _glowMesh = new Mesh { name = "room-glow" };
                Glow = MakeRenderer(chunk, "RoomGlow", _glowMesh, glowMaterial);
                _block.SetColor(BaseColorId, colour * CityStreamer.RoomGlowBrightness);
                Glow.SetPropertyBlock(_block);

                // The windows from outside: a soft veil of the room's light just behind the glass, over the real lit room that shows through it.
                if (windowMaterial != null)
                {
                    _windowMesh = new Mesh { name = "room-windows" };
                    Windows = MakeRenderer(chunk, "RoomWindows", _windowMesh, windowMaterial);
                    Windows.receiveShadows = false;
                    _block.SetColor(BaseColorId, colour * CityStreamer.RoomWindowBrightness);
                    Windows.SetPropertyBlock(_block);
                }
            }

            private static MeshRenderer MakeRenderer(Transform chunk, string name, Mesh mesh, Material material)
            {
                var go = new GameObject(name);
                go.transform.SetParent(chunk, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.enabled = false;
                return renderer;
            }

            /// <summary>Builds the two meshes from <see cref="Rooms"/> (switched off when there are none).</summary>
            public void Draw()
            {
                Glow.enabled = Fill(_glowMesh, false);
                if (Windows != null)
                {
                    Windows.enabled = Fill(_windowMesh, true);
                }
            }

            private bool Fill(Mesh mesh, bool windows)
            {
                Vertices.Clear();
                Normals.Clear();
                Uvs.Clear();
                Triangles.Clear();
                foreach (RoomLightGroup room in Rooms)
                {
                    MeshBuffer part = windows ? room.Glow.Windows : room.Glow.Glow;
                    int first = Vertices.Count;
                    Vertices.AddRange(part.Vertices);
                    Normals.AddRange(part.Normals);
                    if (windows)
                    {
                        Uvs.AddRange(part.Uvs);   // 0..1 across each pane: the veil is brighter towards the top
                    }
                    foreach (int index in part.Triangles)
                    {
                        Triangles.Add(first + index);
                    }
                }
                mesh.Clear();
                if (Triangles.Count == 0)
                {
                    return false;
                }
                mesh.indexFormat = Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(Vertices);
                mesh.SetNormals(Normals);
                if (windows)
                {
                    mesh.SetUVs(0, Uvs);
                }
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateBounds();
                return true;
            }

            public void Free()
            {
                FreeMesh(_glowMesh);
                FreeMesh(_windowMesh);
            }

            private static void FreeMesh(Mesh mesh)
            {
                if (mesh == null)
                {
                    return;   // already gone with its chunk
                }
                if (Application.isPlaying) Object.Destroy(mesh); else Object.DestroyImmediate(mesh);
            }
        }
    }
}
