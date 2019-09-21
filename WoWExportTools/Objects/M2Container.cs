namespace WoWExportTools.Objects
{
    public class M2Container : Container3D
    {
        public Renderer.Structs.DoodadBatch DoodadBatch { get; }
        public bool[] EnabledGeosets { get; }
        public M2Container(Renderer.Structs.DoodadBatch m2, string fileName) : base(fileName)
        {
            DoodadBatch = m2;
            EnabledGeosets = new bool[m2.submeshes.Length];

            // Is there no way to initialize an array of true bools?
            for (int i = 0; i < EnabledGeosets.Length; i++)
                EnabledGeosets[i] = true;
        }
    }
}
