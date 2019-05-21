using DBCD.Providers;
using System.IO;
using WoWFormatLib.Utils;

namespace OBJExporterUI.DBC
{
    class CASCDBCProvider : IDBCProvider
    {
        public Stream StreamForTableName(string tableName)
        {
            if(Listfile.TryGetFileDataID("dbfilesclient/" + tableName + ".db2", out var fileDataID))
            {
                return CASC.OpenFile(fileDataID);
            }
            else
            {
                throw new FileNotFoundException("DBC " + tableName + " not found in listfile, could not look up filedataid!");
            }
        }
    }
}
