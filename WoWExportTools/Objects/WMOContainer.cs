namespace WoWExportTools.Objects
{
    class WMOContainer : Container3D
    {
        public Renderer.Structs.WorldModel WorldModel { get; }
        public WMOContainer(Renderer.Structs.WorldModel wmo, string fileName) : base(fileName)
        {
            WorldModel = wmo;
        }
    }
}
