using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OBJExporterUI
{
    public class Structs
    {
        public struct MapTile
        {
            public uint wdtFileDataID;
            public byte tileX;
            public byte tileY;
        }

        public struct RenderBatch
        {
            public uint firstFace;
            public uint materialID;
            public uint numFaces;
            public uint groupID;
            public uint blendType;
        }

        public struct Vertex
        {
            public Vector3D Position;
            public Vector3D Normal;
            public Vector2D TexCoord;
            public Vector3D Color;
        }

        public struct Vector3D
        {
            public double X;
            public double Y;
            public double Z;
        }

        public struct Vector2D
        {
            public double X;
            public double Y;
        }
        public struct Material
        {
            public string filename;
            public WoWFormatLib.Structs.M2.TextureFlags flags;
            public int textureID;
            public uint shaderID;
            public uint blendMode;
            public uint terrainType;
            public bool transparent;
        }

        public struct WMOGroup
        {
            public string name;
            public uint verticeOffset;
            public Vertex[] vertices;
            public uint[] indices;
            public RenderBatch[] renderBatches;
        }
    }
}
