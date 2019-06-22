using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL;

namespace WoWExportTools
{
    class CacheStorage
    {
        public Dictionary<string, WoWFormatLib.Structs.M2.M2Model> models = new Dictionary<string, WoWFormatLib.Structs.M2.M2Model>();
        public Dictionary<uint, int> materials = new Dictionary<uint, int>();
        public Dictionary<string, WoWFormatLib.Structs.WMO.WMO> worldModels = new Dictionary<string, WoWFormatLib.Structs.WMO.WMO>();

        public Dictionary<string, Renderer.Structs.DoodadBatch> doodadBatches = new Dictionary<string, Renderer.Structs.DoodadBatch>();
        public Dictionary<string, Renderer.Structs.WorldModel> worldModelBatches = new Dictionary<string, Renderer.Structs.WorldModel>();

        public Dictionary<string, Renderer.Structs.Terrain> terrain = new Dictionary<string, Renderer.Structs.Terrain>();

        public CacheStorage()
        {
            
        }

        public void ClearCache()
        {
            throw new NotSupportedException("You should probably extensively test this first before using it somewhere!");
            models = new Dictionary<string, WoWFormatLib.Structs.M2.M2Model>();
            worldModels = new Dictionary<string, WoWFormatLib.Structs.WMO.WMO>();

            // Clean up alpha textures in terrain
            foreach (var adt in terrain)
            {
                foreach (var batch in adt.Value.renderBatches)
                {
                    GL.DeleteTextures(batch.alphaMaterialID.Length, batch.alphaMaterialID);
                }
            }
            terrain = new Dictionary<string, Renderer.Structs.Terrain>();

            // Clean up buffers in doodadbatches
            foreach (var batch in doodadBatches)
            {
                GL.DeleteVertexArray(batch.Value.vao);
                GL.DeleteBuffer(batch.Value.vertexBuffer);
                GL.DeleteBuffer(batch.Value.indiceBuffer);
            }
            doodadBatches = new Dictionary<string, Renderer.Structs.DoodadBatch>();

            // Clean up buffers in worldmodelbatches
            foreach (var batch in worldModelBatches)
            {
                foreach (var groupBatch in batch.Value.groupBatches)
                {
                    GL.DeleteVertexArray(groupBatch.vao);
                    GL.DeleteBuffer(groupBatch.vertexBuffer);
                    GL.DeleteBuffer(groupBatch.indiceBuffer);
                }
            }
            worldModelBatches = new Dictionary<string, Renderer.Structs.WorldModel>();

            // Clean up textures
            var mats = materials.Values.ToArray();
            GL.DeleteTextures(mats.Length, mats);
            materials = new Dictionary<uint, int>();
            
            // Force GC collection
            GC.Collect();
        }
    }
}
