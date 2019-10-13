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

            worker.ReportProgress(15, "Reading MDX file...");
            MDXModel model = reader.model;

            StreamWriter writer = new StreamWriter(outFile);

            writer.WriteLine("# Exported using Marlamin's WoW Export Tools. MDX Exporter by Kruithne.");
            writer.WriteLine("# Model: {0} (MDX version {1})\n", model.name, model.version);

            // Object Name
            writer.WriteLine("o {0}", model.name);

            // Instead of writing verts/normals/uvs for each geoset in order, we instead
            // batch all of them together in three large lists at the top.

            writer.WriteLine("\n# Verticies");
            for (int geosetIndex = 0; geosetIndex < model.geosets.Length; geosetIndex++)
            {
                Geoset geoset = model.geosets[geosetIndex];
                for (int i = 0; i < geoset.verts.Length; i++)
                    writer.WriteLine("v {0} {1} {2}", geoset.verts[i].x, geoset.verts[i].z, -geoset.verts[i].y);
            }

            writer.WriteLine("\n# Normals");
            for (int geosetIndex = 0; geosetIndex < model.geosets.Length; geosetIndex++)
            {
                Geoset geoset = model.geosets[geosetIndex];
                for(int i = 0; i < geoset.normals.Length; i++)
                    writer.WriteLine("vn {0} {1} {2}", geoset.normals[i].x, geoset.normals[i].y, geoset.normals[i].z);
            }

            writer.WriteLine("\n# UVs");
            for (int geosetIndex = 0; geosetIndex < model.geosets.Length; geosetIndex++)
            {
                Geoset geoset = model.geosets[geosetIndex];
                for (int i = 0; i < geoset.uvs.Length; i++)
                    writer.WriteLine("vt {0} {1}", geoset.uvs[i].x, geoset.uvs[i].y * -1); // Flip the Y UV, because it's backwards?
            }

            // Write geoset meshes together.
            long faceIndex = 0;
            for (int geosetIndex = 0; geosetIndex < model.geosets.Length; geosetIndex++)
            {
                Geoset geoset = model.geosets[geosetIndex];
                writer.WriteLine("\ng {0}", geoset.name);

                // +1 to each face to account for OBJ not liking zero-indexed lists.
                for (int i = 0; i < geoset.primitives.Length; i++)
                    writer.WriteLine("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}", faceIndex + geoset.primitives[i].v1 + 1, faceIndex + geoset.primitives[i].v2 + 1, faceIndex + geoset.primitives[i].v3 + 1);

                // Maintain absolute offset rather than relative.
                faceIndex += geoset.verts.Length;
            }

            writer.Close();
        }
    }
}
