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
using WoWFormatLib.Utils;

namespace WoWExportTools.Exporters.OBJ
{
    public class WMOExporter
    {
        public static void ExportWMO(string file, BackgroundWorker exportworker = null, string destinationOverride = null, ushort doodadSetExportID = ushort.MaxValue)
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

        public static void ExportWMO(uint filedataid, BackgroundWorker exportworker = null, string destinationOverride = null, ushort doodadSetExportID = ushort.MaxValue, string filename = "")
        {
            if (exportworker == null)
            {
                exportworker = new BackgroundWorker();
                exportworker.WorkerReportsProgress = true;
            }

            if (string.IsNullOrEmpty(filename))
            {
                if (!Listfile.TryGetFilename(filedataid, out filename))
                {
                    CASCLib.Logger.WriteLine("Warning! Could not find filename for " + filedataid + "!");
                }
            }

            Console.WriteLine("Loading WMO file..");

            exportworker.ReportProgress(5, "Reading WMO..");

            var outdir = ConfigurationManager.AppSettings["outdir"];
            var wmo = new WMOReader().LoadWMO(filedataid, 0, filename);

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
                { Console.WriteLine("Group has no vertices!"); continue; }
                for (var i = 0; i < wmo.groupNames.Count(); i++)
                {
                    if (wmo.group[g].mogp.nameOffset == wmo.groupNames[i].offset)
                    {
                        groups[g].name = wmo.groupNames[i].name.Replace(" ", "_");
                    }
                }

                if (groups[g].name == "antiportal")
                { Console.WriteLine("Group is antiportal"); continue; }

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
                {
                    indicelist.Add(wmo.group[g].mogp.indices[i].indice);
                }

                groups[g].indices = indicelist.ToArray();
            }

            if (destinationOverride == null)
            {
                // Create output directory
                if (!string.IsNullOrEmpty(filename))
                    Directory.CreateDirectory(Path.Combine(outdir, Path.GetDirectoryName(filename)));
                else
                    Directory.CreateDirectory(outdir);
            }



            exportworker.ReportProgress(55, "Exporting WMO doodads..");

            var doodadSB = new StringBuilder();
            doodadSB.AppendLine("ModelFile;PositionX;PositionY;PositionZ;RotationW;RotationX;RotationY;RotationZ;ScaleFactor;DoodadSet");

            for (var i = 0; i < wmo.doodadSets.Count(); i++)
            {
                var doodadSet = wmo.doodadSets[i];

                var currentDoodadSetName = doodadSet.setName.Replace("Set_", "").Replace("SET_", "").Replace("$DefaultGlobal", "Default");

                if (doodadSetExportID != ushort.MaxValue)
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

                    var doodadFilename = "";
                    uint doodadFileDataID = 0;
                    var doodadNotFound = false;

                    if (wmo.doodadIds != null)
                    {
                        doodadFileDataID = wmo.doodadIds[doodadDefinition.offset];
                        if (!Listfile.TryGetFilename(doodadFileDataID, out doodadFilename))
                        {
                            CASCLib.Logger.WriteLine("Could not find filename for " + doodadFileDataID + ", setting filename to filedataid..");
                            doodadFilename = doodadFileDataID.ToString();
                        }
                    }
                    else
                    {
                        CASCLib.Logger.WriteLine("Warning!! File " + filename + " ID: " + filedataid + " still has filenames!");
                        foreach (var doodadNameEntry in wmo.doodadNames)
                        {
                            if (doodadNameEntry.startOffset == doodadDefinition.offset)
                            {
                                doodadFilename = doodadNameEntry.filename.Replace(".MDX", ".M2").Replace(".MDL", ".M2").ToLower();
                                if (!Listfile.TryGetFileDataID(doodadFilename, out doodadFileDataID))
                                {
                                    CASCLib.Logger.WriteLine("Error! Could not find filedataid for " + doodadFilename + "!");
                                    doodadNotFound = true;
                                    continue;
                                }
                            }
                        }
                    }

                    if (!doodadNotFound)
                    {
                        if (destinationOverride == null)
                        {
                            if (!string.IsNullOrEmpty(doodadFilename))
                            {
                                if (!File.Exists(Path.Combine(outdir, Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(doodadFilename) + ".obj")))
                                {
                                    M2Exporter.ExportM2(doodadFileDataID, null, Path.Combine(outdir, Path.GetDirectoryName(filename)), doodadFilename);
                                }

                                if (File.Exists(Path.Combine(outdir, Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(doodadFilename) + ".obj")))
                                {
                                    doodadSB.AppendLine(Path.GetFileNameWithoutExtension(doodadFilename) + ".obj;" + doodadDefinition.position.X.ToString("F09") + ";" + doodadDefinition.position.Y.ToString("F09") + ";" + doodadDefinition.position.Z.ToString("F09") + ";" + doodadDefinition.rotation.W.ToString("F15") + ";" + doodadDefinition.rotation.X.ToString("F15") + ";" + doodadDefinition.rotation.Y.ToString("F15") + ";" + doodadDefinition.rotation.Z.ToString("F15") + ";" + doodadDefinition.scale + ";" + currentDoodadSetName);
                                }
                            }
                            else
                            {
                                if (!File.Exists(Path.Combine(outdir, doodadFileDataID + ".obj")))
                                {
                                    M2Exporter.ExportM2(doodadFileDataID, null, outdir, doodadFilename);
                                }

                                if (File.Exists(Path.Combine(outdir, doodadFileDataID + ".obj")))
                                {
                                    doodadSB.AppendLine(doodadFileDataID + ".obj;" + doodadDefinition.position.X.ToString("F09") + ";" + doodadDefinition.position.Y.ToString("F09") + ";" + doodadDefinition.position.Z.ToString("F09") + ";" + doodadDefinition.rotation.W.ToString("F15") + ";" + doodadDefinition.rotation.X.ToString("F15") + ";" + doodadDefinition.rotation.Y.ToString("F15") + ";" + doodadDefinition.rotation.Z.ToString("F15") + ";" + doodadDefinition.scale + ";" + currentDoodadSetName);
                                }
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(doodadFilename))
                            {
                                if (!File.Exists(Path.Combine(destinationOverride, Path.GetFileNameWithoutExtension(doodadFilename) + ".obj")))
                                {
                                    M2Exporter.ExportM2(doodadFileDataID, null, destinationOverride, doodadFilename);
                                }

                                if (File.Exists(Path.Combine(destinationOverride, Path.GetFileNameWithoutExtension(doodadFilename) + ".obj")))
                                {
                                    doodadSB.AppendLine(Path.GetFileNameWithoutExtension(doodadFilename) + ".obj;" + doodadDefinition.position.X.ToString("F09") + ";" + doodadDefinition.position.Y.ToString("F09") + ";" + doodadDefinition.position.Z.ToString("F09") + ";" + doodadDefinition.rotation.W.ToString("F15") + ";" + doodadDefinition.rotation.X.ToString("F15") + ";" + doodadDefinition.rotation.Y.ToString("F15") + ";" + doodadDefinition.rotation.Z.ToString("F15") + ";" + doodadDefinition.scale + ";" + currentDoodadSetName);
                                }
                            }
                            else
                            {
                                if (!File.Exists(Path.Combine(destinationOverride, doodadFileDataID + ".obj")))
                                {
                                    M2Exporter.ExportM2(doodadFileDataID, null, destinationOverride, doodadFilename);
                                }

                                if (File.Exists(Path.Combine(destinationOverride, doodadFileDataID + ".obj")))
                                {
                                    doodadSB.AppendLine(doodadFileDataID + ".obj;" + doodadDefinition.position.X.ToString("F09") + ";" + doodadDefinition.position.Y.ToString("F09") + ";" + doodadDefinition.position.Z.ToString("F09") + ";" + doodadDefinition.rotation.W.ToString("F15") + ";" + doodadDefinition.rotation.X.ToString("F15") + ";" + doodadDefinition.rotation.Y.ToString("F15") + ";" + doodadDefinition.rotation.Z.ToString("F15") + ";" + doodadDefinition.scale + ";" + currentDoodadSetName);
                                }
                            }
                        }
                    }
                }
            }

            if(doodadSB.ToString().Split('\n').Length > 2)
            {
                if (destinationOverride == null)
                {
                    if (!string.IsNullOrEmpty(filename))
                    {
                        File.WriteAllText(Path.Combine(outdir, Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename.Replace(" ", "")) + "_ModelPlacementInformation.csv"), doodadSB.ToString());
                    }
                    else
                    {
                        File.WriteAllText(Path.Combine(outdir, Path.GetDirectoryName(filename), filedataid + "_ModelPlacementInformation.csv"), doodadSB.ToString());
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(filename))
                    {
                        File.WriteAllText(Path.Combine(outdir, destinationOverride, Path.GetFileNameWithoutExtension(filename).Replace(" ", "") + "_ModelPlacementInformation.csv"), doodadSB.ToString());
                    }
                    else
                    {
                        File.WriteAllText(Path.Combine(outdir, destinationOverride, filedataid + "_ModelPlacementInformation.csv"), doodadSB.ToString());
                    }
                }
            }

            exportworker.ReportProgress(65, "Exporting WMO textures..");

            var mtlsb = new StringBuilder();
            var textureID = 0;

            if (wmo.materials == null)
            {
                CASCLib.Logger.WriteLine("Unable to find materials for WMO " + filedataid + ", not exporting!");
                return;
            }

            var materials = new Structs.Material[wmo.materials.Count()];
            var extraMaterials = new List<Structs.Material>();

            for (var i = 0; i < wmo.materials.Count(); i++)
            {
                var blpreader = new BLPReader();

                if (wmo.textures == null)
                {
                    if (Listfile.TryGetFilename(wmo.materials[i].texture1, out var textureFilename))
                    {
                        materials[i].filename = Path.GetFileNameWithoutExtension(textureFilename).Replace(" ", "");
                    }
                    else
                    {
                        materials[i].filename = wmo.materials[i].texture1.ToString();
                    }

                    blpreader.LoadBLP(wmo.materials[i].texture1);
                }
                else
                {
                    for (var ti = 0; ti < wmo.textures.Count(); ti++)
                    {
                        if (wmo.textures[ti].startOffset == wmo.materials[i].texture1)
                        {
                            materials[i].filename = Path.GetFileNameWithoutExtension(wmo.textures[ti].filename).Replace(" ", "");
                            blpreader.LoadBLP(wmo.textures[ti].filename);
                        }
                    }
                }

                materials[i].textureID = textureID + i;

                if (wmo.materials[i].blendMode == 0)
                {
                    materials[i].transparent = false;
                }
                else
                {
                    materials[i].transparent = true;
                }

                materials[i].blendMode = wmo.materials[i].blendMode;
                materials[i].shaderID = wmo.materials[i].shader;
                materials[i].terrainType = wmo.materials[i].groundType;

                string saveLocation;

                if (destinationOverride == null)
                {
                    if (!string.IsNullOrEmpty(filename))
                    {
                        saveLocation = Path.Combine(outdir, Path.GetDirectoryName(filename), materials[i].filename + ".png");
                    }
                    else
                    {
                        saveLocation = Path.Combine(outdir, materials[i].filename + ".png");
                    }
                }
                else
                {
                    saveLocation = Path.Combine(outdir, destinationOverride, materials[i].filename + ".png");
                }

                if (!File.Exists(saveLocation))
                {
                    try
                    {
                        if (materials[i].transparent)
                        {
                            blpreader.bmp.Save(saveLocation);
                        }
                        else
                        {
                            blpreader.bmp.Clone(new Rectangle(0, 0, blpreader.bmp.Width, blpreader.bmp.Height), PixelFormat.Format32bppRgb).Save(saveLocation);
                        }
                    }
                    catch (Exception e)
                    {
                        CASCLib.Logger.WriteLine("Exception while saving BLP " + materials[i].filename + ": " + e.Message);
                    }
                }

                textureID++;

                // Extra materials
                // Texture 2
                if (CASC.FileExists(wmo.materials[i].texture2))
                {
                    var tex2mat = new Structs.Material();
                    if (wmo.textures == null)
                    {
                        if (Listfile.TryGetFilename(wmo.materials[i].texture2, out var textureFilename))
                        {
                            tex2mat.filename = Path.GetFileNameWithoutExtension(textureFilename).Replace(" ", "");
                        }
                        else
                        {
                            tex2mat.filename = wmo.materials[i].texture2.ToString();
                        }

                        blpreader.LoadBLP(wmo.materials[i].texture2);
                    }
                    else
                    {
                        for (var ti = 0; ti < wmo.textures.Count(); ti++)
                        {
                            if (wmo.textures[ti].startOffset == wmo.materials[i].texture2)
                            {
                                tex2mat.filename = Path.GetFileNameWithoutExtension(wmo.textures[ti].filename).Replace(" ", "");
                                blpreader.LoadBLP(wmo.textures[ti].filename);
                            }
                        }
                    }

                    if (destinationOverride == null)
                    {
                        if (!string.IsNullOrEmpty(filename))
                        {
                            saveLocation = Path.Combine(outdir, Path.GetDirectoryName(filename), tex2mat.filename + ".png");
                        }
                        else
                        {
                            saveLocation = Path.Combine(outdir, tex2mat.filename + ".png");
                        }
                    }
                    else
                    {
                        saveLocation = Path.Combine(outdir, destinationOverride, tex2mat.filename + ".png");
                    }

                    if (!File.Exists(saveLocation))
                    {
                        try
                        {
                            blpreader.bmp.Save(saveLocation);
                        }
                        catch (Exception e)
                        {
                            CASCLib.Logger.WriteLine("Exception while saving BLP " + tex2mat.filename + ": " + e.Message);
                        }
                    }

                    extraMaterials.Add(tex2mat);
                }

                // Texture 3
                if (CASC.FileExists(wmo.materials[i].texture3))
                {
                    var tex3mat = new Structs.Material();
                    if (wmo.textures == null)
                    {
                        if (Listfile.TryGetFilename(wmo.materials[i].texture3, out var textureFilename))
                        {
                            tex3mat.filename = Path.GetFileNameWithoutExtension(textureFilename).Replace(" ", "");
                        }
                        else
                        {
                            tex3mat.filename = wmo.materials[i].texture3.ToString();
                        }

                        blpreader.LoadBLP(wmo.materials[i].texture3);
                    }
                    else
                    {
                        for (var ti = 0; ti < wmo.textures.Count(); ti++)
                        {
                            if (wmo.textures[ti].startOffset == wmo.materials[i].texture3)
                            {
                                tex3mat.filename = Path.GetFileNameWithoutExtension(wmo.textures[ti].filename).Replace(" ", "");
                                blpreader.LoadBLP(wmo.textures[ti].filename);
                            }
                        }
                    }

                    if (destinationOverride == null)
                    {
                        if (!string.IsNullOrEmpty(filename))
                        {
                            saveLocation = Path.Combine(outdir, Path.GetDirectoryName(filename), tex3mat.filename + ".png");
                        }
                        else
                        {
                            saveLocation = Path.Combine(outdir, tex3mat.filename + ".png");
                        }
                    }
                    else
                    {
                        saveLocation = Path.Combine(outdir, destinationOverride, tex3mat.filename + ".png");
                    }

                    if (!File.Exists(saveLocation))
                    {
                        try
                        {
                            blpreader.bmp.Save(saveLocation);
                        }
                        catch (Exception e)
                        {
                            CASCLib.Logger.WriteLine("Exception while saving BLP " + tex3mat.filename + ": " + e.Message);
                        }
                    }

                    extraMaterials.Add(tex3mat);
                }
            }

            var numRenderbatches = 0;
            //Get total amount of render batches
            for (var i = 0; i < wmo.group.Count(); i++)
            {
                if (wmo.group[i].mogp.renderBatches == null)
                {
                    continue;
                }
                numRenderbatches = numRenderbatches + wmo.group[i].mogp.renderBatches.Count();
            }

            exportworker.ReportProgress(75, "Exporting WMO model..");

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
                {
                    mtlsb.Append("map_d " + material.filename + ".png\n");
                }

                if (ConfigurationManager.AppSettings["textureMetadata"] == "True")
                {
                    for (var g = 0; g < wmo.group.Count(); g++)
                    {
                        groups[g].renderBatches = new Structs.RenderBatch[numRenderbatches];

                        var group = wmo.group[g];
                        if (group.mogp.renderBatches == null)
                        {
                            continue;
                        }

                        for (var i = 0; i < group.mogp.renderBatches.Count(); i++)
                        {
                            var batch = group.mogp.renderBatches[i];
                            if (materials[batch.materialID].filename == material.filename)
                            {
                                mtlsb.Append("blend " + material.blendMode + "\n");
                            }
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
                {
                    mtlsb.Append("map_d " + material.filename + ".png\n");
                }
            }

            if (!string.IsNullOrEmpty(filename))
            {
                if (destinationOverride == null)
                {
                    File.WriteAllText(Path.Combine(outdir, filename.Replace(".wmo", ".mtl")), mtlsb.ToString());
                }
                else
                {
                    File.WriteAllText(Path.Combine(outdir, destinationOverride, Path.GetFileName(filename.ToLower()).Replace(".wmo", ".mtl")), mtlsb.ToString());
                }
            }
            else
            {
                if (destinationOverride == null)
                {
                    File.WriteAllText(Path.Combine(outdir, filedataid + ".mtl"), mtlsb.ToString());
                }
                else
                {
                    File.WriteAllText(Path.Combine(outdir, destinationOverride, filedataid + ".mtl"), mtlsb.ToString());
                }
            }

            var rb = 0;
            for (var g = 0; g < wmo.group.Count(); g++)
            {
                groups[g].renderBatches = new Structs.RenderBatch[numRenderbatches];

                var group = wmo.group[g];
                if (group.mogp.renderBatches == null)
                {
                    continue;
                }

                for (var i = 0; i < group.mogp.renderBatches.Count(); i++)
                {
                    var batch = group.mogp.renderBatches[i];

                    groups[g].renderBatches[rb].firstFace = batch.firstFace;
                    groups[g].renderBatches[rb].numFaces = batch.numFaces;

                    if (batch.flags == 2)
                    {
                        groups[g].renderBatches[rb].materialID = (uint)batch.possibleBox2_3;
                    }
                    else
                    {
                        groups[g].renderBatches[rb].materialID = batch.materialID;
                    }
                    groups[g].renderBatches[rb].blendType = wmo.materials[batch.materialID].blendMode;
                    groups[g].renderBatches[rb].groupID = (uint)g;
                    rb++;
                }
            }

            exportworker.ReportProgress(95, "Writing WMO files..");

            StreamWriter objsw;
            if (!string.IsNullOrEmpty(filename))
            {
                if (destinationOverride == null)
                {
                    objsw = new StreamWriter(Path.Combine(outdir, filename.Replace(".wmo", ".obj")));
                }
                else
                {
                    objsw = new StreamWriter(Path.Combine(outdir, destinationOverride, Path.GetFileName(filename.ToLower()).Replace(".wmo", ".obj")));
                }

                objsw.WriteLine("# Written by Marlamin's WoW Export Tools. Original file: " + filename);
                objsw.WriteLine("mtllib " + Path.GetFileNameWithoutExtension(filename) + ".mtl");
                objsw.WriteLine("o " + Path.GetFileName(filename));
            }
            else
            {
                if (destinationOverride == null)
                {
                    objsw = new StreamWriter(Path.Combine(outdir, filedataid + ".obj"));
                }
                else
                {
                    objsw = new StreamWriter(Path.Combine(outdir, destinationOverride, filedataid + ".obj"));
                }

                objsw.WriteLine("# Written by Marlamin's WoW Export Tools. Original file id: " + filedataid);
                objsw.WriteLine("mtllib " + filedataid + ".mtl");
                objsw.WriteLine("o " + filedataid);
            }

            foreach (var group in groups)
            {
                if (group.vertices == null)
                {
                    continue;
                }

                Console.WriteLine("Writing " + group.name);

                foreach (var vertex in group.vertices)
                {
                    objsw.WriteLine("v " + vertex.Position.X + " " + vertex.Position.Y + " " + vertex.Position.Z);
                    objsw.WriteLine("vt " + vertex.TexCoord.X + " " + (vertex.TexCoord.Y - 1) * -1);
                    objsw.WriteLine("vn " + (-vertex.Normal.X).ToString("F12") + " " + vertex.Normal.Y.ToString("F12") + " " + vertex.Normal.Z.ToString("F12"));
                }

                var indices = group.indices;

                for (int rbi = 0; rbi < group.renderBatches.Count(); rbi++)
                {
                    var renderbatch = group.renderBatches[rbi];
                    var i = renderbatch.firstFace;
                    if (renderbatch.numFaces > 0)
                    {
                        objsw.WriteLine("g " + group.name + rbi);
                        objsw.WriteLine("usemtl " + materials[renderbatch.materialID].filename);
                        objsw.WriteLine("s 1");
                        while (i < (renderbatch.firstFace + renderbatch.numFaces))
                        {
                            objsw.WriteLine("f " + (indices[i] + group.verticeOffset + 1) + "/" + (indices[i] + group.verticeOffset + 1) + "/" + (indices[i] + group.verticeOffset + 1) + " " + (indices[i + 1] + group.verticeOffset + 1) + "/" + (indices[i + 1] + group.verticeOffset + 1) + "/" + (indices[i + 1] + group.verticeOffset + 1) + " " + (indices[i + 2] + group.verticeOffset + 1) + "/" + (indices[i + 2] + group.verticeOffset + 1) + "/" + (indices[i + 2] + group.verticeOffset + 1));
                            i = i + 3;
                        }
                    }
                }
            }
            objsw.Close();
        }
    }
}
