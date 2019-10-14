using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.IO;
using WoWFormatLib.FileReaders;
using WoWFormatLib.Structs.MDX;

namespace WoWExportTools.Exporters.OBJ
{
    class MDXExporter
    {
        public static void ExportMDX(MDXReader reader, string outFile, BackgroundWorker worker = null)
        {
            if (worker == null)
                worker = new BackgroundWorker { WorkerReportsProgress = true };

            var customCulture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = customCulture;

            string mtlFile = Path.GetFileNameWithoutExtension(outFile) + ".mtl";
            string mtlPath = Path.Combine(Path.GetDirectoryName(outFile), mtlFile);

            worker.ReportProgress(30, "Reading MDX file...");
            MDXModel model = reader.model;

            // Empty model.
            if (model.geosets != null)
                return;

            worker.ReportProgress(60, "Writing material library...");
            // Write the material library.

            if (model.textures != null)
            {
                StreamWriter writerMTL = new StreamWriter(mtlPath);
                for (int i = 0; i < model.textures.Length; i++)
                {
                    string rawFile = Path.GetFileNameWithoutExtension(model.textures[i]);
                    writerMTL.WriteLine("newmtl {0}", rawFile);
                    writerMTL.WriteLine("illum 1");
                    writerMTL.WriteLine("map_Kd {0}.dds\n", rawFile);
                }

                writerMTL.Close();
            }

            worker.ReportProgress(90, "Writing OBJ...");
            StreamWriter writerOBJ = new StreamWriter(outFile);

            writerOBJ.WriteLine("# Exported using Marlamin's WoW Export Tools. MDX Exporter by Kruithne.");
            writerOBJ.WriteLine("# Model: {0} (MDX version {1})\n", model.name, model.version);

            writerOBJ.WriteLine("mtllib {0}\n", mtlFile);

            // Object Name
            writerOBJ.WriteLine("o {0}", model.name);

            // Instead of writing verts/normals/uvs for each geoset in order, we instead
            // batch all of them together in three large lists at the top.

            writerOBJ.WriteLine("\n# Verticies");
            for (int geosetIndex = 0; geosetIndex < model.geosets.Length; geosetIndex++)
            {
                Geoset geoset = model.geosets[geosetIndex];
                for (int i = 0; i < geoset.verts.Length; i++)
                    writerOBJ.WriteLine("v {0} {1} {2}", geoset.verts[i].x, geoset.verts[i].z, -geoset.verts[i].y);
            }

            writerOBJ.WriteLine("\n# Normals");
            for (int geosetIndex = 0; geosetIndex < model.geosets.Length; geosetIndex++)
            {
                Geoset geoset = model.geosets[geosetIndex];
                for(int i = 0; i < geoset.normals.Length; i++)
                    writerOBJ.WriteLine("vn {0} {1} {2}", geoset.normals[i].x, geoset.normals[i].y, geoset.normals[i].z);
            }

            writerOBJ.WriteLine("\n# UVs");
            for (int geosetIndex = 0; geosetIndex < model.geosets.Length; geosetIndex++)
            {
                Geoset geoset = model.geosets[geosetIndex];
                for (int i = 0; i < geoset.uvs.Length; i++)
                    writerOBJ.WriteLine("vt {0} {1}", geoset.uvs[i].x, geoset.uvs[i].y * -1); // Flip the Y UV, because it's backwards?
            }

            // Write geoset meshes together.
            long faceIndex = 0;
            for (int geosetIndex = 0; geosetIndex < model.geosets.Length; geosetIndex++)
            {
                Geoset geoset = model.geosets[geosetIndex];
                writerOBJ.WriteLine("\ng {0}", geoset.name);

                if (model.textures != null)
                {
                    string textureFile = model.textures[model.materials[geoset.materialIndex].textureID];
                    writerOBJ.WriteLine("usemtl {0}", Path.GetFileNameWithoutExtension(textureFile));
                    writerOBJ.WriteLine("s 1");
                }

                // +1 to each face to account for OBJ not liking zero-indexed lists.
                for (int i = 0; i < geoset.primitives.Length; i++)
                    writerOBJ.WriteLine("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}", faceIndex + geoset.primitives[i].v1 + 1, faceIndex + geoset.primitives[i].v2 + 1, faceIndex + geoset.primitives[i].v3 + 1);

                // Maintain absolute offset rather than relative.
                faceIndex += geoset.verts.Length;
            }

            writerOBJ.Close();
        }
    }
}
