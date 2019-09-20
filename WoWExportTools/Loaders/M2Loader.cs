using System;
using System.Collections.Generic;
using System.Linq;
using WoWFormatLib.FileReaders;
using WoWFormatLib.Structs.M2;
using OpenTK.Graphics.OpenGL;
using OpenTK;

namespace WoWExportTools.Loaders
{
    class M2Loader
    {
        private static uint DEFAULT_TEXTURE_ID = 528732; // dungeons/textures/testing/color_01.blp
        private static uint MISSING_TEXTURE_ID = 186184; // textures/shanecube.blp

        public static Renderer.Structs.DoodadBatch LoadM2(string fileName, int shaderProgram)
        {
            fileName = fileName.ToLower().Replace(".mdx", ".m2");
            fileName = fileName.ToLower().Replace(".mdl", ".m2");

            M2Model model = new M2Model();
            if (Listfile.TryGetFileDataID(fileName, out var fileDataID))
            {
                if (WoWFormatLib.Utils.CASC.FileExists(fileDataID))
                {
                    var modelReader = new M2Reader();
                    modelReader.LoadM2(fileDataID);
                    model = modelReader.model;
                }
                else
                {
                    throw new Exception("Model " + fileName + " does not exist!");
                }
            }
            else
            {
                throw new Exception("Filename " + fileName + " does not exist in listfile!");
            }

            if (model.boundingbox == null)
                throw new Exception("Model does not contain bounding box: " + fileName);

            var doodadBatch = new Renderer.Structs.DoodadBatch()
            {
                boundingBox = new Renderer.Structs.BoundingBox()
                {
                    min = new Vector3(model.boundingbox[0].X, model.boundingbox[0].Y, model.boundingbox[0].Z),
                    max = new Vector3(model.boundingbox[1].X, model.boundingbox[1].Y, model.boundingbox[1].Z)
                }
            };

            if (model.textures == null)
                throw new Exception("Model does not contain textures: " + fileName);

            if (model.skins == null)
                throw new Exception("Model does not contain skins: " + fileName);

            // Textures
            doodadBatch.mats = new Renderer.Structs.Material[model.textures.Count()];
            for (var i = 0; i < model.textures.Count(); i++)
            {
                uint textureFileDataID = DEFAULT_TEXTURE_ID;
                doodadBatch.mats[i].flags = model.textures[i].flags;

                switch (model.textures[i].type)
                {
                    case 0: // NONE
                        if (model.textureFileDataIDs != null && model.textureFileDataIDs.Length > 0 && model.textureFileDataIDs[i] != 0)
                            textureFileDataID = model.textureFileDataIDs[i];
                        else
                            textureFileDataID = WoWFormatLib.Utils.CASC.getFileDataIdByName(model.textures[i].filename);
                        break;
                    case 1: // TEX_COMPONENT_SKIN
                    case 2: // TEX_COMPONENT_OBJECT_SKIN
                    case 11: // TEX_COMPONENT_MONSTER_1
                        break;
                }

                // Not set in TXID
                if (textureFileDataID == 0)
                    textureFileDataID = DEFAULT_TEXTURE_ID;

                if (!WoWFormatLib.Utils.CASC.FileExists(textureFileDataID))
                    textureFileDataID = MISSING_TEXTURE_ID;

                doodadBatch.mats[i].textureID = BLPLoader.LoadTexture(textureFileDataID);
                doodadBatch.mats[i].filename = textureFileDataID.ToString();
            }

            // Submeshes
            doodadBatch.submeshes = new Renderer.Structs.Submesh[model.skins[0].submeshes.Count()];
            for (var i = 0; i < model.skins[0].submeshes.Count(); i++)
            {
                doodadBatch.submeshes[i].enabled = true;
                doodadBatch.submeshes[i].firstFace = model.skins[0].submeshes[i].startTriangle;
                doodadBatch.submeshes[i].numFaces = model.skins[0].submeshes[i].nTriangles;
                for (var tu = 0; tu < model.skins[0].textureunit.Count(); tu++)
                {
                    if (model.skins[0].textureunit[tu].submeshIndex == i)
                    {
                        doodadBatch.submeshes[i].blendType = model.renderflags[model.skins[0].textureunit[tu].renderFlags].blendingMode;

                        uint textureFileDataID = DEFAULT_TEXTURE_ID;
                        if (!WoWFormatLib.Utils.CASC.FileExists(textureFileDataID))
                            textureFileDataID = MISSING_TEXTURE_ID;

                        if (model.textureFileDataIDs != null && model.textureFileDataIDs.Length > 0 && model.textureFileDataIDs[model.texlookup[model.skins[0].textureunit[tu].texture].textureID] != 0)
                        {
                            textureFileDataID = model.textureFileDataIDs[model.texlookup[model.skins[0].textureunit[tu].texture].textureID];
                        }
                        else
                        {
                            if (Listfile.FilenameToFDID.TryGetValue(model.textures[model.texlookup[model.skins[0].textureunit[tu].texture].textureID].filename.Replace('\\', '/').ToLower(), out var filedataid))
                            {
                                textureFileDataID = filedataid;
                            }
                            else
                            {
                                textureFileDataID = DEFAULT_TEXTURE_ID;
                                if (!WoWFormatLib.Utils.CASC.FileExists(textureFileDataID))
                                    textureFileDataID = MISSING_TEXTURE_ID;
                            }
                        }

                        if (!WoWFormatLib.Utils.CASC.FileExists(textureFileDataID))
                            textureFileDataID = MISSING_TEXTURE_ID;

                        doodadBatch.submeshes[i].material = (uint)BLPLoader.LoadTexture(textureFileDataID);
                    }
                }
            }

            doodadBatch.vao = GL.GenVertexArray();
            GL.BindVertexArray(doodadBatch.vao);

            // Vertices & indices
            doodadBatch.vertexBuffer = GL.GenBuffer();
            doodadBatch.indiceBuffer = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.ArrayBuffer, doodadBatch.vertexBuffer);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, doodadBatch.indiceBuffer);

            var modelindicelist = new List<uint>();
            for (var i = 0; i < model.skins[0].triangles.Count(); i++)
            {
                modelindicelist.Add(model.skins[0].triangles[i].pt1);
                modelindicelist.Add(model.skins[0].triangles[i].pt2);
                modelindicelist.Add(model.skins[0].triangles[i].pt3);
            }

            var modelindices = modelindicelist.ToArray();

            doodadBatch.indices = modelindices;

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, doodadBatch.indiceBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(doodadBatch.indices.Length * sizeof(uint)), doodadBatch.indices, BufferUsageHint.StaticDraw);

            var modelvertices = new Renderer.Structs.M2Vertex[model.vertices.Count()];

            for (var i = 0; i < model.vertices.Count(); i++)
            {
                modelvertices[i].Position = new Vector3(model.vertices[i].position.X, model.vertices[i].position.Y, model.vertices[i].position.Z);
                modelvertices[i].Normal = new Vector3(model.vertices[i].normal.X, model.vertices[i].normal.Y, model.vertices[i].normal.Z);
                modelvertices[i].TexCoord = new Vector2(model.vertices[i].textureCoordX, model.vertices[i].textureCoordY);
            }
            GL.BindBuffer(BufferTarget.ArrayBuffer, doodadBatch.vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(modelvertices.Length * 8 * sizeof(float)), modelvertices, BufferUsageHint.StaticDraw);

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

            return doodadBatch;
        }
    }
}
