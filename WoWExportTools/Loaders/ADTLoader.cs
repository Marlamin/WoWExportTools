using System;
using System.Collections.Generic;
using System.Linq;
using WoWFormatLib.FileReaders;
using WoWFormatLib.Structs.ADT;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using static WoWExportTools.Renderer.Structs;

namespace WoWExportTools.Loaders
{
    class ADTLoader
    {
        public static Terrain LoadADT(Structs.MapTile mapTile, int shaderProgram, bool loadModels = false)
        {
            ADT adt = new ADT();
            Terrain result = new Terrain();
            ADTReader adtReader = new ADTReader();

            Listfile.FDIDToFilename.TryGetValue(mapTile.wdtFileDataID, out string wdtFilename);

            adtReader.LoadADT(mapTile.wdtFileDataID, mapTile.tileX, mapTile.tileY, true, wdtFilename);
            adt = adtReader.adtfile;

            var TileSize = 1600.0f / 3.0f; //533.333
            var ChunkSize = TileSize / 16.0f; //33.333
            var UnitSize = ChunkSize / 8.0f; //4.166666
            var MapMidPoint = 32.0f / ChunkSize;

            var verticelist = new List<Vertex>();
            var indicelist = new List<int>();

            result.vao = GL.GenVertexArray();
            GL.BindVertexArray(result.vao);

            result.vertexBuffer = GL.GenBuffer();
            result.indiceBuffer = GL.GenBuffer();

            var materials = new List<Material>();

            if(adt.textures.filenames == null)
            {
                for (var ti = 0; ti < adt.diffuseTextureFileDataIDs.Count(); ti++)
                {
                    var material = new Material();
                    material.filename = adt.diffuseTextureFileDataIDs[ti].ToString();
                    material.textureID = BLPLoader.LoadTexture(adt.diffuseTextureFileDataIDs[ti]);

                    if (adt.texParams != null && adt.texParams.Count() >= ti)
                    {
                        material.scale = (float)Math.Pow(2, (adt.texParams[ti].flags & 0xF0) >> 4);
                        if (adt.texParams[ti].height != 0.0 || adt.texParams[ti].offset != 1.0)
                        {
                            material.heightScale = adt.texParams[ti].height;
                            material.heightOffset = adt.texParams[ti].offset;

                            if (!WoWFormatLib.Utils.CASC.FileExists(adt.heightTextureFileDataIDs[ti]))
                            {
                                Console.WriteLine("Height texture: " + adt.heightTextureFileDataIDs[ti] + " does not exist! Falling back to original texture (hack)..");
                                material.heightTexture = BLPLoader.LoadTexture(adt.diffuseTextureFileDataIDs[ti]);
                            }
                            else
                            {
                                material.heightTexture = BLPLoader.LoadTexture(adt.heightTextureFileDataIDs[ti]);
                            }
                        }
                        else
                        {
                            material.heightScale = 0.0f;
                            material.heightOffset = 1.0f;
                        }
                    }
                    else
                    {
                        material.heightScale = 0.0f;
                        material.heightOffset = 1.0f;
                        material.scale = 1.0f;
                    }
                    materials.Add(material);
                }
            }
            else
            {
                for (var ti = 0; ti < adt.textures.filenames.Count(); ti++)
                {
                    var material = new Material();
                    material.filename = adt.textures.filenames[ti];
                    material.textureID = BLPLoader.LoadTexture(adt.textures.filenames[ti]);

                    if (adt.texParams != null && adt.texParams.Count() >= ti)
                    {
                        material.scale = (float)Math.Pow(2, (adt.texParams[ti].flags & 0xF0) >> 4);
                        if (adt.texParams[ti].height != 0.0 || adt.texParams[ti].offset != 1.0)
                        {
                            material.heightScale = adt.texParams[ti].height;
                            material.heightOffset = adt.texParams[ti].offset;

                            var heightName = adt.textures.filenames[ti].Replace(".blp", "_h.blp");
                            if (!WoWFormatLib.Utils.CASC.FileExists(heightName))
                            {
                                Console.WriteLine("Height texture: " + heightName + " does not exist! Falling back to original texture (hack)..");
                                material.heightTexture = BLPLoader.LoadTexture(adt.textures.filenames[ti]);
                            }
                            else
                            {
                                material.heightTexture = BLPLoader.LoadTexture(heightName);
                            }
                        }
                        else
                        {
                            material.heightScale = 0.0f;
                            material.heightOffset = 1.0f;
                        }
                    }
                    else
                    {
                        material.heightScale = 0.0f;
                        material.heightOffset = 1.0f;
                        material.scale = 1.0f;
                    }
                    materials.Add(material);
                }
            }


            var initialChunkY = adt.chunks[0].header.position.Y;
            var initialChunkX = adt.chunks[0].header.position.X;

            var renderBatches = new List<RenderBatch>();

            for (uint c = 0; c < adt.chunks.Count(); c++)
            {
                var chunk = adt.chunks[c];

                var off = verticelist.Count();

                var batch = new RenderBatch();

                batch.groupID = c;

                for (int i = 0, idx = 0; i < 17; i++)
                {
                    for (var j = 0; j < (((i % 2) != 0) ? 8 : 9); j++)
                    {
                        var v = new Vertex();
                        v.Normal = new Vector3(chunk.normals.normal_0[idx], chunk.normals.normal_1[idx], chunk.normals.normal_2[idx]);
                        if (chunk.vertexShading.red != null)
                            v.Color = new Vector4(chunk.vertexShading.blue[idx] / 255.0f, chunk.vertexShading.green[idx] / 255.0f, chunk.vertexShading.red[idx] / 255.0f, chunk.vertexShading.alpha[idx] / 255.0f);
                        else
                            v.Color = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);

                        v.TexCoord = new Vector2((j + (((i % 2) != 0) ? 0.5f : 0f)) / 8f, (i * 0.5f) / 8f);
                        v.Position = new Vector3(chunk.header.position.X - (i * UnitSize * 0.5f), chunk.header.position.Y - (j * UnitSize), chunk.vertices.vertices[idx++] + chunk.header.position.Z);

                        if ((i % 2) != 0)
                            v.Position.Y -= 0.5f * UnitSize;

                        verticelist.Add(v);
                    }
                }

                result.startPos = verticelist[0];

                batch.firstFace = (uint)indicelist.Count();
                for (var j = 9; j < 145; j++)
                {
                    indicelist.AddRange(new int[] { off + j + 8, off + j - 9, off + j });
                    indicelist.AddRange(new int[] { off + j - 9, off + j - 8, off + j });
                    indicelist.AddRange(new int[] { off + j - 8, off + j + 9, off + j });
                    indicelist.AddRange(new int[] { off + j + 9, off + j + 8, off + j });
                    if ((j + 1) % (9 + 8) == 0) j += 9;
                }
                batch.numFaces = (uint)(indicelist.Count()) - batch.firstFace;

                var layerMaterials = new List<uint>();
                var alphalayermats = new List<int>();
                var layerscales = new List<float>();
                var layerheights = new List<int>();

                batch.heightScales = new Vector4();
                batch.heightOffsets = new Vector4();

                for (var li = 0; li < adt.texChunks[c].layers.Count(); li++)
                {
                    if (adt.texChunks[c].alphaLayer != null)
                        alphalayermats.Add(BLPLoader.GenerateAlphaTexture(adt.texChunks[c].alphaLayer[li].layer));

                    Material curMat;

                    if (adt.diffuseTextureFileDataIDs == null)
                    {
                        if (adt.textures.filenames == null)
                            throw new Exception("ADT has no textures?");

                        var texFileDataID = WoWFormatLib.Utils.CASC.getFileDataIdByName(adt.textures.filenames[adt.texChunks[c].layers[li].textureId]);

                        layerMaterials.Add((uint)BLPLoader.LoadTexture(texFileDataID));
                        curMat = materials.Where(material => material.filename == adt.textures.filenames[adt.texChunks[c].layers[li].textureId]).Single();
                    }
                    else
                    {
                        layerMaterials.Add((uint)BLPLoader.LoadTexture(adt.diffuseTextureFileDataIDs[adt.texChunks[c].layers[li].textureId]));
                        curMat = materials.Where(material => material.filename == adt.diffuseTextureFileDataIDs[adt.texChunks[c].layers[li].textureId].ToString()).Single();
                    }

                    layerscales.Add(curMat.scale);
                    layerheights.Add(curMat.heightTexture);

                    batch.heightScales[li] = curMat.heightScale;
                    batch.heightOffsets[li] = curMat.heightOffset;

                }

                batch.materialID = layerMaterials.ToArray();
                batch.alphaMaterialID = alphalayermats.ToArray();
                batch.scales = layerscales.ToArray();
                batch.heightMaterialIDs = layerheights.ToArray();

                var indices = indicelist.ToArray();
                var vertices = verticelist.ToArray();

                GL.BindBuffer(BufferTarget.ArrayBuffer, result.vertexBuffer);
                GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(vertices.Count() * 12 * sizeof(float)), vertices, BufferUsageHint.StaticDraw);

                //var normalAttrib = GL.GetAttribLocation(shaderProgram, "normal");
                //GL.EnableVertexAttribArray(normalAttrib);
                //GL.VertexAttribPointer(normalAttrib, 3, VertexAttribPointerType.Float, false, sizeof(float) * 11, sizeof(float) * 0);

                var colorAttrib = GL.GetAttribLocation(shaderProgram, "color");
                GL.EnableVertexAttribArray(colorAttrib);
                GL.VertexAttribPointer(colorAttrib, 4, VertexAttribPointerType.Float, false, sizeof(float) * 12, sizeof(float) * 3);

                var texCoordAttrib = GL.GetAttribLocation(shaderProgram, "texCoord");
                GL.EnableVertexAttribArray(texCoordAttrib);
                GL.VertexAttribPointer(texCoordAttrib, 2, VertexAttribPointerType.Float, false, sizeof(float) * 12, sizeof(float) * 7);

                var posAttrib = GL.GetAttribLocation(shaderProgram, "position");
                GL.EnableVertexAttribArray(posAttrib);
                GL.VertexAttribPointer(posAttrib, 3, VertexAttribPointerType.Float, false, sizeof(float) * 12, sizeof(float) * 9);

                GL.BindBuffer(BufferTarget.ElementArrayBuffer, result.indiceBuffer);
                GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(indices.Length * sizeof(int)), indices, BufferUsageHint.StaticDraw);

                renderBatches.Add(batch);
            }

            var doodads = new List<Doodad>();
            var worldModelBatches = new List<WorldModelBatch>();

            if (loadModels)
            {
                for (var mi = 0; mi < adt.objects.models.entries.Count(); mi++)
                {
                    Console.WriteLine("Loading model #" + mi);

                    var modelentry = adt.objects.models.entries[mi];
                    var mmid = adt.objects.m2NameOffsets.offsets[modelentry.mmidEntry];

                    var modelFileName = "";
                    for (var mmi = 0; mmi < adt.objects.m2Names.offsets.Count(); mmi++)
                    {
                        if (adt.objects.m2Names.offsets[mmi] == mmid)
                        {
                            modelFileName = adt.objects.m2Names.filenames[mmi].ToLower();
                            break;
                        }
                    }

                    doodads.Add(new Doodad
                    {
                        filename = modelFileName,
                        position = new Vector3(-(modelentry.position.X - 17066), modelentry.position.Y, -(modelentry.position.Z - 17066)),
                        rotation = new Vector3(modelentry.rotation.X, modelentry.rotation.Y, modelentry.rotation.Z),
                        scale = modelentry.scale
                    });

                    M2Loader.LoadM2(modelFileName, shaderProgram);
                }

                for (var wmi = 0; wmi < adt.objects.worldModels.entries.Count(); wmi++)
                {
                    var wmoFileName = "";

                    var wmodelentry = adt.objects.worldModels.entries[wmi];
                    var mwid = adt.objects.wmoNameOffsets.offsets[wmodelentry.mwidEntry];

                    for (var wmfi = 0; wmfi < adt.objects.wmoNames.offsets.Count(); wmfi++)
                    {
                        if (adt.objects.wmoNames.offsets[wmfi] == mwid)
                        {
                            wmoFileName = adt.objects.wmoNames.filenames[wmfi].ToLower();
                            break;
                        }
                    }

                    if (wmoFileName.Length == 0)
                        throw new Exception("Unable to find filename for WMO!");

                    worldModelBatches.Add(new WorldModelBatch
                    {
                        position = new Vector3(-(wmodelentry.position.X - 17066.666f), wmodelentry.position.Y, -(wmodelentry.position.Z - 17066.666f)),
                        rotation = new Vector3(wmodelentry.rotation.X, wmodelentry.rotation.Y, wmodelentry.rotation.Z),
                        worldModel = WMOLoader.LoadWMO(wmoFileName, shaderProgram)
                    });
                }
            }

            result.renderBatches = renderBatches.ToArray();
            result.doodads = doodads.ToArray();
            result.worldModelBatches = worldModelBatches.ToArray();

            // Clean-up.
            foreach (var batch in renderBatches)
                GL.DeleteTextures(batch.alphaMaterialID.Length, batch.alphaMaterialID);

            GC.Collect();

            return result;
        }
    }
}
