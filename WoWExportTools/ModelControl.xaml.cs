using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;

namespace WoWExportTools
{
    public class ModelGeoset : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private uint _index;
        private string _name;

        public Renderer.Structs.DoodadBatch Model;

        public uint Index
        {
            get { return _index; }
            set
            {
                if (value != _index)
                {
                    _index = value;
                    Notify("DisplayName");
                }
            }
        }

        public string Name
        {
            get { return _name; }
            set
            {
                if (value != _name)
                {
                    _name = value;
                    Notify("DisplayName");
                }
            }
        }

        public bool IsEnabled
        {
            get { return Model.submeshes[_index].enabled; }
            set
            {
                Model.submeshes[_index].enabled = value;
                Notify("IsEnabled");
            }
        }

        public string DisplayName
        {
            get
            {
                if (Name != null)
                    return Index + " (" + Name + ")";

                return Index.ToString();
            }
        }

        private void Notify(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class ModelControl : Window
    {
        private static string MAPPING_FILE = "geosets.txt";
        public static ModelControl instance = new ModelControl();
        private static Dictionary<string, Dictionary<uint, string>> geosetMaps = new Dictionary<string, Dictionary<uint, string>>();

        public static void LoadGeosetMapping()
        {
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

        public static void SetActiveModel(Renderer.Structs.DoodadBatch model)
        {
            instance.ActiveModel = model;
        }

        public static void ShowModelControl(string fileName)
        {
            instance.GeosetNameMap = null;
            if (geosetMaps.ContainsKey(fileName))
                instance.GeosetNameMap = geosetMaps[fileName];

            instance.Show();
        }

        public static void HideModelControl()
        {
            if (instance != null)
                instance.Hide();
        }

        public static void CloseModelControl()
        {
            if (instance != null)
                instance.Close();
        }

        public static bool IsModelControlActive()
        {
            return instance != null && instance.IsVisible;
        }

        public Dictionary<uint, string> GeosetNameMap = null;
        public ObservableCollection<ModelGeoset> activeModelGeosets;
        private Renderer.Structs.DoodadBatch _activeModel;

        public ModelControl()
        {
            InitializeComponent();

            // Link the list control to automatically update using activeModelGeosets.
            activeModelGeosets = new ObservableCollection<ModelGeoset>();
            geosetList.DataContext = activeModelGeosets;
        }

        public Renderer.Structs.DoodadBatch ActiveModel
        {
            get { return _activeModel; }
            set
            {
                _activeModel = value;
                activeModelGeosets.Clear();

                for (uint i = 0; i < _activeModel.submeshes.Length; i++)
                {
                    var mesh = _activeModel.submeshes[i];
                    string name = "Unknown";

                    if (GeosetNameMap != null && GeosetNameMap.ContainsKey(i))
                        name = GeosetNameMap[i];

                    activeModelGeosets.Add(new ModelGeoset() { Model = _activeModel, Name = name, Index = i });
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
