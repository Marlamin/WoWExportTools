using System;
using System.Collections.Generic;
using System.Linq;
using WoWFormatLib.FileReaders;
using WoWFormatLib.Structs.WMO;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace WoWExportTools.Loaders
{
    class WMOLoader
    {
        public static Renderer.Structs.WorldModel LoadWMO(string fileName, int shaderProgram)
        {
            if (!Listfile.TryGetFileDataID(fileName, out uint fileDataID))
                CASCLib.Logger.WriteLine("Could not get filedataid for " + fileName);

            if (!WoWFormatLib.Utils.CASC.FileExists(fileDataID))
                throw new Exception("WMO " + fileName + " does not exist!");

            WMO wmo = new WMOReader().LoadWMO(fileDataID, 0, fileName);

            if (wmo.group.Count() == 0)
            {
                CASCLib.Logger.WriteLine("WMO has no groups: ", fileName);
                throw new Exception("Broken WMO! Report to developer (mail marlamin@marlamin.com) with this filename: " + fileName);
            }

            var wmoBatch = new Renderer.Structs.WorldModel()
            {
                groupBatches = new Renderer.Structs.WorldModelGroupBatches[wmo.group.Count()]
            };

            var groupNames = new string[wmo.group.Count()];

            for (var g = 0; g < wmo.group.Count(); g++)
            {
                if (wmo.group[g].mogp.vertices == null) { continue; }

                wmoBatch.groupBatches[g].vao = GL.GenVertexArray();
                wmoBatch.groupBatches[g].vertexBuffer = GL.GenBuffer();
                wmoBatch.groupBatches[g].indiceBuffer = GL.GenBuffer();

                GL.BindVertexArray(wmoBatch.groupBatches[g].vao);

                GL.BindBuffer(BufferTarget.ArrayBuffer, wmoBatch.groupBatches[g].vertexBuffer);

                var wmovertices = new Renderer.Structs.M2Vertex[wmo.group[g].mogp.vertices.Count()];

                for (var i = 0; i < wmo.groupNames.Count(); i++)
                    if (wmo.group[g].mogp.nameOffset == wmo.groupNames[i].offset)
                        groupNames[g] = wmo.groupNames[i].name.Replace(" ", "_");

                if (groupNames[g] == "antiportal") { continue; }

                for (var i = 0; i < wmo.group[g].mogp.vertices.Count(); i++)
                {
                    wmovertices[i].Position = new Vector3(wmo.group[g].mogp.vertices[i].vector.X, wmo.group[g].mogp.vertices[i].vector.Y, wmo.group[g].mogp.vertices[i].vector.Z);
                    wmovertices[i].Normal = new Vector3(wmo.group[g].mogp.normals[i].normal.X, wmo.group[g].mogp.normals[i].normal.Y, wmo.group[g].mogp.normals[i].normal.Z);
                    if (wmo.group[g].mogp.textureCoords[0] == null)
                        wmovertices[i].TexCoord = new Vector2(0.0f, 0.0f);
                    else
                        wmovertices[i].TexCoord = new Vector2(wmo.group[g].mogp.textureCoords[0][i].X, wmo.group[g].mogp.textureCoords[0][i].Y);
                }

                //Push to buffer
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(wmovertices.Length * 8 * sizeof(float)), wmovertices, BufferUsageHint.StaticDraw);

                //Set pointers in buffer
                //var normalAttrib = GL.GetAttribLocation(shaderProgram, "normal");
                //GL.EnableVertexAttribArray(normalAttrib);
                //GL.VertexAttribPointer(normalAttrib, 3, VertexAttribPointerType.Float, false, sizeof(float) * 8, sizeof(float) * 0);

                var texCoordAttrib = GL.GetAttribLocation(shaderProgram, "texCoord");
                GL.EnableVertexAttribArray(texCoordAttrib);
                GL.VertexAttribPointer(texCoordAttrib, 2, VertexAttribPointerType.Float, false, sizeof(float) * 8, sizeof(float) * 3);

                var posAttrib = GL.GetAttribLocation(shaderProgram, "position");
                GL.EnableVertexAttribArray(posAttrib);
                GL.VertexAttribPointer(posAttrib, 3, VertexAttribPointerType.Float, false, sizeof(float) * 8, sizeof(float) * 5);

                //Switch to Index buffer
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, wmoBatch.groupBatches[g].indiceBuffer);

                var wmoindicelist = new List<uint>();
                for (var i = 0; i < wmo.group[g].mogp.indices.Count(); i++)
                    wmoindicelist.Add(wmo.group[g].mogp.indices[i].indice);

                wmoBatch.groupBatches[g].indices = wmoindicelist.ToArray();

                GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(wmoBatch.groupBatches[g].indices.Length * sizeof(uint)), wmoBatch.groupBatches[g].indices, BufferUsageHint.StaticDraw);
            }

            GL.Enable(EnableCap.Texture2D);

            wmoBatch.mats = new Renderer.Structs.Material[wmo.materials.Count()];
            for (var i = 0; i < wmo.materials.Count(); i++)
            {
                wmoBatch.mats[i].texture1 = wmo.materials[i].texture1;
                wmoBatch.mats[i].texture2 = wmo.materials[i].texture2;
                wmoBatch.mats[i].texture3 = wmo.materials[i].texture3;

                if (wmo.textures == null)
                {
                    if (WoWFormatLib.Utils.CASC.FileExists(wmo.materials[i].texture1))
                        wmoBatch.mats[i].textureID1 = BLPLoader.LoadTexture(wmo.materials[i].texture1);

                    if (WoWFormatLib.Utils.CASC.FileExists(wmo.materials[i].texture2))
                        wmoBatch.mats[i].textureID2 = BLPLoader.LoadTexture(wmo.materials[i].texture2);

                    if (WoWFormatLib.Utils.CASC.FileExists(wmo.materials[i].texture3))
                        wmoBatch.mats[i].textureID3 = BLPLoader.LoadTexture(wmo.materials[i].texture3);
                }
                else
                {
                    for (var ti = 0; ti < wmo.textures.Count(); ti++)
                    {
                        if (wmo.textures[ti].startOffset == wmo.materials[i].texture1)
                            wmoBatch.mats[i].textureID1 = BLPLoader.LoadTexture(wmo.textures[ti].filename);

                        if (wmo.textures[ti].startOffset == wmo.materials[i].texture2)
                            wmoBatch.mats[i].textureID2 = BLPLoader.LoadTexture(wmo.textures[ti].filename);

                        if (wmo.textures[ti].startOffset == wmo.materials[i].texture3)
                            wmoBatch.mats[i].textureID3 = BLPLoader.LoadTexture(wmo.textures[ti].filename);
                    }
                }
            }

            wmoBatch.doodads = new Renderer.Structs.WMODoodad[wmo.doodadDefinitions.Count()];

            for(var i = 0; i < wmo.doodadDefinitions.Count(); i++)
            {
                if(wmo.doodadNames != null)
                {
                    for (var j = 0; j < wmo.doodadNames.Count(); j++)
                    {
                        if (wmo.doodadDefinitions[i].offset == wmo.doodadNames[j].startOffset)
                        {
                            wmoBatch.doodads[i].filename = wmo.doodadNames[j].filename;
                        }
                    }
                }
                else
                {
                    wmoBatch.doodads[i].filedataid = wmo.doodadDefinitions[i].offset;
                }

                wmoBatch.doodads[i].flags = wmo.doodadDefinitions[i].flags;
                wmoBatch.doodads[i].position = new Vector3(wmo.doodadDefinitions[i].position.X, wmo.doodadDefinitions[i].position.Y, wmo.doodadDefinitions[i].position.Z);
                wmoBatch.doodads[i].rotation = new Quaternion(wmo.doodadDefinitions[i].rotation.X, wmo.doodadDefinitions[i].rotation.Y, wmo.doodadDefinitions[i].rotation.Z, wmo.doodadDefinitions[i].rotation.W);
                wmoBatch.doodads[i].scale = wmo.doodadDefinitions[i].scale;
                wmoBatch.doodads[i].color = new Vector4(wmo.doodadDefinitions[i].color[0], wmo.doodadDefinitions[i].color[1], wmo.doodadDefinitions[i].color[2], wmo.doodadDefinitions[i].color[3]);
            }

            var numRenderbatches = 0;
            //Get total amount of render batches
            for (var i = 0; i < wmo.group.Count(); i++)
            {
                if (wmo.group[i].mogp.renderBatches == null) { continue; }
                numRenderbatches = numRenderbatches + wmo.group[i].mogp.renderBatches.Count();
            }

            wmoBatch.wmoRenderBatch = new Renderer.Structs.RenderBatch[numRenderbatches];

            var rb = 0;
            for (var g = 0; g < wmo.group.Count(); g++)
            {
                var group = wmo.group[g];
                if (group.mogp.renderBatches == null) { continue; }
                for (var i = 0; i < group.mogp.renderBatches.Count(); i++)
                {
                    wmoBatch.wmoRenderBatch[rb].firstFace = group.mogp.renderBatches[i].firstFace;
                    wmoBatch.wmoRenderBatch[rb].numFaces = group.mogp.renderBatches[i].numFaces;
                    uint matID = 0;

                    if (group.mogp.renderBatches[i].flags == 2)
                    {
                        matID = (uint) group.mogp.renderBatches[i].possibleBox2_3;
                    }
                    else
                    {
                        matID = group.mogp.renderBatches[i].materialID;
                    }

                    wmoBatch.wmoRenderBatch[rb].materialID = new uint[3];
                    for (var ti = 0; ti < wmoBatch.mats.Count(); ti++)
                    {
                        if (wmo.materials[matID].texture1 == wmoBatch.mats[ti].texture1)
                            wmoBatch.wmoRenderBatch[rb].materialID[0] = (uint)wmoBatch.mats[ti].textureID1;

                        if (wmo.materials[matID].texture2 == wmoBatch.mats[ti].texture2)
                            wmoBatch.wmoRenderBatch[rb].materialID[1] = (uint)wmoBatch.mats[ti].textureID2;

                        if (wmo.materials[matID].texture3 == wmoBatch.mats[ti].texture3)
                            wmoBatch.wmoRenderBatch[rb].materialID[2] = (uint)wmoBatch.mats[ti].textureID3;
                    }

                    wmoBatch.wmoRenderBatch[rb].blendType = wmo.materials[matID].blendMode;
                    wmoBatch.wmoRenderBatch[rb].groupID = (uint)g;
                    rb++;
                }
            }

            return wmoBatch;
        }
    }
}
