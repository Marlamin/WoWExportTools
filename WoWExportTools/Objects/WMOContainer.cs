namespace WoWExportTools.Objects
{
    public class WMOContainer : Container3D
    {
        public Renderer.Structs.WorldModel WorldModel { get; }
        public bool[] EnabledGroups { get; }
        public bool[] EnabledDoodadSets { get; }

        public WMOContainer(Renderer.Structs.WorldModel wmo, string fileName) : base(fileName)
        {
            WorldModel = wmo;
            EnabledGroups = new bool[wmo.groupBatches.Length];
            EnabledDoodadSets = new bool[wmo.doodadSets.Length];

            // Is there no way to initialize an array of true bools?
            for (int i = 0; i < EnabledGroups.Length; i++)
                EnabledGroups[i] = true;

            for (int i = 0; i < EnabledDoodadSets.Length; i++)
                EnabledDoodadSets[i] = true;
        }
    }
}
