using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using WoWFormatLib.FileReaders;
using WoWFormatLib.Utils;

namespace WoWExportTools.Exporters.OBJ
{
    class M2Exporter
    {
        private static uint DEFAULT_TEXTURE = 840426; // dungeons/textures/testing/color_13.blp
        private static uint DEFAULT_TEXTURE_SUB = 186184; // textures/shanecube.blp

        public static void ExportM2(string file, BackgroundWorker exportworker = null, string destinationOverride = null)
        {
            if (!Listfile.TryGetFileDataID(file, out var filedataid))
            {
                CASCLib.Logger.WriteLine("Error! Could not find filedataid for " + file + ", skipping export!");
                return;
            }
            else
            {
                ExportM2(filedataid, exportworker, destinationOverride, file);
            }
        }

        public static void ExportM2(uint fileDataID, BackgroundWorker exportworker = null, string destinationOverride = null, string filename = "")
        {
            if (!CASC.FileExists(fileDataID))
                throw new Exception("404 M2 Not Found!");

            var reader = new M2Reader();
            reader.LoadM2(fileDataID);

            // Default to using fileDataID as a name if nothing is provided.
            if (string.IsNullOrEmpty(filename))
                filename = fileDataID.ToString();

            ExportM2(reader, filename, exportworker, destinationOverride);
        }

        public static void ExportM2(M2Reader reader, string fileName, BackgroundWorker exportworker = null, string destinationOverride = null, bool externalOverride = false)
        {
            if (exportworker == null)
            {
                exportworker = new BackgroundWorker
                {
                    WorkerReportsProgress = true
                };
            }

            var customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            var exportDir = ConfigurationManager.AppSettings["outdir"];
            exportworker.ReportProgress(15, "Reading M2..");

            // Don't export models without vertices
            if (reader.model.vertices.Count() == 0)
                return;

            var vertices = new Structs.Vertex[reader.model.vertices.Count()];
            for (var i = 0; i < reader.model.vertices.Count(); i++)
            {
                vertices[i].Position = new Structs.Vector3D()
                {
                    X = reader.model.vertices[i].position.X,
                    Y = reader.model.vertices[i].position.Z,
                    Z = reader.model.vertices[i].position.Y * -1
                };

                vertices[i].Normal = new Structs.Vector3D()
                {
                    X = reader.model.vertices[i].normal.X,
                    Y = reader.model.vertices[i].normal.Z,
                    Z = reader.model.vertices[i].normal.Y
                };

                vertices[i].TexCoord = new Structs.Vector2D()
                {
                    X = reader.model.vertices[i].textureCoordX,
                    Y = reader.model.vertices[i].textureCoordY
                };
            }

            string outDir = exportDir;
            if (destinationOverride != null)
            {
                if (externalOverride)
                    outDir = destinationOverride;
                else
                    outDir = Path.Combine(outDir, destinationOverride);
            }


            outDir = Path.Combine(outDir, Path.GetDirectoryName(fileName));
            if (!Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            string filePath = Path.Combine(outDir, Path.GetFileName(fileName).Replace(".m2", ""));
            string objFilePath = filePath + ".obj";
            string mtlFilePath = filePath + ".mtl";

            StreamWriter objWriter = new StreamWriter(objFilePath);
            StreamWriter mtlWriter = new StreamWriter(mtlFilePath);

            // Write OBJ header.
            objWriter.WriteLine("# Written by Marlamin's WoW Export Tools. Source file: " + fileName);
            objWriter.WriteLine("mtllib " + Path.GetFileName(mtlFilePath));

            foreach (var vertex in vertices)
            {
                objWriter.WriteLine("v " + vertex.Position.X + " " + vertex.Position.Y + " " + vertex.Position.Z);
                objWriter.WriteLine("vt " + vertex.TexCoord.X + " " + (vertex.TexCoord.Y - 1) * -1);
                objWriter.WriteLine("vn " + (-vertex.Normal.X).ToString("F12") + " " + vertex.Normal.Y.ToString("F12") + " " + vertex.Normal.Z.ToString("F12"));
            }

            var indicelist = new List<uint>();
            for (var i = 0; i < reader.model.skins[0].triangles.Count(); i++)
            {
                var t = reader.model.skins[0].triangles[i];
                indicelist.Add(t.pt1);
                indicelist.Add(t.pt2);
                indicelist.Add(t.pt3);
            }

            var indices = indicelist.ToArray();
            exportworker.ReportProgress(35, "Writing files..");

            var renderbatches = new Structs.RenderBatch[reader.model.skins[0].submeshes.Count()];
            for (var i = 0; i < reader.model.skins[0].submeshes.Count(); i++)
            {
                renderbatches[i].firstFace = reader.model.skins[0].submeshes[i].startTriangle;
                renderbatches[i].numFaces = reader.model.skins[0].submeshes[i].nTriangles;
                renderbatches[i].groupID = (uint)i;
                for (var tu = 0; tu < reader.model.skins[0].textureunit.Count(); tu++)
                {
                    if (reader.model.skins[0].textureunit[tu].submeshIndex == i)
                    {
                        renderbatches[i].blendType = reader.model.renderflags[reader.model.skins[0].textureunit[tu].renderFlags].blendingMode;
                        renderbatches[i].materialID = reader.model.texlookup[reader.model.skins[0].textureunit[tu].texture].textureID;
                    }
                }
            }

            exportworker.ReportProgress(65, "Exporting textures..");

            uint defaultTexID = DEFAULT_TEXTURE;
            if (!CASC.FileExists(defaultTexID))
                defaultTexID = DEFAULT_TEXTURE_SUB;

            var textureID = 0;
            var materials = new Structs.Material[reader.model.textures.Count()];

            for (var i = 0; i < reader.model.textures.Count(); i++)
            {
                uint textureFileDataID = defaultTexID;

                materials[i].flags = reader.model.textures[i].flags;

                if (reader.model.textures[i].type == 0)
                {
                    if (reader.model.textureFileDataIDs != null && reader.model.textureFileDataIDs.Length > 0 && reader.model.textureFileDataIDs[i] != 0)
                        textureFileDataID = reader.model.textureFileDataIDs[i];
                    else
                        Listfile.TryGetFileDataID(reader.model.textures[i].filename, out textureFileDataID);
                }
                else
                {
                    Console.WriteLine("Texture type " + reader.model.textures[i].type + " not supported, falling back to placeholder texture");
                }

                materials[i].textureID = textureID + i;

                if (!Listfile.TryGetFilename(textureFileDataID, out var textureFilename))
                    textureFilename = textureFileDataID.ToString();

                materials[i].filename = Path.GetFileNameWithoutExtension(textureFilename);

                try
                {
                    var blpreader = new BLPReader();
                    blpreader.LoadBLP(textureFileDataID);
                    blpreader.bmp.Save(Path.Combine(outDir, materials[i].filename + ".png"));
                }
                catch (Exception e)
                {
                    CASCLib.Logger.WriteLine("Exception while saving BLP " + materials[i].filename + ": " + e.Message);
                }
            }

            exportworker.ReportProgress(85, "Writing files..");

            foreach (var material in materials)
            {
                mtlWriter.WriteLine("newmtl " + material.filename);
                mtlWriter.WriteLine("illum 1");
                //mtlsb.WriteLine("map_Ka " + material.filename + ".png");
                mtlWriter.WriteLine("map_Kd " + material.filename + ".png");
            }

            mtlWriter.Close();

            objWriter.WriteLine("o " + Path.GetFileName(fileName));

            foreach (var renderbatch in renderbatches)
            {
                var i = renderbatch.firstFace;

                objWriter.WriteLine("g " + renderbatch.groupID);
                objWriter.WriteLine("usemtl " + materials[renderbatch.materialID].filename);
                objWriter.WriteLine("s 1");
                while (i < (renderbatch.firstFace + renderbatch.numFaces))
                {
                    objWriter.WriteLine("f " + (indices[i] + 1) + "/" + (indices[i] + 1) + "/" + (indices[i] + 1) + " " + (indices[i + 1] + 1) + "/" + (indices[i + 1] + 1) + "/" + (indices[i + 1] + 1) + " " + (indices[i + 2] + 1) + "/" + (indices[i + 2] + 1) + "/" + (indices[i + 2] + 1));
                    i = i + 3;
                }
            }

            objWriter.Close();

            // Only export phys when exporting a single M2, causes issues for some users when combined with WMO/ADT
            if (destinationOverride == null)
            {
                exportworker.ReportProgress(90, "Exporting collision..");

                objWriter = new StreamWriter(filePath + ".phys.obj");


                objWriter.WriteLine("# Written by Marlamin's WoW Export Tools. Source file: " + fileName);

                for (var i = 0; i < reader.model.boundingvertices.Count(); i++)
                {
                    objWriter.WriteLine("v " +
                         reader.model.boundingvertices[i].vertex.X + " " +
                         reader.model.boundingvertices[i].vertex.Z + " " +
                        -reader.model.boundingvertices[i].vertex.Y);
                }

                for (var i = 0; i < reader.model.boundingtriangles.Count(); i++)
                {
                    var t = reader.model.boundingtriangles[i];
                    objWriter.WriteLine("f " + (t.index_0 + 1) + " " + (t.index_1 + 1) + " " + (t.index_2 + 1));
                }

                objWriter.Close();
            }

            // https://en.wikipedia.org/wiki/Wavefront_.obj_file#Basic_materials
            // http://wiki.unity3d.com/index.php?title=ExportOBJ
            // http://web.cse.ohio-state.edu/~hwshen/581/Site/Lab3_files/Labhelp_Obj_parser.htm
        }
    }
}
