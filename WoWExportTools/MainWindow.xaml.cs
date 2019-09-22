using CASCLib;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using WoWFormatLib.FileReaders;
using WoWFormatLib.Utils;
using WoWExportTools.Objects;
using WoWExportTools.Sound;

namespace WoWExportTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly BackgroundWorker worker = new BackgroundWorker();
        private readonly BackgroundWorker exportworker = new BackgroundWorker();
        private readonly BackgroundWorker adtexportworker = new BackgroundWorker();
        private readonly BackgroundWorkerEx cascworker = new BackgroundWorkerEx();
        private readonly BackgroundWorkerEx fileworker = new BackgroundWorkerEx();
        private readonly BackgroundWorkerEx tileworker = new BackgroundWorkerEx();

        private bool showM2 = true;
        private bool showWMO = true;

        private bool mapsLoaded = false;
        private bool texturesLoaded = false;

        private List<string> models;
        private List<string> textures;
        private List<string> sounds;

        private Dictionary<int, MapListItem> mapNames = new Dictionary<int, MapListItem>();
        private Dictionary<uint, WDTReader> wdtCache = new Dictionary<uint, WoWFormatLib.FileReaders.WDTReader>();

        private List<string> mapFilters = new List<string>();

        private static ListBox tileBox;
        private static bool ignoreTextureAlpha;

        private PreviewControl previewControl;
        private Splash splash;

        public static bool shuttingDown = false;

        private ModelControl modelControlWindow;

        private SoundPlayer soundPlayer;

        private System.Windows.Forms.OpenFileDialog dialogM2Open;
        private System.Windows.Forms.OpenFileDialog dialogBLPOpen;

        public MainWindow(Splash splash)
        {
            try
            {
                InitializeComponent();
            }
            catch (InvalidOperationException)
            {
                // For some reason, this throws when the MainWindow instance is created after
                // the configuration window is closed on a first-run.
            }

            this.splash = splash;
            Closed += MainWindow_Closed;

            tileBox = tileListBox;

            Title = "WoW Export Tools " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            if (shuttingDown)
                return;

            soundPlayer = new SoundPlayer();
            soundPlayer.PlaybackStopped += SoundPlayer_PlaybackStopped;

            previewControl = new PreviewControl(renderCanvas);
            previewControl.IsPreviewEnabled = (bool)previewCheckbox.IsChecked;
            modelControlWindow = new ModelControl(previewControl);

            CompositionTarget.Rendering += previewControl.CompositionTarget_Rendering;
            wfHost.Initialized += previewControl.WindowsFormsHost_Initialized;

            adtexportworker.DoWork += Adtexporterworker_DoWork; ;
            adtexportworker.RunWorkerCompleted += Exportworker_RunWorkerCompleted;
            adtexportworker.ProgressChanged += Worker_ProgressChanged;
            adtexportworker.WorkerReportsProgress = true;

            exportworker.DoWork += Exportworker_DoWork;
            exportworker.RunWorkerCompleted += Exportworker_RunWorkerCompleted;
            exportworker.ProgressChanged += Worker_ProgressChanged;
            exportworker.WorkerReportsProgress = true;

            cascworker.DoWork += CASCworker_DoWork;
            cascworker.RunWorkerCompleted += CASCworker_RunWorkerCompleted;
            cascworker.ProgressChanged += CASC_ProgressChanged;
            cascworker.WorkerReportsProgress = true;

            fileworker.DoWork += Fileworker_DoWork;
            fileworker.RunWorkerCompleted += Fileworker_RunWorkerCompleted;
            fileworker.ProgressChanged += Fileworker_ProgressChanged;
            fileworker.WorkerReportsProgress = true;

            exportButton.Content = "Export model to OBJ!";

            exportWMO.IsChecked = ConfigurationManager.AppSettings["exportWMO"] == "True";
            exportM2.IsChecked = ConfigurationManager.AppSettings["exportM2"] == "True";
            exportFoliage.IsChecked = ConfigurationManager.AppSettings["exportFoliage"] == "True";
            exportCollision.IsChecked = ConfigurationManager.AppSettings["exportCollision"] == "True";
            exportWMODoodads.IsChecked = ConfigurationManager.AppSettings["exportWMODoodads"] == "True";

            // Set-up conversion dialogs.
            dialogM2Open = new System.Windows.Forms.OpenFileDialog()
            {
                FileName = "Select an M2 file",
                Filter = "M2 Files (*.m2)|*.m2",
                Title = "Open M2 File"
            };

            dialogBLPOpen = new System.Windows.Forms.OpenFileDialog()
            {
                FileName = "Select a BLP file",
                Filter = "BLP Files (*.blp)|*.blp",
                Title = "Open BLP File"
            };
        }

        private void Adtexporterworker_DoWork(object sender, DoWorkEventArgs e)
        {
            var selectedFiles = (System.Collections.IList)e.Argument;

            ConfigurationManager.RefreshSection("appSettings");

            foreach (Structs.MapTile selectedFile in selectedFiles)
            {
                Logger.WriteLine("ExportWorker: Exporting {0}..", selectedFile);
                try
                {
                    Exporters.OBJ.ADTExporter.ExportADT(selectedFile.wdtFileDataID, selectedFile.tileX, selectedFile.tileY, adtexportworker);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("ExportWorker: Exception occured in " + ex.Source + " " + ex.Message + " " + ex.StackTrace);
                }
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            splash.Close();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            cascworker.RunWorkerAsync();
        }

        /* Generic UI */
        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            previewControl.LoadModel((string)modelListBox.SelectedItem);
        }

        private void UpdateFilter()
        {
            if (!MainMenu.IsEnabled)
                return;

            var filtered = new List<string>();
            if (TexturesTab.IsSelected)
            {
                for (var i = 0; i < textures.Count(); i++)
                    if (textures[i].IndexOf(filterTextBox.Text, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                        filtered.Add(textures[i]);

                textureListBox.DataContext = filtered;
            }
            else if (MapsTab.IsSelected)
            {
                UpdateMapListView();
            }
            else if (ModelsTab.IsSelected)
            {
                if (filterTextBox.Text.StartsWith("maptile:"))
                {
                    var filterSplit = filterTextBox.Text.Remove(0, 8).Split('_');
                    if (filterSplit.Length == 3)
                    {
                        exportButton.Content = "Crawl maptile for models";

                        if (Listfile.TryGetFileDataID("world/maps/" + filterSplit[0] + "/" + filterSplit[0] + "_" + filterSplit[1] + "_" + filterSplit[2] + ".adt", out var fileDataID))
                        {
                            if (CASC.FileExists(fileDataID))
                                exportButton.IsEnabled = true;
                            else
                                exportButton.IsEnabled = false;
                        }
                        else
                        {
                            exportButton.IsEnabled = false;
                        }
                    }
                }
                else
                {
                    exportButton.Content = "Export model to OBJ!";
                }

                for (var i = 0; i < models.Count(); i++)
                    if (models[i].IndexOf(filterTextBox.Text, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                        filtered.Add(models[i]);

                modelListBox.DataContext = filtered;
            }
            else if (SoundTab.IsSelected)
            {
                for (var i = 0; i < sounds.Count(); i++)
                    if (sounds[i].IndexOf(filterTextBox.Text, 0, StringComparison.CurrentCultureIgnoreCase) != -1)
                        filtered.Add(sounds[i]);

                soundListBox.DataContext = filtered;
            }
        }

        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateFilter();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if ((string)exportButton.Content == "Crawl maptile for models")
            {
                var filterSplit = filterTextBox.Text.Remove(0, 8).Split('_');
                var filename = "world\\maps\\" + filterSplit[0] + "\\" + filterSplit[0] + "_" + filterSplit[1] + "_" + filterSplit[2] + ".adt";

                fileworker.RunWorkerAsync(filename);
            }
            else
            {
                progressBar.Value = 0;
                progressBar.Visibility = Visibility.Visible;
                loadingLabel.Content = "";
                loadingLabel.Visibility = Visibility.Visible;
                wmoCheckBox.IsEnabled = false;
                m2CheckBox.IsEnabled = false;
                exportButton.IsEnabled = false;
                modelListBox.IsEnabled = false;
                exportCollision.IsEnabled = false;
                exportWMODoodads.IsEnabled = false;

                exportworker.RunWorkerAsync(modelListBox.SelectedItems);
            }
        }

        /* Workers */
        private void CASCworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            worker.DoWork += Worker_DoWork;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.ProgressChanged += CASC_ProgressChanged;
            worker.WorkerReportsProgress = true;

            models = new List<string>();
            textures = new List<string>();
            sounds = new List<string>();

            progressBar.Visibility = Visibility.Visible;

            worker.RunWorkerAsync();

            MainMenu.IsEnabled = true;
        }

        private void CASCworker_DoWork(object sender, DoWorkEventArgs e)
        {
            var basedir = ConfigurationManager.AppSettings["basedir"];
            if (Directory.Exists(basedir))
            {
                cascworker.ReportProgress(0, "Loading WoW from disk..");
                try
                {
                    CASC.InitCasc(cascworker, basedir, ConfigurationManager.AppSettings["program"]);
                }
                catch (Exception exception)
                {
                    Logger.WriteLine("CASCWorker: Exception from {0} during CASC startup: {1}", exception.Source, exception.Message);
                    var result = MessageBox.Show("A fatal error occured during loading your local WoW installation.\n\n" + exception.Message + "\n\nPlease try updating/repairing WoW through the Battle.net App. \n\nIf that doesn't work do the following: \n- Go to your WoW install directory\n- Go inside the data folder\n- Rename the 'indices' folder to 'indices_old'\n- Start WoW to regenerate indices\n- After WoW has started, quit WoW\n\nStill having issues?\nGo to marlam.in/obj and contact me for further help.", "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
                    if (result == MessageBoxResult.OK)
                    {
                        Environment.Exit(1);
                    }
                }
            }
            else
            {
                cascworker.ReportProgress(0, "Loading WoW from web..");
                try
                {
                    CASC.InitCasc(cascworker, null, ConfigurationManager.AppSettings["program"]);
                }
                catch (Exception exception)
                {
                    Logger.WriteLine("CASCWorker: Exception from {0} during CASC startup: {1}", exception.Source, exception.Message);
                    var result = MessageBox.Show("A fatal error occured during loading the online WoW installation.\n\n" + exception.Message + "\n\nGo to marlam.in/obj and contact me for further help.", "Fatal error", MessageBoxButton.OK, MessageBoxImage.Error);
                    if (result == MessageBoxResult.OK)
                    {
                        Environment.Exit(1);
                    }
                }
            }
        }

        private void Fileworker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            modelListBox.DataContext = (List<string>)e.UserState;
        }
        private void Fileworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            exportButton.Content = "Export model to OBJ!";
        }
        private void Fileworker_DoWork(object sender, DoWorkEventArgs e)
        {
            var results = new List<string>();
            var remaining = new List<string>();
            var progress = 0;

            remaining.Add((string)e.Argument);

            while (remaining.Count > 0)
            {
                var filename = remaining[0];
                if (filename.EndsWith(".wmo"))
                {
                    var wmo = new WoWFormatLib.FileReaders.WMOReader();
                    wmo.LoadWMO(filename);
                    // Loop through filenames from WMO
                }
                else if (filename.EndsWith(".adt"))
                {
                    var adt = new WoWFormatLib.FileReaders.ADTReader();
                    adt.LoadADT(filename);

                    foreach (var entry in adt.adtfile.objects.wmoNames.filenames)
                    {
                        results.Add(entry.ToLower());
                    }

                    foreach (var entry in adt.adtfile.objects.m2Names.filenames)
                    {
                        results.Add(entry.ToLower());
                    }
                }

                remaining.Remove(filename);
            }

            fileworker.ReportProgress(progress, results);
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            loadingImage.Visibility = Visibility.Hidden;
            tabs.Visibility = Visibility.Visible;
            modelListBox.Visibility = Visibility.Visible;
            filterTextBox.Visibility = Visibility.Visible;
            filterTextLabel.Visibility = Visibility.Visible;
            exportButton.Visibility = Visibility.Visible;
            wmoCheckBox.Visibility = Visibility.Visible;
            m2CheckBox.Visibility = Visibility.Visible;
            exportCollision.Visibility = Visibility.Visible;
            exportWMODoodads.Visibility = Visibility.Visible;

            splash.Visibility = Visibility.Hidden;
            Visibility = Visibility.Collapsed;
            Visibility = Visibility.Visible;

            progressBar.Value = 100;
            loadingLabel.Content = "Done";

            MenuListfile.IsEnabled = true;

            modelListBox.DataContext = models;
            textureListBox.DataContext = textures;
            soundListBox.DataContext = sounds;

            Logger.WriteLine("Worker: Startup complete!");

            ConfigurationManager.RefreshSection("appSettings");

            if (ConfigurationManager.AppSettings["program"] == "wow_classic" || ConfigurationManager.AppSettings["program"] == "wow_classic_beta")
            {
                previewControl.LoadModel("world/arttest/boxtest/xyz.m2");
            }
            else
            {
                previewControl.LoadModel("spells/axistestobject.m2");
            }

            previewControl.SetCamera(3.200006f, 0f, 0.6000016f, 0.9000001f);

            UpdateFilter();
        }
        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var state = (string)e.UserState;

            if (!string.IsNullOrEmpty(state))
                loadingLabel.Content = state;

            progressBar.Value = e.ProgressPercentage;
        }

        private void CASC_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            var state = (string)e.UserState;
            string text = null;

            if (!string.IsNullOrEmpty(state))
            {
                loadingLabel.Content = state;
                text = state;
            }

            progressBar.Value = e.ProgressPercentage;
            splash.SetLoadingStatus(text, e.ProgressPercentage);
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            worker.ReportProgress(0, "Loading listfile..");

            if (!File.Exists("listfile.csv"))
            {
                worker.ReportProgress(20, "Downloading listfile..");
                Listfile.Update();
            }
            else if (DateTime.Now.AddDays(-7) > File.GetLastWriteTime("listfile.csv"))
            {
                worker.ReportProgress(20, "Updating listfile..");
                Listfile.Update();
            }

            worker.ReportProgress(50, "Loading geoset mapping from disk..");
            Application.Current.Dispatcher.Invoke(delegate {
                modelControlWindow.LoadGeosetMapping();
            });

            worker.ReportProgress(55, "Loading listfile from disk..");

            if (Listfile.FDIDToFilename.Count == 0)
                Listfile.Load();

            worker.ReportProgress(60, "Filtering listfile..");

            var linelist = new List<string>();

            foreach (var line in Listfile.FDIDToFilename)
                if (CASC.FileExists(line.Key))
                    linelist.Add(line.Value);

            var regex = new System.Text.RegularExpressions.Regex(@"(_\d\d\d_)|(_\d\d\d.wmo$)|(lod\d.wmo$)");

            foreach (var line in linelist)
            {
                if (showWMO && line.EndsWith("wmo"))
                    if (!regex.Match(line).Success)
                        models.Add(line);

                if (showM2 && line.EndsWith("m2"))
                    models.Add(line);

                if (line.EndsWith("blp"))
                    textures.Add(line);

                if (line.EndsWith(".ogg") || line.EndsWith(".mp3"))
                    sounds.Add(line);
            }

            worker.ReportProgress(70, "Adding unknown files to file list..");

            try
            {
                var dbcd = new DBCD.DBCD(new DBC.CASCDBCProvider(), new DBCD.Providers.GithubDBDProvider());

                // M2s
                if (showM2)
                {
                    var storage = dbcd.Load("ModelFileData");

                    if (!storage.AvailableColumns.Contains("FileDataID"))
                        throw new Exception("Unable to find FileDataID column in ModelFileData! Likely using a version without up to date definition.");

                    foreach (dynamic entry in storage.Values)
                    {
                        uint fileDataID = (uint)entry.FileDataID;
                        if (!Listfile.FDIDToFilename.ContainsKey(fileDataID))
                        {
                            models.Add("unknown_" + fileDataID + ".m2");
                        }
                    }
                }

                // BLPs
                var tfdStorage = dbcd.Load("TextureFileData");

                if (!tfdStorage.AvailableColumns.Contains("FileDataID"))
                {
                    throw new Exception("Unable to find FileDataID column in ModelFileData! Likely using a version without up to date definition.");
                }

                foreach (dynamic entry in tfdStorage.Values)
                {
                    uint fileDataID = (uint)entry.FileDataID;
                    if (!Listfile.FDIDToFilename.ContainsKey(fileDataID))
                    {
                        textures.Add("unknown_" + fileDataID + ".blp");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine("ListfileWorker: Critical error " + ex.Message + " when trying to add unknown files from DBC to file list!");
            }

            worker.ReportProgress(80, "Sorting listfile..");

            models.Sort();
            textures.Sort();
            sounds.Sort();
        }

        private void Exportworker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            exportButton.IsEnabled = true;
            progressBar.Value = 100;
            loadingLabel.Content = "Done";
            wmoCheckBox.IsEnabled = true;
            m2CheckBox.IsEnabled = true;
            modelListBox.IsEnabled = true;
            exportCollision.IsEnabled = true;
            exportSoundButton.IsEnabled = true;
            exportWMODoodads.IsEnabled = true;

            /* ADT specific UI */
            exportTileButton.IsEnabled = true;
            mapListBox.IsEnabled = true;
            tileListBox.IsEnabled = true;
        }

        private void Exportworker_DoWork(object sender, DoWorkEventArgs e)
        {
            var selectedFiles = (System.Collections.IList)e.Argument;

            ConfigurationManager.RefreshSection("appSettings");

            foreach (string selectedFile in selectedFiles)
            {
                var fdidExport = false;
                uint fileDataID = 0;

                if (selectedFile.StartsWith("unknown_"))
                {
                    fileDataID = uint.Parse(selectedFile.Replace("unknown_", string.Empty).Replace(".m2", string.Empty).Replace(".blp", string.Empty));
                    fdidExport = true;
                }
                else
                {
                    if (!Listfile.TryGetFileDataID(selectedFile, out fileDataID))
                    {
                        Logger.WriteLine("ExportWorker: File {0} does not exist in listfile, skipping export!", selectedFile);
                        continue;
                    }
                }

                if (!CASC.FileExists(fileDataID))
                {
                    Logger.WriteLine("ExportWorker: File {0} does not exist, skipping export!", selectedFile);
                    continue;
                }

                Logger.WriteLine("ExportWorker: Exporting {0}..", selectedFile);
                try
                {
                    ConfigurationManager.RefreshSection("appSettings");
                    var outdir = ConfigurationManager.AppSettings["outdir"];

                    if (selectedFile.EndsWith(".wmo"))
                    {
                        short doodadGroups = -1;
                        if (ConfigurationManager.AppSettings["exportWMODoodads"] == "True")
                        {
                            // ToDo: Apply additional filtering of doodad groups here.
                            doodadGroups = short.MaxValue;
                        }

                        Exporters.OBJ.WMOExporter.ExportWMO(selectedFile, exportworker, null, doodadGroups);
                    }
                    else if (selectedFile.EndsWith(".m2"))
                    {
                        bool[] enabledGeosets = null;
                        Container3D activeObject = previewControl.activeObject;
                        
                        if (activeObject is M2Container m2Object)
                            if (m2Object.FileName == selectedFile)
                                enabledGeosets = m2Object.EnabledGeosets;

                        if (fdidExport)
                            Exporters.OBJ.M2Exporter.ExportM2(fileDataID, exportworker, null, null, enabledGeosets);
                        else
                            Exporters.OBJ.M2Exporter.ExportM2(selectedFile, exportworker, null, enabledGeosets);
                    }
                    else if (selectedFile.EndsWith(".blp"))
                    {
                        try
                        {
                            var blp = new BLPReader();
                            blp.LoadBLP(fileDataID);

                            var bmp = blp.bmp;
                            if (ignoreTextureAlpha)
                                bmp = bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.PixelFormat.Format32bppRgb);

                            if (!fdidExport)
                            {
                                Directory.CreateDirectory(Path.Combine(outdir, Path.GetDirectoryName(selectedFile)));
                                bmp.Save(Path.Combine(outdir, Path.GetDirectoryName(selectedFile), Path.GetFileNameWithoutExtension(selectedFile)) + ".png");
                            }
                            else
                            {
                                bmp.Save(Path.Combine(outdir, fileDataID + ".png"));
                            }
                        }
                        catch (Exception blpException)
                        {
                            Console.WriteLine(blpException.Message);
                        }
                    }
                    else
                    {
                        // Default to just exporting raw files for everything else.
                        string outPath = Path.Combine(outdir, selectedFile);
                        if (!File.Exists(outPath))
                        {
                            string outDir = Path.Combine(outdir, Path.GetDirectoryName(selectedFile));
                            Directory.CreateDirectory(outDir);

                            using (Stream rs = CASC.OpenFile(fileDataID))
                            using (FileStream fs = File.Create(outPath))
                            {
                                rs.Seek(0, SeekOrigin.Begin);
                                rs.CopyTo(fs);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.WriteLine("ExportWorker: Exception occured in " + ex.Source + " " + ex.Message + " " + ex.StackTrace);
                }
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFilter();
        }

        private void ModelListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Just use the first selected item in the case of multi-selections.
            // It makes sense to preview something, rather than nothing.
            if (modelListBox.SelectedItem != null)
            {
                modelControlButton.IsEnabled = true;
                string selectedFile = (string) modelListBox.SelectedItem;

                // Even if we're not rendering the model, we want to load it so that
                // we have geoset data and the likes available.
                previewControl.LoadModel(selectedFile);

                modelControlWindow.UpdateModelControl();
            }

            e.Handled = true;
        }
        private void ModelCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (exportButton == null) { return; }
            if (m2CheckBox == null) { return; }

            showM2 = (bool)m2CheckBox.IsChecked;
            showWMO = (bool)wmoCheckBox.IsChecked;

            progressBar.Visibility = Visibility.Visible;
            loadingLabel.Visibility = Visibility.Visible;
            exportButton.Visibility = Visibility.Hidden;
            modelListBox.Visibility = Visibility.Hidden;
            filterTextBox.Visibility = Visibility.Hidden;
            filterTextLabel.Visibility = Visibility.Hidden;
            wmoCheckBox.Visibility = Visibility.Hidden;
            m2CheckBox.Visibility = Visibility.Hidden;
            exportCollision.Visibility = Visibility.Hidden;
            exportWMODoodads.Visibility = Visibility.Hidden;

            models = new List<string>();
            textures = new List<string>();
            sounds = new List<string>();
            worker.RunWorkerAsync();
        }

        private void TexturesTab_GotFocus(object sender, RoutedEventArgs e)
        {
            if (!texturesLoaded)
            {
                textureListBox.DataContext = textures;
                texturesLoaded = true;
                UpdateFilter();
            }
        }

        private void TextureListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var file = (string)textureListBox.SelectedItem;
            if (file == null)
                return;

            uint fileDataID = 0;

            if (file.StartsWith("unknown_"))
            {
                fileDataID = uint.Parse(file.Replace("unknown_", string.Empty).Replace(".blp", string.Empty));
            }
            else
            {
                if (!Listfile.TryGetFileDataID(file, out fileDataID))
                {
                    Logger.WriteLine("BLP preview: File {0} does not exist in listfile, skipping preview!", file);
                    return;
                }
            }

            if (!CASC.FileExists(fileDataID))
            {
                Logger.WriteLine("BLP preview: File {0} does not exist, skipping preview!", file);
                return;
            }

            try
            {
                using (var memory = new MemoryStream())
                {
                    var blp = new WoWFormatLib.FileReaders.BLPReader();
                    blp.LoadBLP(fileDataID);

                    var bmp = blp.bmp;

                    if (ignoreTextureAlpha)
                    {
                        bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.PixelFormat.Format32bppRgb).Save(memory, ImageFormat.Bmp);
                    }
                    else
                    {
                        bmp.Save(memory, ImageFormat.Png);
                    }

                    memory.Position = 0;

                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();
                    blpImage.Source = bitmapImage;
                    blpImage.MaxWidth = bitmapImage.Width;
                    blpImage.MaxHeight = bitmapImage.Height;
                }
            }
            catch (Exception blpException)
            {
                Console.WriteLine(blpException.Message);
            }

            e.Handled = true;
        }

        private void IgnoreAlpha_Checked(object sender, RoutedEventArgs e)
        {
            if ((bool)ignoreAlpha.IsChecked)
            {
                ignoreTextureAlpha = true;
            }
            else
            {
                ignoreTextureAlpha = false;
            }

            var selected = textureListBox.SelectedIndex;
            textureListBox.SelectedIndex = -1;
            textureListBox.SelectedIndex = selected;
        }

        private void ExportTextureButton_Click(object sender, RoutedEventArgs e)
        {
            progressBar.Value = 0;
            progressBar.Visibility = Visibility.Visible;
            loadingLabel.Content = "";
            loadingLabel.Visibility = Visibility.Visible;
            wmoCheckBox.IsEnabled = false;
            m2CheckBox.IsEnabled = false;
            exportButton.IsEnabled = false;
            modelListBox.IsEnabled = false;

            exportworker.RunWorkerAsync(textureListBox.SelectedItems);
        }

        private void ExportTileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedMap = (MapListItem)mapListBox.SelectedItem;
            var selectedTile = (string)tileListBox.SelectedItem;

            if (selectedMap == null || selectedTile == null)
            {
                Console.WriteLine("Nothing selected, not exporting.");
                return;
            }

            Console.WriteLine(selectedMap.Name + ", " + selectedMap.Internal + ", " + selectedTile);

            progressBar.Value = 0;
            progressBar.Visibility = Visibility.Visible;
            loadingLabel.Content = "";
            loadingLabel.Visibility = Visibility.Visible;
            wmoCheckBox.IsEnabled = false;
            m2CheckBox.IsEnabled = false;
            exportButton.IsEnabled = false;
            modelListBox.IsEnabled = false;

            /* ADT specific UI */
            exportTileButton.IsEnabled = false;
            mapListBox.IsEnabled = false;
            tileListBox.IsEnabled = false;

            var tileList = new List<Structs.MapTile>();

            progressBar.Value = 10;
            loadingLabel.Content = "Baking map textures, this will take a while.";

            Dispatcher.Invoke(new Action(() => { }), System.Windows.Threading.DispatcherPriority.ContextIdle, null);

            foreach (string item in tileListBox.SelectedItems)
            {
                var mapName = selectedMap.Internal.ToLower();
                var mapTile = new Structs.MapTile();
                var coord = item.Split('_');
                mapTile.tileX = byte.Parse(coord[0]);
                mapTile.tileY = byte.Parse(coord[1]);

                var wdtFileName = "world/maps/" + mapName + "/" + mapName + ".wdt";
                if (!Listfile.TryGetFileDataID(wdtFileName, out mapTile.wdtFileDataID))
                {
                    Logger.WriteLine("Unable to find WDT fileDataID for map " + selectedMap.Internal.ToLower());
                }

                tileList.Add(mapTile);

                ConfigurationManager.RefreshSection("appSettings");
                var outdir = ConfigurationManager.AppSettings["outdir"];

                if (((ComboBoxItem)bakeSize.SelectedItem).Name != "none")
                {
                    previewControl.BakeTexture(mapTile, Path.Combine(outdir, Path.GetDirectoryName(wdtFileName), mapName.Replace(" ", "") + "_" + mapTile.tileX + "_" + mapTile.tileY + ".png"));
                }
            }

            adtexportworker.RunWorkerAsync(tileList);
        }

        private void MapListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            tileListBox.Items.Clear();

            if (mapListBox.HasItems)
            {
                var selectedItem = (MapListItem)mapListBox.SelectedItem;

                if (CASC.FileExists((uint)selectedItem.WDTFileDataID))
                {
                    if (!wdtCache.ContainsKey(selectedItem.ID))
                    {
                        var reader = new WoWFormatLib.FileReaders.WDTReader();
                        reader.LoadWDT((uint)selectedItem.WDTFileDataID);
                        wdtCache.Add(selectedItem.ID, reader);
                    }

                    for (var i = 0; i < wdtCache[selectedItem.ID].tiles.Count; i++)
                    {
                        tileListBox.Items.Add(wdtCache[selectedItem.ID].tiles[i].Item1.ToString() + "_" + wdtCache[selectedItem.ID].tiles[i].Item2.ToString());
                    }
                }
            }

            e.Handled = true;
        }

        private void TileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (tileListBox.SelectedItem == null)
            {
                e.Handled = true;
                return;
            }

            try
            {
                var selectedMap = (MapListItem)mapListBox.SelectedItem;

                if (!wdtCache.ContainsKey(selectedMap.ID))
                {
                    var reader = new WoWFormatLib.FileReaders.WDTReader();
                    reader.LoadWDT((uint)selectedMap.WDTFileDataID);
                    wdtCache.Add(selectedMap.ID, reader);
                }

                var file = (string)tileListBox.SelectedItem;
                var splitTile = file.Split('_');
                var fixedTileName = splitTile[0].PadLeft(2, '0') + "_" + splitTile[1].PadLeft(2, '0');
                var minimapFile = "world\\minimaps\\" + selectedMap.Internal + "\\map" + fixedTileName + ".blp";

                if (!Listfile.TryGetFileDataID(minimapFile, out var minimapFileDataID))
                {
                    Logger.WriteLine("Unable to find filedataid for minimap file " + minimapFile);
                    minimapFileDataID = wdtCache[selectedMap.ID].tileFiles[(byte.Parse(splitTile[0]), byte.Parse(splitTile[1]))].minimapTexture;
                }

                if (!CASC.FileExists(minimapFileDataID))
                {
                    // interface\icons\inv_misc_questionmark.blp
                    minimapFileDataID = 134400;
                }

                var blp = new WoWFormatLib.FileReaders.BLPReader();
                blp.LoadBLP(minimapFileDataID);

                var bmp = blp.bmp;

                using (var memory = new MemoryStream())
                {
                    bmp.Save(memory, ImageFormat.Png);

                    memory.Position = 0;

                    var bitmapImage = new BitmapImage();

                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = memory;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    tileImage.Source = bitmapImage;
                }

                selectedTileLabel.Content = "Selected tile: " + file.Insert(2, "_");

            }
            catch (Exception blpException)
            {
                Console.WriteLine(blpException.Message);
            }

            e.Handled = true;
        }

        private void MapCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var source = (CheckBox)sender;
            if (source.Name.StartsWith("wow"))
            {
                // Expansion filter
                //Console.WriteLine("Checkbox event on " + source.Name);

                if ((bool)source.IsChecked)
                {
                    if (!mapFilters.Contains(source.Name))
                    {
                        mapFilters.Add(source.Name);
                    }
                }
                else
                {
                    mapFilters.Remove(source.Name);
                }
            }
            else
            {
                // Category filter
                //Console.WriteLine("Checkbox event on " + source.Content);

                if ((bool)source.IsChecked)
                {
                    if (!mapFilters.Contains((string)source.Content))
                    {
                        mapFilters.Add((string)source.Content);
                    }
                }
                else
                {
                    mapFilters.Remove((string)source.Content);
                }
            }

            if (mapsLoaded)
            {
                UpdateMapListView();
            }
        }

        private void MapViewerButton_Click(object sender, RoutedEventArgs e)
        {
            var tileList = new List<string>();

            var selectedItem = (MapListItem)mapListBox.SelectedItem;
            if (selectedItem == null) return;

            foreach (var selectedTile in tileListBox.SelectedItems)
            {
                var tiles = selectedTile.ToString().Split('_');
                var x = int.Parse(tiles[0]);
                var y = int.Parse(tiles[1]);

                var adtFile = "world\\maps\\" + selectedItem.Internal + "\\" + selectedItem.Internal + "_" + x + "_" + y + ".adt";
                tileList.Add(adtFile);
            }

            //previewControl.LoadModel(tileList);
        }

        private void TileViewerButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = (MapListItem)mapListBox.SelectedItem;
            if (selectedItem == null) return;

            //var mw = new MapWindow(selectedItem.Internal);
            //mw.Show();
        }

        private void BakeSize_Loaded(object sender, RoutedEventArgs e)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            foreach (ComboBoxItem cbi in bakeSize.Items)
            {
                if (cbi.Name == config.AppSettings.Settings["bakeQuality"].Value)
                {
                    bakeSize.SelectedItem = cbi;
                    break;
                }
            }
        }

        public static void SelectTile(string tile)
        {
            tileBox.SelectedValue = tile;
        }

        public class MapListItem
        {
            public uint ID { get; set; }
            public string Type { get; set; }
            public string Name { get; set; }
            public string Internal { get; set; }
            public string Image { get; set; }
            public uint ExpansionID { get; set; }
            public int WDTFileDataID { get; set; }
        }

        private void MenuMapNames_Click(object sender, RoutedEventArgs e)
        {
            UpdateMapList();
        }

        private void MenuPreferences_Click(object sender, RoutedEventArgs e)
        {
            var cfgWindow = new ConfigurationWindow(true);
            cfgWindow.ShowDialog();
            exportButton.Content = "Export model to OBJ!";
        }

        private void MenuListfile_Click(object sender, RoutedEventArgs e)
        {
            MenuListfile.IsEnabled = false;

            progressBar.Visibility = Visibility.Visible;
            loadingLabel.Visibility = Visibility.Visible;
            exportButton.Visibility = Visibility.Hidden;
            modelListBox.Visibility = Visibility.Hidden;
            filterTextBox.Visibility = Visibility.Hidden;
            filterTextLabel.Visibility = Visibility.Hidden;
            wmoCheckBox.Visibility = Visibility.Hidden;
            m2CheckBox.Visibility = Visibility.Hidden;
            tabs.Visibility = Visibility.Hidden;

            Listfile.Update();

            models.Clear();
            textures.Clear();
            sounds.Clear();

            worker.RunWorkerAsync();
        }

        private void MenuVersion_Click(object sender, RoutedEventArgs e)
        {
            var vwindow = new VersionWindow();
            vwindow.Show();
        }
        private void MenuQuit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void UpdateMapList()
        {
            try
            {
                using (var client = new WebClient())
                using (var stream = new MemoryStream())
                {
                    var responseStream = client.OpenRead("https://docs.google.com/spreadsheets/d/1yYSHjWTX0l751QscolQpFNWjwdKLbD_rzviZ_XqTPfk/export?exportFormat=csv&gid=0");
                    responseStream.CopyTo(stream);
                    File.WriteAllBytes("mapnames.csv", stream.ToArray());
                    responseStream.Close();
                    responseStream.Dispose();
                }
            }
            catch (Exception e)
            {
                Logger.WriteLine("Unable to download map names csv: " + e.Message);
            }
        }
        private void UpdateMapListView()
        {
            if (!File.Exists("mapnames.csv"))
            {
                UpdateMapList();
            }

            if (mapNames.Count == 0)
            {
                if (File.Exists("mapnames.csv"))
                {
                    using (var parser = new TextFieldParser("mapnames.csv"))
                    {
                        parser.TextFieldType = FieldType.Delimited;
                        parser.SetDelimiters(",");
                        while (!parser.EndOfData)
                        {
                            var fields = parser.ReadFields();
                            if (fields[0] != "ID")
                            {
                                var expansionID = ExpansionNameToID(fields[5]);
                                var image = "pack://application:,,,/Resources/wow" + expansionID + ".png";

                                if (!Listfile.TryGetFileDataID("world/maps/" + fields[2] + "/" + fields[2] + ".wdt", out uint wdtFileDataID))
                                {
                                    continue;
                                }

                                mapNames.Add(int.Parse(fields[0]), new MapListItem { ID = uint.Parse(fields[0]), Name = fields[4], Internal = fields[2], Type = fields[3], Image = image, ExpansionID = (uint)expansionID, WDTFileDataID = (int)wdtFileDataID });
                            }
                        }
                    }
                }

                try
                {
                    var dbcd = new DBCD.DBCD(new DBC.CASCDBCProvider(), new DBCD.Providers.GithubDBDProvider());
                    var storage = dbcd.Load("Map");

                    foreach (dynamic entry in storage.Values)
                    {
                        int wdtFileDataID = 0;

                        if (storage.AvailableColumns.Contains("WdtFileDataID"))
                        {
                            wdtFileDataID = entry.WdtFileDataID;
                        }
                        else
                        {
                            wdtFileDataID = (int)CASC.getFileDataIdByName("world/maps/" + entry.Directory + "/" + entry.Directory + ".wdt");
                        }

                        if (CASC.FileExists((uint)wdtFileDataID))
                        {
                            int mapID = entry.ID;
                            string mapDirectory = entry.Directory;
                            string mapName = entry.MapName_lang;
                            if (mapNames.ContainsKey(mapID))
                            {
                                mapName = mapNames[mapID].Name;
                            }

                            uint mapType = entry.MapType;

                            var mapTypeDesc = "Unknown (" + mapType + ")";

                            switch (mapType)
                            {
                                case 0:
                                    mapTypeDesc = "Normal";
                                    break;
                                case 1:
                                    mapTypeDesc = "Instance";
                                    break;
                                case 2:
                                    mapTypeDesc = "Raid";
                                    break;
                                case 3:
                                    mapTypeDesc = "PvP";
                                    break;
                                case 4:
                                    mapTypeDesc = "Arena";
                                    break;
                                default:
                                    mapTypeDesc = "Unknown (" + mapType + ")";
                                    break;
                            }

                            var mapItem = new MapListItem { ID = (uint)mapID, Name = mapName, Internal = mapDirectory, ExpansionID = (uint)entry.ExpansionID + 1, Image = "pack://application:,,,/Resources/wow" + (entry.ExpansionID + 1) + ".png", Type = mapTypeDesc, WDTFileDataID = wdtFileDataID };

                            if (!mapNames.ContainsKey(mapID))
                            {
                                mapNames.Add(mapID, mapItem);
                            }
                            else
                            {
                                mapNames[mapID] = mapItem;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occured during DBC reading: " + ex.Message);
                }
            }

            mapListBox.DisplayMemberPath = "Value";
            mapListBox.Items.Clear();

            foreach (var map in mapNames)
            {
                var wdt = "World/Maps/" + map.Value.Internal + "/" + map.Value.Internal + ".wdt";

                if (!mapFilters.Contains("wow" + map.Value.ExpansionID) || !mapFilters.Contains(map.Value.Type))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(filterTextBox.Text) || (map.Value.Internal.IndexOf(filterTextBox.Text, 0, StringComparison.CurrentCultureIgnoreCase) != -1 || map.Value.Name.IndexOf(filterTextBox.Text, 0, StringComparison.CurrentCultureIgnoreCase) != -1))
                {
                    mapListBox.Items.Add(map.Value);
                }
            }

            mapsLoaded = true;
        }
        private int ExpansionNameToID(string name)
        {
            switch (name)
            {
                case "Vanilla":
                    return 1;
                case "Burning Crusade":
                    return 2;
                case "Wrath of the Lich King":
                    return 3;
                case "Cataclysm":
                    return 4;
                case "Mists of Pandaria":
                    return 5;
                case "Warlords of Draenor":
                    return 6;
                case "Legion":
                    return 7;
                case "Battle for Azeroth":
                    return 8;
                default:
                    return 1;
            }
        }

        private void PreviewCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            if (previewControl != null)
                previewControl.IsPreviewEnabled = (bool)previewCheckbox.IsChecked;
        }

        private void CollisionCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            SetConfigValue("exportCollision", exportCollision.IsChecked.ToString());
        }

        private void ExportWMODoodadsCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            SetConfigValue("exportWMODoodads", exportWMODoodads.IsChecked.ToString());
        }

        private void ExportWMO_Click(object sender, RoutedEventArgs e)
        {
            SetConfigValue("exportWMO", exportWMO.IsChecked.ToString());
        }
        private void ExportM2_Click(object sender, RoutedEventArgs e)
        {
            SetConfigValue("exportM2", exportM2.IsChecked.ToString());
        }

        private void ExportFoliage_Click(object sender, RoutedEventArgs e)
        {
            SetConfigValue("exportFoliage", exportFoliage.IsChecked.ToString());
        }

        private void BakeSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetConfigValue("bakeQuality", ((ComboBoxItem)bakeSize.SelectedItem).Name);
            e.Handled = true;
        }

        private void SetConfigValue(string key, string value)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = value;
            config.Save(ConfigurationSaveMode.Full);
        }

        private void MenuConvertM2_Click(object sender, RoutedEventArgs e)
        {
            if (dialogM2Open.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var filePath = dialogM2Open.FileName;
                    using (Stream dataStream = dialogM2Open.OpenFile())
                    {
                        var reader = new M2Reader();
                        reader.LoadM2(dataStream);

                        Exporters.OBJ.M2Exporter.ExportM2(reader, filePath, null, Path.GetDirectoryName(filePath), true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception reading local M2: " + ex.Message);
                }
            }
        }

        private void MenuConvertBLP_Click(object sender, RoutedEventArgs e)
        {
            if (dialogBLPOpen.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var filePath = dialogBLPOpen.FileName;
                    using (Stream dataStream = dialogBLPOpen.OpenFile())
                    {
                        BLPReader reader = new BLPReader();
                        reader.LoadBLP(dataStream);

                        Bitmap bmp = reader.bmp;
                        if (ignoreTextureAlpha)
                            bmp = bmp.Clone(new Rectangle(0, 0, bmp.Width, bmp.Height), System.Drawing.Imaging.PixelFormat.Format32bppRgb);

                        bmp.Save(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + ".png"));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception reading local BLP: " + ex.Message);
                }
            }
        }

        private void ListBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
            {
                ListBox box = (ListBox)sender;

                if (box.SelectedItems.Count > 0)
                {
                    List<string> items = new List<string>(box.SelectedItems.Count);
                    foreach (string item in box.SelectedItems)
                        items.Add(item);

                    Clipboard.SetText(string.Join("\n", items));
                }
            }
        }

        private void ShowModelControlButton_Click(object sender, RoutedEventArgs e)
        {
            modelControlWindow.Show();
            modelControlWindow.Focus();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            shuttingDown = true;

            if (modelControlWindow != null)
                modelControlWindow.Close();
        }

        private void PlaySelectedSound(object sender, EventArgs e)
        {
            if (soundPlayer.IsPlaying)
            {
                soundPlayer.Stop();
                exportPlayButton.Content = "Play";
            }
            else
            {
                if (soundListBox.SelectedItem != null)
                {
                    string fileName = (string)soundListBox.SelectedItem;
                    if (Listfile.TryGetFileDataID(fileName, out uint fileID))
                    {
                        if (fileName.EndsWith(".mp3"))
                            soundPlayer.Play(fileID, SoundPlayer.FORMAT_MP3);
                        else if (fileName.EndsWith(".ogg"))
                            soundPlayer.Play(fileID, SoundPlayer.FORMAT_VORBIS);

                        exportPlayButton.Content = "Stop";
                    }
                    else
                    {
                        throw new FileNotFoundException("Unable to locate sound file: " + fileName);
                    }
                }
            }
        }

        private void SoundPlayer_PlaybackStopped(object sender, EventArgs e)
        {
            exportPlayButton.Content = "Play";
        }

        private void ExportSound_Click(object sender, RoutedEventArgs e)
        {
            progressBar.Value = 0;
            progressBar.Visibility = Visibility.Visible;
            loadingLabel.Content = "";
            loadingLabel.Visibility = Visibility.Visible;
            exportSoundButton.IsEnabled = false;

            exportworker.RunWorkerAsync(soundListBox.SelectedItems);
        }
    }
}
