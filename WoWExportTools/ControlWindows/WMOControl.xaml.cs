using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using WoWExportTools.Objects;

namespace WoWExportTools
{
    public class WMODoodadSet : ControlOption
    {
        public WMOContainer Object;

        public bool IsEnabled
        {
            get { return Object.EnabledDoodadSets[_index]; }
            set
            {
                Object.EnabledDoodadSets[_index] = value;
                Notify("IsEnabled");
            }
        }
    }

    public class WMOGroup : ControlOption
    {
        public WMOContainer Object;

        public bool IsEnabled
        {
            get { return Object.EnabledGroups[_index]; }
            set
            {
                Object.EnabledGroups[_index] = value;
                Notify("IsEnabled");
            }
        }
    }

    public partial class WMOControl : Window
    {
        public ObservableCollection<WMODoodadSet> activeDoodadSets;
        public ObservableCollection<WMOGroup> activeGroups;

        private PreviewControl previewControl;

        public WMOControl(PreviewControl previewControl)
        {
            InitializeComponent();
            this.previewControl = previewControl;

            activeDoodadSets = new ObservableCollection<WMODoodadSet>();
            activeGroups = new ObservableCollection<WMOGroup>();
            wmoSetsList.DataContext = activeDoodadSets;
            wmoGroupList.DataContext = activeGroups;
        }

        public void UpdateWMOControl()
        {
            Container3D activeObject = previewControl.activeObject;

            if (activeObject != null && activeObject is WMOContainer wmoObject)
            {
                var wmo = wmoObject.WorldModel;

                activeDoodadSets.Clear();
                for (uint i = 0; i < wmo.doodadSets.Length; i++)
                    activeDoodadSets.Add(new WMODoodadSet() { Object = wmoObject, Name = wmo.doodadSets[i], Index = i });

                activeGroups.Clear();
                for (uint j = 0; j < wmo.groupBatches.Length; j++)
                    activeGroups.Add(new WMOGroup() { Object = wmoObject, Name = wmo.groupBatches[j].groupName, Index = j });

                //wmo.groupBatches;
                //wmo.doodads
            }
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
