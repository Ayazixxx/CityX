using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CityX.MapImport
{
    /// <summary>
    /// Reads the geometry out of a Source 1 <c>.bsp</c> (VBSP, version 21 =
    /// CS:GO).
    ///
    /// Only the lumps needed to rebuild visible surfaces are parsed. A BSP
    /// holds a great deal more -- the BSP tree itself, visibility sets, baked
    /// lightmaps, physics hulls -- none of which is useful once the geometry
    /// is out and living in a different engine.
    ///
    /// WHAT THIS DELIBERATELY DOES NOT TOUCH: textures. Source stores only
    /// material NAMES here (see the TexDataStringData lump); the actual images
    /// live in the game's own files, belong to Valve, and are not ours to
    /// ship. The names are read purely so surfaces can be grouped and tool
    /// brushes identified. What comes out is the map's SHAPE, which is the
    /// part the mapper actually authored.
    /// </summary>
    public sealed class SourceBsp
    {
        // Lump indices, from the Source SDK's bspfile.h.
        private const int LumpEntities = 0;
        private const int LumpPlanes = 1;
        private const int LumpTexData = 2;
        private const int LumpVertexes = 3;
        private const int LumpTexInfo = 6;
        private const int LumpFaces = 7;
        private const int LumpEdges = 12;
        private const int LumpSurfEdges = 13;
        private const int LumpModels = 14;
        private const int LumpTexDataStringData = 43;
        private const int LumpTexDataStringTable = 44;

        // Surface flags worth knowing about. Tool brushes are invisible in
        // game and must stay invisible here -- importing them would fill the
        // map with giant solid blocks where the clip volumes and ladders are.
        private const int SurfSky2D = 0x0002;
        private const int SurfSky = 0x0004;
        private const int SurfTrigger = 0x0040;
        private const int SurfNoDraw = 0x0080;
        private const int SurfHint = 0x0100;
        private const int SurfSkip = 0x0200;
        private const int SurfInvisibleMask = SurfSky2D | SurfSky | SurfTrigger | SurfNoDraw | SurfHint | SurfSkip;

        public struct Face
        {
            public int FirstEdge;
            public int NumEdges;
            public int TexInfo;
            public int DispInfo;
            public ushort PlaneNum;
            public byte Side;
        }

        public struct TexInfo
        {
            public Vector4 TextureVecS;
            public Vector4 TextureVecT;
            public int Flags;
            public int TexData;
        }

        public struct TexData
        {
            public int NameStringTableId;
            public int Width;
            public int Height;
        }

        public struct Model
        {
            public Vector3 Origin;
            public int FirstFace;
            public int NumFaces;
        }

        public int Version { get; private set; }
        public Vector3[] Vertices { get; private set; }
        public int[] SurfEdges { get; private set; }
        public ushort[,] Edges { get; private set; }
        public Face[] Faces { get; private set; }
        public TexInfo[] TexInfos { get; private set; }
        public TexData[] TexDatas { get; private set; }
        public Model[] Models { get; private set; }
        public string[] TextureNames { get; private set; }
        public string EntityText { get; private set; }

        public static SourceBsp Load(string path)
        {
            SourceBsp bsp = new SourceBsp();
            using (FileStream fs = File.OpenRead(path))
            using (BinaryReader r = new BinaryReader(fs))
            {
                string magic = new string(r.ReadChars(4));
                if (magic != "VBSP")
                {
                    throw new InvalidDataException(
                        $"'{Path.GetFileName(path)}' is not a Source 1 BSP (magic '{magic}'). " +
                        "CS2 maps are Source 2 and use a completely different format.");
                }

                bsp.Version = r.ReadInt32();

                int[] offsets = new int[64];
                int[] lengths = new int[64];
                for (int i = 0; i < 64; i++)
                {
                    offsets[i] = r.ReadInt32();
                    lengths[i] = r.ReadInt32();
                    r.ReadInt32();      // lump version
                    r.ReadBytes(4);     // fourCC
                }

                bsp.EntityText = ReadEntities(fs, r, offsets[LumpEntities], lengths[LumpEntities]);
                bsp.Vertices = ReadVertices(fs, r, offsets[LumpVertexes], lengths[LumpVertexes]);
                bsp.Edges = ReadEdges(fs, r, offsets[LumpEdges], lengths[LumpEdges]);
                bsp.SurfEdges = ReadSurfEdges(fs, r, offsets[LumpSurfEdges], lengths[LumpSurfEdges]);
                bsp.Faces = ReadFaces(fs, r, offsets[LumpFaces], lengths[LumpFaces]);
                bsp.TexInfos = ReadTexInfos(fs, r, offsets[LumpTexInfo], lengths[LumpTexInfo]);
                bsp.TexDatas = ReadTexDatas(fs, r, offsets[LumpTexData], lengths[LumpTexData]);
                bsp.Models = ReadModels(fs, r, offsets[LumpModels], lengths[LumpModels]);
                bsp.TextureNames = ReadTextureNames(fs, r,
                    offsets[LumpTexDataStringTable], lengths[LumpTexDataStringTable],
                    offsets[LumpTexDataStringData], lengths[LumpTexDataStringData]);
            }
            return bsp;
        }

        /// <summary>True when a face is a tool brush that should never be rendered.</summary>
        public bool IsFaceInvisible(in Face face)
        {
            if (face.TexInfo < 0 || face.TexInfo >= TexInfos.Length)
            {
                return true;
            }
            return (TexInfos[face.TexInfo].Flags & SurfInvisibleMask) != 0;
        }

        /// <summary>
        /// Walks a face's perimeter in winding order.
        ///
        /// This indirection is the heart of the format and the easiest part to
        /// get wrong. Faces do not index vertices directly. They index a run
        /// of SURFEDGES, each of which is a SIGNED index into EDGES. The sign
        /// carries the direction: positive means read the edge forwards, and
        /// negative means read it backwards. Ignore the sign and every other
        /// polygon comes out wound inside out, which renders as a map full of
        /// holes you can see through from one side only.
        /// </summary>
        public List<int> FacePerimeter(in Face face)
        {
            List<int> indices = new List<int>(face.NumEdges);
            for (int i = 0; i < face.NumEdges; i++)
            {
                int surfEdge = SurfEdges[face.FirstEdge + i];
                if (surfEdge >= 0)
                {
                    indices.Add(Edges[surfEdge, 0]);
                }
                else
                {
                    indices.Add(Edges[-surfEdge, 1]);
                }
            }
            return indices;
        }

        public string TextureNameFor(in Face face)
        {
            if (face.TexInfo < 0 || face.TexInfo >= TexInfos.Length)
            {
                return "";
            }
            int texData = TexInfos[face.TexInfo].TexData;
            if (texData < 0 || texData >= TexDatas.Length)
            {
                return "";
            }
            int nameId = TexDatas[texData].NameStringTableId;
            if (nameId < 0 || nameId >= TextureNames.Length)
            {
                return "";
            }
            return TextureNames[nameId];
        }

        // --- lump readers ---

        private static string ReadEntities(FileStream fs, BinaryReader r, int offset, int length)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            return Encoding.ASCII.GetString(r.ReadBytes(length));
        }

        private static Vector3[] ReadVertices(FileStream fs, BinaryReader r, int offset, int length)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            Vector3[] result = new Vector3[length / 12];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            }
            return result;
        }

        private static ushort[,] ReadEdges(FileStream fs, BinaryReader r, int offset, int length)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            int count = length / 4;
            ushort[,] result = new ushort[count, 2];
            for (int i = 0; i < count; i++)
            {
                result[i, 0] = r.ReadUInt16();
                result[i, 1] = r.ReadUInt16();
            }
            return result;
        }

        private static int[] ReadSurfEdges(FileStream fs, BinaryReader r, int offset, int length)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            int[] result = new int[length / 4];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = r.ReadInt32();
            }
            return result;
        }

        private static Face[] ReadFaces(FileStream fs, BinaryReader r, int offset, int length)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            // dface_t is 56 bytes in BSP v21.
            Face[] result = new Face[length / 56];
            for (int i = 0; i < result.Length; i++)
            {
                Face f = new Face();
                f.PlaneNum = r.ReadUInt16();
                f.Side = r.ReadByte();
                r.ReadByte();                 // onNode
                f.FirstEdge = r.ReadInt32();
                f.NumEdges = r.ReadInt16();
                f.TexInfo = r.ReadInt16();
                f.DispInfo = r.ReadInt16();
                r.ReadInt16();                // surfaceFogVolumeID
                r.ReadBytes(4);               // styles
                r.ReadInt32();                // lightofs
                r.ReadSingle();               // area
                r.ReadBytes(8);               // LightmapTextureMinsInLuxels
                r.ReadBytes(8);               // LightmapTextureSizeInLuxels
                r.ReadInt32();                // origFace
                r.ReadUInt16();               // numPrims
                r.ReadUInt16();               // firstPrimID
                r.ReadUInt32();               // smoothingGroups
                result[i] = f;
            }
            return result;
        }

        private static TexInfo[] ReadTexInfos(FileStream fs, BinaryReader r, int offset, int length)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            TexInfo[] result = new TexInfo[length / 72];
            for (int i = 0; i < result.Length; i++)
            {
                TexInfo t = new TexInfo();
                t.TextureVecS = new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                t.TextureVecT = new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                r.ReadBytes(32);              // lightmapVecs
                t.Flags = r.ReadInt32();
                t.TexData = r.ReadInt32();
                result[i] = t;
            }
            return result;
        }

        private static TexData[] ReadTexDatas(FileStream fs, BinaryReader r, int offset, int length)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            TexData[] result = new TexData[length / 32];
            for (int i = 0; i < result.Length; i++)
            {
                TexData t = new TexData();
                r.ReadBytes(12);              // reflectivity
                t.NameStringTableId = r.ReadInt32();
                t.Width = r.ReadInt32();
                t.Height = r.ReadInt32();
                r.ReadInt32();                // view_width
                r.ReadInt32();                // view_height
                result[i] = t;
            }
            return result;
        }

        private static Model[] ReadModels(FileStream fs, BinaryReader r, int offset, int length)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            Model[] result = new Model[length / 48];
            for (int i = 0; i < result.Length; i++)
            {
                Model m = new Model();
                r.ReadBytes(12);              // mins
                r.ReadBytes(12);              // maxs
                m.Origin = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                r.ReadInt32();                // headnode
                m.FirstFace = r.ReadInt32();
                m.NumFaces = r.ReadInt32();
                result[i] = m;
            }
            return result;
        }

        private static string[] ReadTextureNames(FileStream fs, BinaryReader r,
            int tableOffset, int tableLength, int dataOffset, int dataLength)
        {
            fs.Seek(tableOffset, SeekOrigin.Begin);
            int count = tableLength / 4;
            int[] starts = new int[count];
            for (int i = 0; i < count; i++)
            {
                starts[i] = r.ReadInt32();
            }

            fs.Seek(dataOffset, SeekOrigin.Begin);
            byte[] blob = r.ReadBytes(dataLength);

            string[] names = new string[count];
            for (int i = 0; i < count; i++)
            {
                int start = starts[i];
                if (start < 0 || start >= blob.Length)
                {
                    names[i] = "";
                    continue;
                }
                int end = start;
                while (end < blob.Length && blob[end] != 0)
                {
                    end++;
                }
                names[i] = Encoding.ASCII.GetString(blob, start, end - start);
            }
            return names;
        }
    }
}
