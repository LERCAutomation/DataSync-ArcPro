// The DataTools are a suite of ArcGIS Pro addins used to extract
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2024 Andy Foy Consulting.
//
// This file is part of DataTools suite of programs.
//
// DataTools are free software: you can redistribute it and/or modify
// them under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// DataTools are distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with with program.  If not, see <http://www.gnu.org/licenses/>.

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.Framework.Controls;
using ArcGIS.Desktop.Mapping;
using DataTools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace DataSync.UI
{
    internal class PaneHeader2ViewModel : PanelViewModelBase, INotifyPropertyChanged
    {
        #region Fields

        private readonly DockpaneMainViewModel _dockPane;

        private bool _tablesLoaded = false;
        private bool _syncErrors = false;
        private bool _checkHasRun = false;
        private bool _remoteLayerAdded = false;

        private string _logFilePath;
        private string _logFile;

        // Server fields.
        private string _sdeFileName;
        private string _defaultSchema;
        private string _checkStoredProcedure;
        private string _updateStoredProcedure;
        private string _clearStoredProcedure;

        // Table fields.
        private string _localLayer;
        private string _remoteTable;
        private string _remoteLayer;
        private string _keyColumn;
        private string _spatialColumn;
        private long _localCount;
        private long _remoteCount;

        private List<ResultSummary> _resultSummary;
        private List<ResultDetail> _resultDetail;

        private string _userID;

        private const string _displayName = "DataSync";

        private readonly DataSyncConfig _toolConfig;
        private MapFunctions _mapFunctions;
        private SQLServerFunctions _sqlFunctions;

        #endregion Fields

        #region ViewModelBase Members

        public override string DisplayName
        {
            get { return _displayName; }
        }

        #endregion ViewModelBase Members

        #region Creator

        /// <summary>
        /// Set the global variables.
        /// </summary>
        /// <param name="dockPane"></param>
        /// <param name="toolConfig"></param>
        public PaneHeader2ViewModel(DockpaneMainViewModel dockPane, DataSyncConfig toolConfig)
        {
            _dockPane = dockPane;

            // Return if no config object passed.
            if (toolConfig == null) return;

            // Set the config object.
            _toolConfig = toolConfig;

            InitializeComponent();
        }

        /// <summary>
        /// Initialise the extract pane.
        /// </summary>
        private void InitializeComponent()
        {
            // Set the SDE file name.
            _sdeFileName = _toolConfig.SDEFile;

            // Create a new map functions object.
            _mapFunctions = new();

            // Create a new SQL functions object.
            _sqlFunctions = new(_sdeFileName);

            // Get the relevant config file settings.
            _logFilePath = _toolConfig.LogFilePath;
            _defaultSchema = _toolConfig.DatabaseSchema;
            _checkStoredProcedure = _toolConfig.CheckStoredProcedure;
            _updateStoredProcedure = _toolConfig.UpdateStoredProcedure;
            _clearStoredProcedure = _toolConfig.ClearStoredProcedure;
            _localLayer = _toolConfig.LocalLayer;
            _remoteTable = _toolConfig.RemoteTable;
            _remoteLayer = _toolConfig.RemoteLayer;
            _keyColumn = _toolConfig.KeyColumn;
            _spatialColumn = _toolConfig.SpatialColumn;

            // Clear the check has run flag.
            _checkHasRun = false;
        }

        #endregion Creator

        #region Controls Enabled

        /// <summary>
        /// Can the check button be pressed?
        /// </summary>
        public bool CheckButtonEnabled
        {
            get
            {
                return ((_dockPane.ProcessStatus == null)
                    && (_tablesLoaded));
            }
        }

        /// <summary>
        /// Is the list of result summary enabled?
        /// </summary>
        public bool ResultSummaryListEnabled
        {
            get
            {
                return ((_dockPane.ProcessStatus == null)
                    && (_resultSummaryList != null));
            }
        }

        /// <summary>
        /// Is the list of result details enabled?
        /// </summary>
        public bool ResultDetailListEnabled
        {
            get
            {
                return ((_dockPane.ProcessStatus == null)
                    && (_resultDetailList != null));
            }
        }

        /// <summary>
        /// Can the run button be pressed?
        /// </summary>
        public bool RunButtonEnabled
        {
            get
            {
                return ((_checkHasRun)
                    && (_dockPane.ProcessStatus == null));
            }
        }

        #endregion Controls Enabled

        #region Controls Visibility

        /// <summary>
        /// Are the Result lists visible.
        /// </summary>
        public Visibility ResultVisibility
        {
            get
            {
                if ((_checkHasRun == false))
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
        }

        /// <summary>
        /// Are the Options buttons visible.
        /// </summary>
        public Visibility OptionsVisibility
        {
            get
            {
                if ((_checkHasRun == false))
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
        }

        /// <summary>
        /// Is the ResultDetailList expand button visible.
        /// </summary>
        public Visibility ResultDetailListExpandButtonVisibility
        {
            get
            {
                //TODO: Set number of rows before button is shown.
                if ((_resultDetailList == null) || (_resultDetailList.Count < 9))
                    return Visibility.Hidden;
                else
                    return Visibility.Visible;
            }
        }

        #endregion Controls Visibility

        #region Message

        private string _message;

        /// <summary>
        /// The message to display on the form.
        /// </summary>
        public string Message
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
                OnPropertyChanged(nameof(HasMessage));
                OnPropertyChanged(nameof(Message));
            }
        }

        private MessageType _messageLevel;

        /// <summary>
        /// The type of message; Error, Warning, Confirmation, Information
        /// </summary>
        public MessageType MessageLevel
        {
            get
            {
                return _messageLevel;
            }
            set
            {
                _messageLevel = value;
                OnPropertyChanged(nameof(MessageLevel));
            }
        }

        /// <summary>
        /// Is there a message to display?
        /// </summary>
        public Visibility HasMessage
        {
            get
            {
                if (_dockPane.ProcessStatus != null
                || string.IsNullOrEmpty(_message))
                    return Visibility.Collapsed;
                else
                    return Visibility.Visible;
            }
        }

        /// <summary>
        /// Show the message with the required icon (message type).
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="messageLevel"></param>
        public void ShowMessage(string msg, MessageType messageLevel)
        {
            MessageLevel = messageLevel;
            Message = msg;
        }

        /// <summary>
        /// Clear the form messages.
        /// </summary>
        public void ClearMessage()
        {
            Message = "";
        }

        #endregion Message

        #region Check Command

        private ICommand _checkCommand;

        /// <summary>
        /// Create Check button command.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand CheckCommand
        {
            get
            {
                if (_checkCommand == null)
                {
                    Action<object> checkAction = new(CheckCommandClick);
                    _checkCommand = new RelayCommand(checkAction, param => CheckButtonEnabled);
                }

                return _checkCommand;
            }
        }

        /// <summary>
        /// Handles event when Check button is clicked.
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void CheckCommandClick(object param)
        {
            // Run the check (don't wait).
            CheckChangesAsync();
        }

        #endregion Check Command

        #region Run Command

        /// <summary>
        /// Run the sync.
        /// </summary>
        public async void RunSyncAsync()
        {
            // Clear any messages.
            ClearMessage();

            // Update the fields and buttons in the form.
            UpdateFormControls();
            _dockPane.RefreshPanel1Buttons();

            // Process the sync.
            bool success;
            success = await ApplyChangesAsync();

            // Indicate that the sync process has completed (successfully or not).
            string message;
            string image;

            if (success)
            {
                message = "Process complete!";
                image = "Success";
            }
            else if (_syncErrors)
            {
                message = "Process ended with errors!";
                image = "Error";
            }
            else
            {
                message = "Process ended unexpectedly!";
                image = "Error";
            }

            // Finish up now the sync has stopped (successfully or not).
            StopSync(message, image);

            // Update the fields and buttons in the form.
            UpdateFormControls();
            _dockPane.RefreshPanel1Buttons();
        }

        #endregion Run Command

        #region Properties

        private string _tableSummaryText;

        public string TableSummaryText
        {
            get
            {
                return _tableSummaryText;
            }
            set
            {
                _tableSummaryText = value;
                OnPropertyChanged(nameof(TableSummaryText));
            }
        }

        /// <summary>
        /// The list of result summary items.
        /// </summary>
        private ObservableCollection<ResultSummary> _resultSummaryList;

        /// <summary>
        /// Get/Set the list of results summary.
        /// </summary>
        public ObservableCollection<ResultSummary> ResultSummaryList
        {
            get
            {
                return _resultSummaryList;
            }
            set
            {
                _resultSummaryList = value;
            }
        }

        /// <summary>
        /// Triggered when the selection in the summary result list changes.
        /// </summary>
        public int ResultSummaryListSelectedIndex
        {
            set
            {
                // Skip if no item is selected.
                if (value == -1)
                    return;

                // Get the selected item.
                ResultSummary resultSummary = _resultSummaryList[value];

                // Get the selected result type.
                string type = resultSummary.Type;

                // Get the list of result details for the selected result type.
                List<ResultDetail> resultDetail = _resultDetail.Where(r => r.ResultType == type).ToList();

                // Load the result details for selected type.
                ResultDetailList = new ObservableCollection<ResultDetail>(resultDetail);

                // Clear the selected index for the result details list.
                if (_resultDetailList != null)
                {
                    foreach (ResultDetail detail in _resultDetailList)
                    {
                        detail.IsSelected = false;
                    }
                }
            }
        }

        /// <summary>
        /// The list of SQL tables.
        /// </summary>
        private ObservableCollection<ResultDetail> _resultDetailList;

        /// <summary>
        /// Get the list of SQL tables.
        /// </summary>
        public ObservableCollection<ResultDetail> ResultDetailList
        {
            get
            {
                return _resultDetailList;
            }
            set
            {
                _resultDetailList = value;
                OnPropertyChanged(nameof(ResultDetailList));
                OnPropertyChanged(nameof(ResultDetailListExpandButtonVisibility));
            }
        }

        public int ResultDetailListSelectedIndex
        {
            set
            {
                // Skip if no item is selected.
                if (value == -1)
                    return;

                // Clear any messages.
                ClearMessage();

                // Get the selected item.
                ResultDetail resultDetail = _resultDetailList[value];

                // Get the type of the selected result.
                string resultType = resultDetail.ResultType;

                // If the result type is a deleted feature.
                if (resultType == "Deleted")
                {
                    // Get the key of the selected result.
                    string oldRef = resultDetail.OldRef?.Trim();

                    // Zoom to the selected result.
                    ZoomToResultAsync(_remoteLayer, oldRef);
                }
                else
                {
                    // Get the key of the selected result.
                    string newRef = resultDetail.NewRef?.Trim();

                    // Zoom to the selected result.
                    ZoomToResultAsync(_localLayer, newRef);
                }
            }
        }

        private double? _resultDetailListHeight = null;

        /// <summary>
        /// Get the height of the result details list.
        /// </summary>
        public double? ResultDetailListHeight
        {
            get
            {
                if (_resultDetailList == null || _resultDetailList.Count == 0)
                    return 422;
                else
                    return _resultDetailListHeight;
            }
        }

        /// <summary>
        /// Get the content of the result details list expand button.
        /// </summary>
        public string ResultDetailListExpandButtonContent
        {
            get
            {
                if (_resultDetailListHeight == null)
                    return "-";
                else
                    return "+";
            }
        }

        private bool _clearLogFile;

        /// <summary>
        /// Is the log file to be cleared before running the extract?
        /// </summary>
        public bool ClearLogFile
        {
            get
            {
                return _clearLogFile;
            }
            set
            {
                _clearLogFile = value;
            }
        }

        private bool _openLogFile;

        /// <summary>
        /// Is the log file to be opened after running the extract?
        /// </summary>
        public bool OpenLogFile
        {
            get
            {
                return _openLogFile;
            }
            set
            {
                _openLogFile = value;
            }
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Update the fields and buttons in the form.
        /// </summary>
        private void UpdateFormControls()
        {
            UpdateFormFields();

            // Check if the run button is now enabled/disabled.
            _dockPane.CheckRunButton();
        }

        /// <summary>
        /// Update the fields in the form.
        /// </summary>
        private void UpdateFormFields()
        {
            OnPropertyChanged(nameof(CheckButtonEnabled));
            OnPropertyChanged(nameof(ResultSummaryList));
            OnPropertyChanged(nameof(ResultSummaryListEnabled));
            OnPropertyChanged(nameof(ResultDetailList));
            OnPropertyChanged(nameof(ResultDetailListEnabled));
            OnPropertyChanged(nameof(ResultVisibility));
            OnPropertyChanged(nameof(OptionsVisibility));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(HasMessage));
        }

        /// <summary>
        /// Set all of the form fields to their default values.
        /// </summary>
        /// <returns></returns>
        public async Task ResetFormAsync(bool reset)
        {
            // Clear the result summary selections first (to avoid selections being retained).
            if (_resultSummaryList != null)
            {
                foreach (ResultSummary layer in _resultSummaryList)
                {
                    layer.IsSelected = false;
                }
            }

            // Clear the result detail selections first (to avoid selections being retained).
            if (_resultDetailList != null)
            {
                foreach (ResultDetail layer in _resultDetailList)
                {
                    layer.IsSelected = false;
                }
            }

            // Log file.
            ClearLogFile = _toolConfig.DefaultClearLogFile;
            OpenLogFile = _toolConfig.DefaultOpenLogFile;

            // Reload the details of the local and remote tables.
            await LoadTableDetailsAsync(reset, false);
        }

        /// <summary>
        /// Load the details of the local and remote tables.
        /// </summary>
        /// <param name="reset"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task LoadTableDetailsAsync(bool reset, bool message)
        {
            // If already processing then exit.
            if (_dockPane.ProcessStatus != null)
                return;

            // Reset the check has run flag.
            _checkHasRun = false;

            // Reset the tables loaded flag.
            _tablesLoaded = false;

            // Clear the local and remote row counts.
            TableSummaryText = "";

            // Clear the list of result summary and details.
            ClearFormLists();

            _dockPane.FormLoading = true;
            if (reset)
                _dockPane.ProgressUpdate("Refreshing table details...");
            else
                _dockPane.ProgressUpdate("Loading table details...");

            // Clear any messages.
            ClearMessage();

            // Update the fields and buttons in the form.
            UpdateFormControls();

            // Load the local table details (don't wait)
            Task<string> localDetailsTask = LoadLocalDetailsAsync();

            // Load the local table details (don't wait)
            Task<string> remoteDetailsTask = LoadRemoteDetailsAsync();

            // Wait for all of the lists to load.
            await Task.WhenAll(localDetailsTask, remoteDetailsTask);

            // Hide progress update.
            _dockPane.ProgressUpdate(null, -1, -1);

            // Set flag to show the tables have been loaded.
            _tablesLoaded = true;

            // Indicate the refresh has finished.
            _dockPane.FormLoading = false;

            // Update the fields and buttons in the form.
            UpdateFormControls();
            _dockPane.RefreshPanel1Buttons();

            // Show any message from loading the local layer details.
            if (localDetailsTask.Result != null!)
            {
                ShowMessage(localDetailsTask.Result, MessageType.Warning);
                if (message)
                    MessageBox.Show(localDetailsTask.Result, _displayName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show any message from loading the remote table details.
            if (localDetailsTask.Result != null!)
            {
                ShowMessage(localDetailsTask.Result, MessageType.Warning);
                if (message)
                    MessageBox.Show(localDetailsTask.Result, _displayName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Show any message from loading the remote table details.
            if (remoteDetailsTask.Result != null!)
            {
                ShowMessage(remoteDetailsTask.Result, MessageType.Warning);
                if (message)
                    MessageBox.Show(remoteDetailsTask.Result, _displayName, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Set the local and remote row counts.
            string localFeatures = _localCount < 0 ? "Error counting local features" : string.Format("{0} local features", _localCount.ToString());
            string remoteFeatures = _remoteCount < 0 ? "Error counting remote features" : string.Format("{0} remote features", _remoteCount.ToString());

            // Display the local and remote row counts.
            TableSummaryText = string.Format("{0}\r\n{1}", localFeatures, remoteFeatures);
        }

        /// <summary>
        /// Load the local table details.
        /// </summary>
        /// <returns>string: error message</returns>
        public async Task<string> LoadLocalDetailsAsync()
        {
            if (_mapFunctions == null || _mapFunctions.MapName == null || MapView.Active.Map.Name != _mapFunctions.MapName)
            {
                // Create a new map functions object.
                _mapFunctions = new();
            }

            // Check if there is an active map.
            bool mapOpen = _mapFunctions.MapName != null;

            if (!mapOpen)
                return "No active map open.";

            // Find the map layer by name.
            FeatureLayer localLayer = _mapFunctions.FindLayer(_localLayer);

            // Check the local layer is loaded.
            if (localLayer == null)
                return "Local layer '" + _localLayer + "' not found.";

            // Check the spatial column is in the layer.
            if (!await _mapFunctions.FieldExistsAsync(_localLayer, _spatialColumn))
                return string.Format("Key column '{0}' not found in local layer '{1}'", _keyColumn, _localLayer);

            // Check the key column is in the layer.
            if (!await _mapFunctions.FieldExistsAsync(_localLayer, _keyColumn))
                return string.Format("Spatial column '{0}' not found in local layer '{1}'", _keyColumn, _localLayer);

            // Count the number of rows in the layer.
            _localCount = await ArcGISFunctions.CountFeaturesAsync(localLayer);

            //TODO - Check for null geometry?
            //// Check for any rows with null geometry.
            //string whereClause = _spatialColumn + ' IS NULL';
            //await _mapFunctions.SelectLayerByAttributesAsync(layerName, whereClause, SelectionCombinationMethod.New);

            return null;
        }

        /// <summary>
        /// Load the remote table details.
        /// </summary>
        /// <returns>string: error message</returns>
        public async Task<string> LoadRemoteDetailsAsync()
        {
            // Check if the feature class exists.
            if (!await _sqlFunctions.FeatureClassExistsAsync(_remoteTable))
                return "Remote table '" + _remoteTable + "' not found.";

            // Check the spatial column is in the table.
            if (!await _sqlFunctions.FieldExistsAsync(_remoteTable, _spatialColumn))
                return string.Format("Key column '{0}' not found in remote table '{1}'", _keyColumn, _remoteTable);

            // Check the key column is in the table.
            if (!await _mapFunctions.FieldExistsAsync(_remoteTable, _keyColumn))
                return string.Format("Spatial column '{0}' not found in remote table '{1}'", _keyColumn, _remoteTable);

            // Count the number of rows in the remote table.
            _remoteCount = await _sqlFunctions.FeatureClassCountRowsAsync(_remoteTable);

            //TODO - Check for null geometry?
            //// Check for any rows with null geometry.
            //string whereClause = _spatialColumn + ' IS NULL';
            //await _mapFunctions.SelectLayerByAttributesAsync(_remoteTable, whereClause, SelectionCombinationMethod.New);

            return null;
        }

        /// <summary>
        /// Clear the list of result summary and details.
        /// </summary>
        /// <returns></returns>
        public void ClearFormLists()
        {
            // If not already processing.
            if (_dockPane.ProcessStatus == null)
            {
                // Clear the list of result summary.
                ResultSummaryList = [];

                // Clear the list of result detail.
                ResultDetailList = [];

                // Update the fields and buttons in the form.
                UpdateFormControls();
            }
        }

        /// <summary>
        /// Check the changes between the local layer and remote
        /// table before updating.
        /// </summary>
        /// <returns>bool</returns>
        private async Task<bool> CheckChangesAsync()
        {
            // Clear any messages.
            ClearMessage();

            // Replace any illegal characters in the user name string.
            _userID = StringFunctions.StripIllegals(Environment.UserName, "_", false);

            // User ID should be something at least.
            if (string.IsNullOrEmpty(_userID))
                _userID = "Temp";

            // Set the destination log file path.
            _logFile = _logFilePath + @"\DataSync_" + _userID + ".log";

            // Archive the log file (if it exists).
            if (ClearLogFile)
            {
                if (FileFunctions.FileExists(_logFile))
                {
                    // Get the last modified date/time for the current log file.
                    DateTime dateLogFile = File.GetLastWriteTime(_logFile);
                    string dateLastMod = dateLogFile.ToString("yyyy") + dateLogFile.ToString("MM") + dateLogFile.ToString("dd") + "_" +
                        dateLogFile.ToString("HH") + dateLogFile.ToString("mm") + dateLogFile.ToString("ss");

                    // Rename the current log file.
                    string logFileArchive = _logFilePath + @"\DataSync_" + _userID + "_" + dateLastMod + ".log";
                    if (!FileFunctions.RenameFile(_logFile, logFileArchive))
                    {
                        MessageBox.Show("Error: Cannot rename log file. Please make sure it is not open in another window.", _displayName, MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
            }

            // Create log file path.
            if (!FileFunctions.DirExists(_logFilePath))
            {
                try
                {
                    Directory.CreateDirectory(_logFilePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: Cannot create directory " + _logFilePath + ". System error: " + ex.Message);
                    return false;
                }
            }

            // If userid is temp.
            if (_userID == "Temp")
                FileFunctions.WriteLine(_logFile, "User ID not found. User ID used will be 'Temp'.");

            // Reset the check has run flag.
            _checkHasRun = false;

            //TODO - Needed?
            // Expand the detail list (ready to be resized later).
            _resultDetailListHeight = null;

            // Update the fields and buttons in the form.
            UpdateFormControls();
            _dockPane.RefreshPanel1Buttons();

            // Reset the result summary and detail lists.
            ResultSummaryList = [];
            ResultDetailList = [];

            // Clear the local layer features selection.
            await _mapFunctions.ClearLayerSelectionAsync(_localLayer);

            _dockPane.ProgressUpdate("Uploading local layer to server", -1, -1);

            FileFunctions.WriteLine(_logFile, "Uploading local layer to server ...");

            // Get the full local layer path (in case it's nested in one or more groups).
            string localLayerPath = _mapFunctions.GetLayerPath(_localLayer);

            // Set the full remote table path.
            string remoteTablePath = _sdeFileName + @"\" + _defaultSchema + "." + _remoteTable;

            if (!await ArcGISFunctions.CopyFeaturesAsync(localLayerPath, remoteTablePath + "_TEMP", false))
            {
                FileFunctions.WriteLine(_logFile, "Error: Uploading local layer.");
                //_extractErrors = true;
                return false;
            }

            FileFunctions.WriteLine(_logFile, "Upload to server complete.");

            // Check if the remote map layer is loaded.
            if (_mapFunctions.FindLayer(_remoteLayer) == null)
            {
                _remoteLayerAdded = false;

                FileFunctions.WriteLine(_logFile, "Adding remote layer to map.");

                // Get the position of the local layer in the map.
                int localIndex = _mapFunctions.FindLayerIndex(_localLayer) + 1;

                // Add the remote table to the map below the local table.
                if (!await _mapFunctions.AddLayerToMapAsync(remoteTablePath, localIndex, _remoteLayer))
                {
                    FileFunctions.WriteLine(_logFile, "Error: Adding remote layer to map.");
                    //_extractErrors = true;
                    return false;
                }

                // Flag that the remote layer has been added to the map.
                _remoteLayerAdded = true;
            }

            _dockPane.ProgressUpdate("Checking updates in local layer", -1, -1);

            FileFunctions.WriteLine(_logFile, "Checking updates in local layer ...");

            string resultsTable = _remoteTable + "_SYNC";

            // Delete the results table before we start.
            if (await _sqlFunctions.TableExistsAsync(resultsTable))
                await _sqlFunctions.DeleteTableAsync(resultsTable);

            // Execute the stored procedure to check the local layer and remote table.
            long resultsCount = await PerformSQLCheckAsync(_defaultSchema, _remoteTable, _keyColumn, _spatialColumn, resultsTable);

            // Check the results table has been created.
            if (resultsCount < 0)
            {
                FileFunctions.WriteLine(_logFile, "Error: Checking updates in local layer.");
                //_extractErrors = true;
                return false;
            }

            FileFunctions.WriteLine(_logFile, "Check of updates complete.");

            _dockPane.ProgressUpdate(null, -1, -1);

            // Get all of the sync results.
            string resultTypeColumn = "Type";
            string newRefColumn = "Ref";
            string oldRefColumn = "RefOld";
            string newAreaColumn = "Area";
            string oldAreaColumn = "AreaOld";
            _resultDetail = await _sqlFunctions.GetSyncResultsAsync(resultsTable, resultTypeColumn, newRefColumn, oldRefColumn, newAreaColumn, oldAreaColumn);

            // Get a summary of the results.
            _resultSummary = _resultDetail.GroupBy(t => t.ResultType).Select(s => new ResultSummary()
            {
                Type = s.Key,
                Count = s.Count()
            }).OrderBy(r => r.Type).ToList();

            // Set the list of result summary.
            ResultSummaryList = new ObservableCollection<ResultSummary>(_resultSummary);

            // Set the check has run flag.
            _checkHasRun = true;

            // Update the fields and buttons in the form.
            UpdateFormControls();
            _dockPane.RefreshPanel1Buttons();

            // Force result detail list height to reset.
            ResultDetailListExpandCommandClick(null);

            return true;
        }

        /// <summary>
        /// Apply the changes to the remote table.
        /// </summary>
        /// <returns>bool</returns>
        private async Task<bool> ApplyChangesAsync()
        {
            _dockPane.ProgressUpdate("Applying updates to remote table", -1, -1);

            // Execute the stored procedure to update the remote table.
            if (await PerformSQLUpdateAsync(_defaultSchema, _remoteTable))
            {
                FileFunctions.WriteLine(_logFile, "Error: Applying updates to remote table.");
                _syncErrors = true;
                return false;
            }

            // Check the updated remote feature class exists.
            if (!await _sqlFunctions.FeatureClassExistsAsync(_remoteTable))
            {
                FileFunctions.WriteLine(_logFile, "Error: Updated remote table is not found.");
                _syncErrors = true;
                return false;
            }

            // Count the number of rows in the remote table.
            _remoteCount = await _sqlFunctions.FeatureClassCountRowsAsync(_remoteTable);

            if (_remoteCount <= 0)
            {
                FileFunctions.WriteLine(_logFile, "Error: Updated remote table is empty.");
                _syncErrors = true;
                return false;
            }

            FileFunctions.WriteLine(_logFile, _remoteCount.ToString() + " rows in updated remote table.");
            FileFunctions.WriteLine(_logFile, "Updates to remote table complete.");
            FileFunctions.WriteLine(_logFile, "----------------------------------------------------------------------");

            // Increment the progress value to the last step.
            _dockPane.ProgressUpdate("Cleaning up...", -1, -1);

            // Clean up after the sync.
            await CleanUpSyncAsync();

            return true;
        }

        /// <summary>
        /// Indicate that the sync process has stopped (either
        /// successfully or otherwise).
        /// </summary>
        /// <param name="message"></param>
        /// <param name="image"></param>
        private void StopSync(string message, string image)
        {
            FileFunctions.WriteLine(_logFile, "---------------------------------------------------------------------------");
            FileFunctions.WriteLine(_logFile, message);
            FileFunctions.WriteLine(_logFile, "---------------------------------------------------------------------------");

            // Resume the map redrawing.
            _mapFunctions.PauseDrawing(false);

            // Indicate extract has finished.
            _dockPane.SyncRunning = false;
            _dockPane.ProgressUpdate(null, -1, -1);

            string imageSource = string.Format("pack://application:,,,/DataSync;component/Images/{0}32.png", image);

            // Notify user of completion.
            Notification notification = new()
            {
                Title = _displayName,
                Severity = Notification.SeverityLevel.High,
                Message = message,
                ImageSource = new BitmapImage(new Uri(imageSource)) as ImageSource
            };
            FrameworkApplication.AddNotification(notification);

            // Open the log file (if required).
            if (OpenLogFile || _syncErrors)
                Process.Start("notepad.exe", _logFile);
        }

        /// <summary>
        /// Clean up after the sync has completed (successfully or not).
        /// </summary>
        /// <returns></returns>
        private async Task CleanUpSyncAsync()
        {
            FileFunctions.WriteLine(_logFile, "");

            // Clear the layer features selection.
            await _mapFunctions.ClearLayerSelectionAsync(_localLayer);

            // Clear the temporary remote tables.
            await ClearSQLTableAsync(_defaultSchema, _remoteTable);

            //// Remove the remote layer (if added).
            //if (_remoteLayerAdded)
            //{
            //    FileFunctions.WriteLine(_logFile, "Removing remote layer from map.");
            //    await ClearMapTablesAsync(_remoteLayer);
            //}
        }

        /// <summary>
        /// Perform the spatial selection via a stored procedure.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="remoteTable"></param>
        /// <param name="keyColumn"></param>
        /// <param name="spatialColumn"></param>
        /// <returns></returns>
        internal async Task<long> PerformSQLCheckAsync(string schema, string remoteTable, string keyColumn, string spatialColumn, string resultsTable)
        {
            bool success;
            long tableCount = 0;

            // Get the name of the stored procedure to execute selection in SQL Server.
            string storedProcedureName = _checkStoredProcedure;

            // Set up the SQL command.
            StringBuilder sqlCmd = new();

            // Build the SQL command to execute the stored procedure.
            sqlCmd = sqlCmd.Append(string.Format("EXECUTE {0}", storedProcedureName));
            sqlCmd.Append(string.Format(" '{0}'", schema));
            sqlCmd.Append(string.Format(", '{0}'", resultsTable));
            sqlCmd.Append(string.Format(", '{0}'", remoteTable));
            sqlCmd.Append(string.Format(", '{0}'", keyColumn));
            sqlCmd.Append(string.Format(", '{0}'", spatialColumn));
            sqlCmd.Append(string.Format(", '{0}'", spatialColumn));

            string sqlOutputTable = schema + '.' + resultsTable;

            try
            {
                FileFunctions.WriteLine(_logFile, "Executing SQL comparison for '" + remoteTable + "' ...");

                // Execute the stored procedure.
                await _sqlFunctions.ExecuteSQLOnGeodatabase(sqlCmd.ToString());

                // Check if the output feature class exists.
                if (!await _sqlFunctions.TableExistsAsync(sqlOutputTable))
                    success = false;

                // Count the number of rows in the output feature count.
                tableCount = await _sqlFunctions.TableCountRowsAsync(sqlOutputTable);

                success = true;

            }
            catch (Exception ex)
            {
                FileFunctions.WriteLine(_logFile, "Error: Executing the stored procedure: " + ex.Message);
                success = false;
            }

            if (!success)
                return -1;

            //FileFunctions.WriteLine(_logFile, "SQL spatial selection complete.");

            return tableCount;
        }

        /// <summary>
        /// Perform the update of the remote table via a
        /// stored procedure.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="remoteTable"></param>
        /// <returns>bool</returns>
        internal async Task<bool> PerformSQLUpdateAsync(string schema, string remoteTable)
        {
            // Get the name of the stored procedure to execute selection in SQL Server.
            string storedProcedureName = _updateStoredProcedure;

            // Set up the SQL command.
            StringBuilder sqlCmd = new();

            // Build the SQL command to execute the stored procedure.
            sqlCmd = sqlCmd.Append(string.Format("EXECUTE {0}", storedProcedureName));
            sqlCmd.Append(string.Format(" '{0}'", schema));
            sqlCmd.Append(string.Format(", '{0}'", remoteTable));

            try
            {
                FileFunctions.WriteLine(_logFile, "Applying updates to remote table ...");

                // Execute the stored procedure.
                await _sqlFunctions.ExecuteSQLOnGeodatabase(sqlCmd.ToString());

            }
            catch (Exception ex)
            {
                FileFunctions.WriteLine(_logFile, "Error: Executing the stored procedure: " + ex.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clear the temporary SQL spatial table by running a stored procedure.
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="tableName"></param>
        /// <returns>bool</returns>
        internal async Task<bool> ClearSQLTableAsync(string schema, string tableName)
        {
            // Set up the SQL command.
            StringBuilder sqlCmd = new();

            // Get the name of the stored procedure to clear the
            // temporary tables in SQL Server.
            string clearSpatialSPName = _clearStoredProcedure;

            // Build the SQL command to execute the stored procedure.
            sqlCmd = sqlCmd.Append(string.Format("EXECUTE {0}", clearSpatialSPName));
            sqlCmd.Append(string.Format(" '{0}'", schema));
            sqlCmd.Append(string.Format(", '{0}'", tableName));

            try
            {
                //FileFunctions.WriteLine(_logFile, "Deleting SQL temporary table");

                // Execute the stored procedure.
                await _sqlFunctions.ExecuteSQLOnGeodatabase(sqlCmd.ToString());
            }
            catch (Exception ex)
            {
                FileFunctions.WriteLine(_logFile, "Error: Deleting the SQL temporary tables: " + ex.Message);
                return false;
            }

            return true;
        }

        internal async Task<bool> ClearMapTablesAsync(string layerName)
        {
            // Remove the remote layer from the map.
            if (!await _mapFunctions.RemoveLayerAsync(layerName))
            {
                FileFunctions.WriteLine(_logFile, "Error: Removing layer '" + layerName + "' from map.");
                return false;
            }

            return true;
        }

        internal async Task ZoomToResultAsync(string layerName, string keyValue)
        {
            // Get the feature layer.
            FeatureLayer featurelayer = _mapFunctions.FindLayer(layerName);
            if (featurelayer == null)
            {
                ShowMessage("Layer '" + layerName + "' not found in map", MessageType.Warning);
                return;
            }

            // Create the search query.
            string searchClause;
            if (keyValue == null)
                searchClause = _keyColumn + " IS NULL";
            if (keyValue == "")
                searchClause = _keyColumn + " = ''";
            else
                searchClause = _keyColumn + " = '" + keyValue + "'";

            // Select the feature matching the key ref in the map (don't wait).
            if (!await _mapFunctions.SelectLayerByAttributesAsync(layerName, searchClause, SelectionCombinationMethod.New))
            {
                ShowMessage("Error funding feature in layer '" + layerName + "'", MessageType.Warning);
                return;
            }

            // Exit if no features selected.
            if (featurelayer.SelectionCount == 0)
            {
                ShowMessage("Feature not found in layer '" + layerName + "'", MessageType.Warning);
                return;
            }

            // Get the OID of the selected feature.
            IReadOnlyList<long> selectedOIDs = [];
            try
            {
                await QueuedTask.Run(() =>
                {
                    // Get the oids for the selected features.
                    var gsSelection = featurelayer.GetSelection();
                    selectedOIDs = gsSelection.GetObjectIDs();
                });
            }
            catch
            {
                // Handle Exception.
                return;
            }

            // If any OIDs were found.
            if ((selectedOIDs == null) || (selectedOIDs.Count == 0))
                return;

            // If only one object was found.
            if (selectedOIDs.Count == 1)
            {
                // Zoom to the selected feature.
                long selectedOID = selectedOIDs[0];
                await _mapFunctions.ZoomToLayerAsync(layerName, selectedOID, 2, null);
            }
            // Multiple objects were found.
            else
            {
                // Zoom to all of the selected features.
                await _mapFunctions.ZoomToLayerAsync(layerName, selectedOIDs);

                ShowMessage("Multiple features found for selected feature", MessageType.Warning);
            }
        }

        #endregion Methods

        #region ResultDetailListExpand Command

        private ICommand _resultDetailListExpandCommand;

        /// <summary>
        /// Create ResultDetailList Expand button command.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
        public ICommand ResultDetailListExpandCommand
        {
            get
            {
                if (_resultDetailListExpandCommand == null)
                {
                    Action<object> expandResultDetailListAction = new(ResultDetailListExpandCommandClick);
                    _resultDetailListExpandCommand = new RelayCommand(expandResultDetailListAction, param => true);
                }
                return _resultDetailListExpandCommand;
            }
        }

        /// <summary>
        /// Handles event when ResultDetailListExpand button is pressed.
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void ResultDetailListExpandCommandClick(object param)
        {
            if (_resultDetailListHeight == null)
                _resultDetailListHeight = 422;
            else
                _resultDetailListHeight = null;

            OnPropertyChanged(nameof(ResultDetailListHeight));
            OnPropertyChanged(nameof(ResultDetailListExpandButtonContent));
        }

        #endregion ResultDetailListExpand Command

        //#region SQL

        ///// <summary>
        ///// Check if the table contains a spatial column in the columns text.
        ///// </summary>
        ///// <param name="tableName"></param>
        ///// <param name="columnsText"></param>
        ///// <returns>string: spatial column</returns>
        //internal async Task<string> IsSQLTableSpatialAsync(string tableName, string columnsText)
        //{
        //    string[] geometryFields = ["SP_GEOMETRY", "Shape"]; // Expand as required.

        //    // Get the list of field names in the selected table.
        //    List<string> fieldsList = await _sqlFunctions.GetFieldNamesListAsync(tableName);

        //    // Loop through the geometry fields looking for a match.
        //    foreach (string geomField in geometryFields)
        //    {
        //        // If the columns text contains the geometry field.
        //        if (columnsText.Contains(geomField, StringComparison.OrdinalIgnoreCase))
        //        {
        //            return geomField;
        //        }
        //        // If "*" is used check for the existence of the geometry field in the table.
        //        else if (columnsText.Equals("*", StringComparison.OrdinalIgnoreCase))
        //        {
        //            foreach (string fieldName in fieldsList)
        //            {
        //                // If the column text contains the geometry field.
        //                if (fieldName.Equals(geomField, StringComparison.OrdinalIgnoreCase))
        //                    return geomField;
        //            }
        //        }
        //    }

        //    // No geometry column found.
        //    return null;
        //}

        //#endregion SQL

        #region Debugging Aides

        /// <summary>
        /// Warns the developer if this object does not have
        /// a public property with the specified name. This
        /// method does not exist in a Release build.
        /// </summary>
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public void VerifyPropertyName(string propertyName)
        {
            // Verify that the property name matches a real,
            // public, instance property on this object.
            if (TypeDescriptor.GetProperties(this)[propertyName] == null)
            {
                string msg = "Invalid property name: " + propertyName;

                if (ThrowOnInvalidPropertyName)
                    throw new(msg);
                else
                    Debug.Fail(msg);
            }
        }

        /// <summary>
        /// Returns whether an exception is thrown, or if a Debug.Fail() is used
        /// when an invalid property name is passed to the VerifyPropertyName method.
        /// The default value is false, but subclasses used by unit tests might
        /// override this property's getter to return true.
        /// </summary>
        protected virtual bool ThrowOnInvalidPropertyName { get; private set; }

        #endregion Debugging Aides

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public new event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        internal virtual void OnPropertyChanged(string propertyName)
        {
            VerifyPropertyName(propertyName);

            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                PropertyChangedEventArgs e = new(propertyName);
                handler(this, e);
            }
        }

        #endregion INotifyPropertyChanged Members
    }

    #region ResultSummary Class

    /// <summary>
    /// ResultSummary to display.
    /// </summary>
    public class ResultSummary : INotifyPropertyChanged
    {
        #region Fields

        public string Type { get; set; }

        public int Count { get; set; }

        private bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;

                OnPropertyChanged(nameof(IsSelected));
            }
        }

        #endregion Fields

        #region Creator

        public ResultSummary()
        {
            // constructor takes no arguments.
        }

        public ResultSummary(string summaryType)
        {
            Type = summaryType;
        }

        public ResultSummary(string summaryType, int summaryCount)
        {
            Type = summaryType;
            Count = summaryCount;
        }

        #endregion Creator

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        internal virtual void OnPropertyChanged(string propertyName)
        {
            //VerifyPropertyName(propertyName);

            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                PropertyChangedEventArgs e = new(propertyName);
                handler(this, e);
            }
        }

        #endregion INotifyPropertyChanged Members
    }

    #endregion ResultSummary Class

    #region ResultDetail Class

    /// <summary>
    /// ResultDetail to display.
    /// </summary>
    public class ResultDetail : INotifyPropertyChanged
    {
        #region Fields

        public string ResultType { get; set; }

        public string NewRef { get; set; }

        public string OldRef { get; set; }

        public string NewArea { get; set; }

        public string OldArea { get; set; }

        private bool _isSelected;

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;

                OnPropertyChanged(nameof(IsSelected));
            }
        }

        #endregion Fields

        #region Creator

        public ResultDetail()
        {
            // constructor takes no arguments.
        }

        public ResultDetail(string resultType)
        {
            ResultType = resultType;
        }

        public ResultDetail(string resultType, string newRef, string oldRef, float newArea, float oldArea)
        {
            ResultType = resultType;
            NewRef = newRef;
            OldRef = oldRef;
            NewArea = newArea.ToString();
            OldArea = oldArea.ToString();
        }

        #endregion Creator

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        internal virtual void OnPropertyChanged(string propertyName)
        {
            //VerifyPropertyName(propertyName);

            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                PropertyChangedEventArgs e = new(propertyName);
                handler(this, e);
            }
        }

        #endregion INotifyPropertyChanged Members
    }

    #endregion ResultDetail Class
}