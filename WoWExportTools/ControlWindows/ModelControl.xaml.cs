using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using WoWExportTools.Objects;

namespace WoWExportTools
{
    public class ModelGeoset : ControlOption
    {
        public M2Container Object;

        public bool IsEnabled
        {
            get { return Object.EnabledGeosets[_index]; }
            set
            {
                Object.EnabledGeosets[_index] = value;
                Notify("IsEnabled");
            }
        }
    }

    public partial class ModelControl : Window
    {
        private static string MAPPING_FILE = "geosets.txt";
        private Dictionary<string, Dictionary<uint, string>> geosetMaps = new Dictionary<string, Dictionary<uint, string>>();

        public Dictionary<uint, string> GeosetNameMap = null;
        public ObservableCollection<ModelGeoset> activeModelGeosets;

        private bool loadedGeosetMapping = false;

        private PreviewControl previewControl;

        public ModelControl(PreviewControl previewControl)
        {
            InitializeComponent();

            this.previewControl = previewControl;

            // Link the list control to automatically update using activeModelGeosets.
            activeModelGeosets = new ObservableCollection<ModelGeoset>();
            geosetList.DataContext = activeModelGeosets;
        }

        public void UpdateModelControl()
        {
            Container3D activeObject = previewControl.activeObject;

            if (activeObject != null && activeObject is M2Container m2Object)
            {
                activeModelGeosets.Clear();

                var model = m2Object.DoodadBatch;
                for (uint i = 0; i < model.submeshes.Length; i++)
                {
                    var mesh = model.submeshes[i];
                    string name = "Unknown";

                    if (geosetMaps.ContainsKey(m2Object.FileName))
                    {
                        var map = geosetMaps[m2Object.FileName];
                        if (map.ContainsKey(i))
                            name = map[i];
                    }

                    activeModelGeosets.Add(new ModelGeoset() { Object = m2Object, Name = name, Index = i });
                }
            }
        }

        public void LoadGeosetMapping()
        {
            if (loadedGeosetMapping)
                return;

            loadedGeosetMapping = true;

            if (!File.Exists(MAPPING_FILE))
                return;

            using (var mapFile = File.Open(MAPPING_FILE, FileMode.Open))
            using (var reader = new StreamReader(mapFile))
            {
                Dictionary<uint, string> currentMap = null;
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    // Skip empty lines.
                    if (line.Length == 0)
                        continue;

                    if (line.IndexOf('=') > -1 && currentMap != null)
                    {
                        string[] parts = line.Split(new char[] { '=' }, 2);
                        if (uint.TryParse(parts[0].Trim(), out uint geosetID))
                            if (!currentMap.ContainsKey(geosetID))
                                currentMap.Add(geosetID, parts[1].Trim());
                    }
                    else
                    {
                        if (currentMap == null || currentMap.Count > 0)
                            currentMap = new Dictionary<uint, string>();

                        geosetMaps.Add(line.ToLower(), currentMap);
                    }
                }
            }
        }

        private void GeosetEnableButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ModelGeoset item in activeModelGeosets)
                item.IsEnabled = true;
        }

        private void GeosetDisableButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (ModelGeoset item in activeModelGeosets)
                item.IsEnabled = false;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!MainWindow.shuttingDown)
            {
                Hide();
                e.Cancel = true;
            }
        }
    }
}
