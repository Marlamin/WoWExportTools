using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using WoWFormatLib.FileReaders;
using WoWFormatLib.SereniaBLPLib;
using WoWFormatLib.Utils;

namespace WoWExportTools.Exporters.OBJ
{
    public class WMOExporter
    {
        public static void ExportWMO(string file, BackgroundWorker exportworker = null, string destinationOverride = null, short doodadSetExportID = short.MaxValue)
        {
            if (!Listfile.TryGetFileDataID(file, out var filedataid))
            {
                CASCLib.Logger.WriteLine("Error! Could not find filedataid for " + file + ", skipping export!");
                return;
            }
            else
            {
                ExportWMO(filedataid, exportworker, destinationOverride, doodadSetExportID, file);
            }
        }

        public static void ExportWMO(uint fileDataID, BackgroundWorker exportworker = null, string destinationOverride = null, short doodadSetExportID = short.MaxValue, string fileName = "")
        {
            if (exportworker == null)
            {
                exportworker = new BackgroundWorker();
                exportworker.WorkerReportsProgress = true;
            }

            if (string.IsNullOrEmpty(fileName))
                if (!Listfile.TryGetFilename(fileDataID, out fileName))
                    CASCLib.Logger.WriteLine("Warning! Could not find filename for " + fileDataID + "!");

            fileName = fileName.ToLower();

            Console.WriteLine("Loading WMO file..");
            exportworker.ReportProgress(5, "Reading WMO..");

            var outDir = ConfigurationManager.AppSettings["outdir"];
            var wmo = new WMOReader().LoadWMO(fileDataID, 0, fileName);

            var customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            exportworker.ReportProgress(30, "Reading WMO..");

            uint totalVertices = 0;
            var groups = new Structs.WMOGroup[wmo.group.Count()];

            for (var g = 0; g < wmo.group.Count(); g++)
            {
                Console.WriteLine("Loading group #" + g);
                if (wmo.group[g].mogp.vertices == null)
                {
                    Console.WriteLine("Group has no vertices!");
                    continue;
                }

                for (var i = 0; i < wmo.groupNames.Count(); i++)
                    if (wmo.group[g].mogp.nameOffset == wmo.groupNames[i].offset)
                        groups[g].name = wmo.groupNames[i].name.Replace(" ", "_");

                if (groups[g].name == "antiportal")
                {
                    Console.WriteLine("Group is antiportal");
                    continue;
                }

                groups[g].verticeOffset = totalVertices;
                groups[g].vertices = new Structs.Vertex[wmo.group[g].mogp.vertices.Count()];

                for (var i = 0; i < wmo.group[g].mogp.vertices.Count(); i++)
                {
                    groups[g].vertices[i].Position = new Structs.Vector3D()
                    {
                        X = wmo.group[g].mogp.vertices[i].vector.X * -1,
                        Y = wmo.group[g].mogp.vertices[i].vector.Z,
                        Z = wmo.group[g].mogp.vertices[i].vector.Y
                    };

                    groups[g].vertices[i].Normal = new Structs.Vector3D()
                    {
                        X = wmo.group[g].mogp.normals[i].normal.X,
                        Y = wmo.group[g].mogp.normals[i].normal.Z,
                        Z = wmo.group[g].mogp.normals[i].normal.Y
                    };

                    groups[g].vertices[i].TexCoord = new Structs.Vector2D()
                    {
                        X = wmo.group[g].mogp.textureCoords[0][i].X,
                        Y = wmo.group[g].mogp.textureCoords[0][i].Y
                    };

                    totalVertices++;
                }

                var indicelist = new List<uint>();
                for (var i = 0; i < wmo.group[g].mogp.indices.Count(); i++)
                    indicelist.Add(wmo.group[g].mogp.indices[i].indice);

                groups[g].indices = indicelist.ToArray();
            }

            // Create output directory.
            if (destinationOverride == null)
                Directory.CreateDirectory(fileName == null ? outDir : Path.Combine(outDir, Path.GetDirectoryName(fileName)));

            exportworker.ReportProgress(55, "Exporting WMO doodads..");

            List<string> doodadList = new List<string>();
            doodadList.Add("ModelFile;PositionX;PositionY;PositionZ;RotationW;RotationX;RotationY;RotationZ;ScaleFactor;DoodadSet");

            if (doodadSetExportID > -1)
            {
                for (var i = 0; i < wmo.doodadSets.Count(); i++)
                {
                    var doodadSet = wmo.doodadSets[i];
                    var currentDoodadSetName = doodadSet.setName.Replace("Set_", "").Replace("SET_", "").Replace("$DefaultGlobal", "Default");

                    if (doodadSetExportID != short.MaxValue)
                    {
                        if (i != 0 && i != doodadSetExportID)
                        {
                            Console.WriteLine("Skipping doodadset with ID " + i + " (" + currentDoodadSetName + ") because export filter is set to " + doodadSetExportID);
                            continue;
                        }
                    }

                    for (var j = doodadSet.firstInstanceIndex; j < (doodadSet.firstInstanceIndex + doodadSet.numDoodads); j++)
                    {
                        var doodadDefinition = wmo.doodadDefinitions[j];

                        var doodadFileName = "";
                        uint doodadFileDataID = 0;
                        var doodadNotFound = false;

                        if (wmo.doodadIds != null)
                        {
                            doodadFileDataID = wmo.doodadIds[doodadDefinition.offset];
                            if (!Listfile.TryGetFilename(doodadFileDataID, out doodadFileName))
                            {
                                CASCLib.Logger.WriteLine("Could not find filename for " + doodadFileDataID + ", setting filename to filedataid..");
                                doodadFileName = doodadFileDataID.ToString();
                            }
                        }
                        else
                        {
                            CASCLib.Logger.WriteLine("Warning!! File " + fileName + " ID: " + fileDataID + " still has filenames!");
                            foreach (var doodadNameEntry in wmo.doodadNames)
                            {
                                if (doodadNameEntry.startOffset == doodadDefinition.offset)
                                {
                                    doodadFileName = doodadNameEntry.filename.Replace(".MDX", ".M2").Replace(".MDL", ".M2").ToLower();
                                    if (!Listfile.TryGetFileDataID(doodadFileName, out doodadFileDataID))
                                    {
                                        CASCLib.Logger.WriteLine("Error! Could not find filedataid for " + doodadFileName + "!");
                                        doodadNotFound = true;
                                        continue;
                                    }
                                }
                            }
                        }

                        if (!doodadNotFound)
                        {
                            string objFileName = Path.GetFileNameWithoutExtension(doodadFileName ?? doodadFileDataID.ToString()) + ".obj";
                            string objPath = Path.Combine(destinationOverride ?? outDir, destinationOverride != null ? "" : Path.GetDirectoryName(fileName));
                            string objName = Path.Combine(objPath, objFileName);

                            if (!File.Exists(objName))
                                M2Exporter.ExportM2(doodadFileDataID, null, objPath, doodadFileName);

                            if (File.Exists(objName))
                                doodadList.Add(objFileName + ";" + doodadDefinition.position.X.ToString("F09") + ";" + doodadDefinition.position.Y.ToString("F09") + ";" + doodadDefinition.position.Z.ToString("F09") + ";" + doodadDefinition.rotation.W.ToString("F15") + ";" + doodadDefinition.rotation.X.ToString("F15") + ";" + doodadDefinition.rotation.Y.ToString("F15") + ";" + doodadDefinition.rotation.Z.ToString("F15") + ";" + doodadDefinition.scale + ";" + currentDoodadSetName);
                        }
                    }
                }
            }

            if (doodadList.Count > 1)
            {
                string mpiFile = (fileName == null ? fileDataID.ToString() : Path.GetFileNameWithoutExtension(fileName.Replace(" ", ""))) + "_ModelPlacementInformation.csv";
                File.WriteAllText(Path.Combine(outDir, destinationOverride ?? Path.GetDirectoryName(fileName), mpiFile), string.Join("\n", doodadList.ToArray()));
            }

            exportworker.ReportProgress(65, "Exporting WMO textures..");

            var mtlsb = new StringBuilder();
            var textureID = 0;

            if (wmo.materials == null)
            {
                CASCLib.Logger.WriteLine("Unable to find materials for WMO " + fileDataID + ", not exporting!");
                return;
            }

            var materials = new Structs.Material[wmo.materials.Count()];
            var extraMaterials = new List<Structs.Material>();

            for (var i = 0; i < wmo.materials.Count(); i++)
            {
                var blpReader = new BLPReader();

                if (wmo.textures == null)
                {
                    if (Listfile.TryGetFilename(wmo.materials[i].texture1, out var textureFilename))
                        materials[i].filename = Path.GetFileNameWithoutExtension(textureFilename).Replace(" ", "").ToLower();
                    else
                        materials[i].filename = wmo.materials[i].texture1.ToString();

                    blpReader.LoadBLP(wmo.materials[i].texture1);
                }
                else
                {
                    for (var ti = 0; ti < wmo.textures.Count(); ti++)
                    {
                        if (wmo.textures[ti].startOffset == wmo.materials[i].texture1)
                        {
                            materials[i].filename = Path.GetFileNameWithoutExtension(wmo.textures[ti].filename).Replace(" ", "").ToLower();
                            blpReader.LoadBLP(wmo.textures[ti].filename);
                        }
                    }
                }

                materials[i].textureID = textureID + i;
                materials[i].transparent = wmo.materials[i].blendMode != 0;

                materials[i].blendMode = wmo.materials[i].blendMode;
                materials[i].shaderID = wmo.materials[i].shader;
                materials[i].terrainType = wmo.materials[i].groundType;

                string saveLocation = Path.Combine(outDir, destinationOverride ?? Path.GetDirectoryName(fileName), materials[i].filename + ".png");
                if (!File.Exists(saveLocation))
                {
                    try
                    {
                        if (materials[i].transparent)
                            blpReader.bmp.Save(saveLocation);
                        else
                            blpReader.bmp.Clone(new Rectangle(0, 0, blpReader.bmp.Width, blpReader.bmp.Height), PixelFormat.Format32bppRgb).Save(saveLocation);
                    }
                    catch (Exception e)
                    {
                        CASCLib.Logger.WriteLine("Exception while saving BLP " + materials[i].filename + ": " + e.Message);
                    }
                }

                textureID++;

                string extraPath = Path.Combine(outDir, destinationOverride ?? Path.GetDirectoryName(fileName));
                ExportExtraMaterials(wmo.materials[i].texture2, wmo, extraMaterials, i, extraPath);
                ExportExtraMaterials(wmo.materials[i].texture3, wmo, extraMaterials, i, extraPath);
            }

            var numRenderbatches = 0;
            //Get total amount of render batches
            for (var i = 0; i < wmo.group.Count(); i++)
            {
                if (wmo.group[i].mogp.renderBatches == null)
                    continue;

                numRenderbatches = numRenderbatches + wmo.group[i].mogp.renderBatches.Count();
            }

            exportworker.ReportProgress(75, "Exporting WMO model..");

            bool exportMetadata = ConfigurationManager.AppSettings["textureMetadata"] == "True";

            //No idea how MTL files really work yet. Needs more investigation.
            foreach (var material in materials)
            {
                mtlsb.Append("newmtl " + material.filename + "\n");
                mtlsb.Append("Ns 96.078431\n");
                mtlsb.Append("Ka 1.000000 1.000000 1.000000\n");
                mtlsb.Append("Kd 0.640000 0.640000 0.640000\n");
                mtlsb.Append("Ks 0.000000 0.000000 0.000000\n");
                mtlsb.Append("Ke 0.000000 0.000000 0.000000\n");
                mtlsb.Append("Ni 1.000000\n");
                mtlsb.Append("d 1.000000\n");
                mtlsb.Append("illum 1\n");
                mtlsb.Append("map_Kd " + material.filename + ".png\n");

                if (material.transparent)
                    mtlsb.Append("map_d " + material.filename + ".png\n");

                if (exportMetadata)
                {
                    for (var g = 0; g < wmo.group.Count(); g++)
                    {
                        groups[g].renderBatches = new Structs.RenderBatch[numRenderbatches];

                        var group = wmo.group[g];
                        if (group.mogp.renderBatches == null)
                            continue;

                        for (var i = 0; i < group.mogp.renderBatches.Count(); i++)
                        {
                            var batch = group.mogp.renderBatches[i];
                            if (materials[batch.materialID].filename == material.filename)
                                mtlsb.Append("blend " + material.blendMode + "\n");
                        }
                    }
                }
            }

            foreach (var material in extraMaterials)
            {
                mtlsb.Append("newmtl " + material.filename + "\n");
                mtlsb.Append("Ns 96.078431\n");
                mtlsb.Append("Ka 1.000000 1.000000 1.000000\n");
                mtlsb.Append("Kd 0.640000 0.640000 0.640000\n");
                mtlsb.Append("Ks 0.000000 0.000000 0.000000\n");
                mtlsb.Append("Ke 0.000000 0.000000 0.000000\n");
                mtlsb.Append("Ni 1.000000\n");
                mtlsb.Append("d 1.000000\n");
                mtlsb.Append("illum 1\n");
                mtlsb.Append("map_Kd " + material.filename + ".png\n");

                if (material.transparent)
                    mtlsb.Append("map_d " + material.filename + ".png\n");
            }

            string mtlFile = fileName != null ? fileName.Replace(".wmo", ".mtl") : fileDataID + ".mtl";
            if (destinationOverride != null)
                mtlFile = Path.GetFileName(mtlFile);

            File.WriteAllText(Path.Combine(destinationOverride ?? outDir, mtlFile), mtlsb.ToString());

            var rb = 0;
            for (var g = 0; g < wmo.group.Count(); g++)
            {
                groups[g].renderBatches = new Structs.RenderBatch[numRenderbatches];

                var group = wmo.group[g];
                if (group.mogp.renderBatches == null)
                    continue;

                for (var i = 0; i < group.mogp.renderBatches.Count(); i++)
                {
                    var batch = group.mogp.renderBatches[i];

                    groups[g].renderBatches[rb].firstFace = batch.firstFace;
                    groups[g].renderBatches[rb].numFaces = batch.numFaces;
                    groups[g].renderBatches[rb].materialID = batch.flags == 2 ? (uint)batch.possibleBox2_3 : batch.materialID;
                    groups[g].renderBatches[rb].blendType = wmo.materials[batch.materialID].blendMode;
                    groups[g].renderBatches[rb].groupID = (uint)g;
                    rb++;
                }
            }

            exportworker.ReportProgress(95, "Writing WMO files..");

            string objFile = fileName != null ? fileName.Replace(".wmo", ".obj") : fileDataID + ".obj";
            if (destinationOverride != null)
                objFile = Path.GetFileName(objFile);

            string fileID = fileName ?? fileDataID.ToString();
            StreamWriter objWriter = new StreamWriter(Path.Combine(destinationOverride ?? outDir, objFile));
            objWriter.WriteLine("# Written by Marlamin's WoW Export Tools. Original file: " + fileID);
            objWriter.WriteLine("mtllib " + Path.GetFileNameWithoutExtension(fileID) + ".mtl");
            objWriter.WriteLine("o " + Path.GetFileName(fileID));

            foreach (var group in groups)
            {
                if (group.vertices == null)
                    continue;

                Console.WriteLine("Writing " + group.name);

                foreach (var vertex in group.vertices)
                {
                    objWriter.WriteLine("v " + vertex.Position.X + " " + vertex.Position.Y + " " + vertex.Position.Z);
                    objWriter.WriteLine("vt " + vertex.TexCoord.X + " " + (vertex.TexCoord.Y - 1) * -1);
                    objWriter.WriteLine("vn " + (-vertex.Normal.X).ToString("F12") + " " + vertex.Normal.Y.ToString("F12") + " " + vertex.Normal.Z.ToString("F12"));
                }

                var indices = group.indices;

                for (int rbi = 0; rbi < group.renderBatches.Count(); rbi++)
                {
                    var renderbatch = group.renderBatches[rbi];
                    var i = renderbatch.firstFace;
                    if (renderbatch.numFaces > 0)
                    {
                        objWriter.WriteLine("g " + group.name + rbi);
                        objWriter.WriteLine("usemtl " + materials[renderbatch.materialID].filename);
                        objWriter.WriteLine("s 1");
                        while (i < (renderbatch.firstFace + renderbatch.numFaces))
                        {
                            objWriter.WriteLine("f " + (indices[i] + group.verticeOffset + 1) + "/" + (indices[i] + group.verticeOffset + 1) + "/" + (indices[i] + group.verticeOffset + 1) + " " + (indices[i + 1] + group.verticeOffset + 1) + "/" + (indices[i + 1] + group.verticeOffset + 1) + "/" + (indices[i + 1] + group.verticeOffset + 1) + " " + (indices[i + 2] + group.verticeOffset + 1) + "/" + (indices[i + 2] + group.verticeOffset + 1) + "/" + (indices[i + 2] + group.verticeOffset + 1));
                            i = i + 3;
                        }
                    }
                }
            }
            objWriter.Close();
        }

        private static void ExportExtraMaterials(uint tex, WoWFormatLib.Structs.WMO.WMO wmo, List<Structs.Material> mats, int matIdx, string path)
        {
            Stream ms = null;
            if (CASC.FileExists(tex))
            {
                var mat = new Structs.Material();
                if (wmo.textures == null)
                {
                    if (Listfile.TryGetFilename(tex, out var textureFilename))
                        mat.filename = Path.GetFileNameWithoutExtension(textureFilename).Replace(" ", "");
                    else
                        mat.filename = tex.ToString();

                    ms = CASC.OpenFile(tex);
                }
                else
                {
                    for (var ti = 0; ti < wmo.textures.Count(); ti++)
                    {
                        if (wmo.textures[ti].startOffset == tex)
                        {
                            mat.filename = Path.GetFileNameWithoutExtension(wmo.textures[ti].filename).Replace(" ", "");
                            ms = CASC.OpenFile(wmo.textures[ti].filename);
                        }
                    }
                }

                if (ms == null)
                    return; // Can this even happen?

                string saveLocation = path + mat.filename + ".png";
                if (!File.Exists(saveLocation))
                {
                    try
                    {
                        using (BlpFile blp = new BlpFile(ms))
                            blp.GetBitmap(0).Save(saveLocation);
                    }
                    catch (Exception e)
                    {
                        CASCLib.Logger.WriteLine("Exception while saving BLP " + mat.filename + ": " + e.Message);
                    }
                }

                mats.Add(mat);
            }
        }
    }
}
