using System.ComponentModel;

namespace WoWExportTools
{
    public class ControlOption : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected uint _index;
        protected string _name;

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

        public string DisplayName
        {
            get
            {
                if (Name != null)
                    return Index + " (" + Name + ")";

                return Index.ToString();
            }
        }

        protected void Notify(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
