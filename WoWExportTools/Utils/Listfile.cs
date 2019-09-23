using CASCLib;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;

namespace WoWExportTools
{
    public static class Listfile
    {
        public static Dictionary<uint, string> FDIDToFilename = new Dictionary<uint, string>();
        public static Dictionary<string, uint> FilenameToFDID = new Dictionary<string, uint>();

        public static void Update()
        {
            using (var client = new WebClient())
            using (var stream = new MemoryStream())
            {
                client.Headers[HttpRequestHeader.AcceptEncoding] = "gzip";
                var responseStream = new GZipStream(client.OpenRead("https://wow.tools/casc/listfile/download/csv/unverified"), CompressionMode.Decompress);
                responseStream.CopyTo(stream);
                File.WriteAllBytes("listfile.csv", stream.ToArray());
                responseStream.Close();
                responseStream.Dispose();
            }
        }

        public static void Load()
        {
            if (!File.Exists("listfile.csv"))
            {
                Update();
            }

            using (var listfile = File.Open("listfile.csv", FileMode.Open))
            using (var reader = new StreamReader(listfile))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    string[] tokens = line.Split(';');

                    if (tokens.Length != 2)
                    {
                        Logger.WriteLine($"Invalid line in listfile: {line}");
                        continue;
                    }

                    if (!uint.TryParse(tokens[0], out uint fileDataId))
                    {
                        Logger.WriteLine($"Invalid line in listfile: {line}");
                        continue;
                    }

                    FDIDToFilename.Add(fileDataId, tokens[1]);

                    if (!FilenameToFDID.ContainsKey(tokens[1]))
                    {
                        FilenameToFDID.Add(tokens[1], fileDataId);
                    }
                }
            }
        }

        public static bool TryGetFileDataID(string filename, out uint fileDataID)
        {
            var cleaned = filename.ToLower().Replace('\\', '/');

            return FilenameToFDID.TryGetValue(cleaned, out fileDataID);
        }

        public static bool TryGetFilename(uint filedataid, out string filename)
        {
            return FDIDToFilename.TryGetValue(filedataid, out filename);
        }
    }
}
