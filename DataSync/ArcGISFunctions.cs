﻿// The DataTools are a suite of ArcGIS Pro addins used to extract
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2024 Andy Foy Consulting.
//
// This file is part of DataTools suite of programs..
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

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Editing.Attributes;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using DataSync.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QueryFilter = ArcGIS.Core.Data.QueryFilter;

namespace DataTools
{
    /// <summary>
    /// This class provides ArcGIS Pro map functions.
    /// </summary>
    internal class MapFunctions
    {
        #region Fields

        private Map _activeMap;
        private MapView _activeMapView;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Set the global variables.
        /// </summary>
        public MapFunctions()
        {
            // Get the active map view (if there is one).
            _activeMapView = GetActiveMapView();

            // Set the map currently displayed in the active map view.
            if (_activeMapView != null)
                _activeMap = _activeMapView.Map;
            else
                _activeMap = null;
        }

        #endregion Constructor

        #region Properties

        /// <summary>
        /// The name of the active map.
        /// </summary>
        public string MapName
        {
            get
            {
                if (_activeMap == null)
                    return null;
                else
                    return _activeMap.Name;
            }
        }

        #endregion Properties

        #region Map

        /// <summary>
        /// Get the active map view.
        /// </summary>
        /// <returns></returns>
        internal static MapView GetActiveMapView()
        {
            // Get the active map view.
            MapView mapView = MapView.Active;
            if (mapView == null)
                return null;

            return mapView;
        }

        /// <summary>
        /// Pause or resume bool in the active map.
        /// </summary>
        /// <param name="pause"></param>
        public void PauseDrawing(bool pause)
        {
            _activeMapView.DrawingPaused = pause;
        }

        /// <summary>
        /// Create a new map and return the map name.
        /// </summary>
        /// <param name="mapName"></param>
        /// <returns>string</returns>
        public async Task<string> CreateMapAsync(string mapName)
        {
            _activeMap = null;
            _activeMapView = null;

            // If no map name is supplied.
            if (String.IsNullOrEmpty(mapName))
                return null;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Create a new map without a base map.
                    _activeMap = MapFactory.Instance.CreateMap(mapName, basemap: Basemap.None);

                    // Create and activate new map.
                    ProApp.Panes.CreateMapPaneAsync(_activeMap, MapViewingMode.Map);
                });

                // Get the active map view;
                _activeMapView = GetActiveMapView();
            }
            catch
            {
                // Handle Exception.
                return null;
            }

            return _activeMap.Name;
        }

        /// <summary>
        /// Add a layer to the active map.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>bool</returns>
        public async Task<bool> AddLayerToMapAsync(string url, int index = 0, string layerName = "")
        {
            // If no url is supplied.
            if (url == null)
                return false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    Uri uri = new(url);

                    // Check if the layer is already loaded (unlikely as the map is new)
                    Layer findLayer = _activeMap.Layers.FirstOrDefault(t => t.Name == uri.Segments.Last());

                    // If the layer is not loaded, add it.
                    if (findLayer == null)
                    {
                        Layer layer = LayerFactory.Instance.CreateLayer(uri, _activeMap, index, layerName);
                    }
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Add a standalone layer to the active map.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>bool</returns>
        public async Task<bool> AddTableToMapAsync(string url)
        {
            // If no url is supplied.
            if (url == null)
                return false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    Uri uri = new(url);

                    // Check if the layer is already loaded.
                    StandaloneTable findTable = _activeMap.StandaloneTables.FirstOrDefault(t => t.Name == uri.Segments.Last());

                    // If the layer is not loaded, add it.
                    if (findTable == null)
                    {
                        StandaloneTable table = StandaloneTableFactory.Instance.CreateStandaloneTable(uri, _activeMap);
                    }
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Zoom to a an object for a given ratio or scale.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="objectID"></param>
        /// <param name="factor"></param>
        /// <param name="mapScaleOrDistance"></param>
        /// <returns>bool</returns>
        public async Task<bool> ZoomToLayerAsync(string layerName, long objectID, double? factor, double? mapScaleOrDistance)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return false;

            // Check if the layer is already loaded.
            BasicFeatureLayer findLayer = FindLayer(layerName);

            // If the layer is not loaded.
            if (findLayer == null)
                return false;

            try
            {
                // Zoom to the extent of the object.
                await _activeMapView.ZoomToAsync(findLayer, objectID, null, true, factor, mapScaleOrDistance);
            }
            catch
            {
                // Handle exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Zoom to a list of objects.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="objectIDs"></param>
        /// <returns>bool</returns>
        public async Task<bool> ZoomToLayerAsync(string layerName, IEnumerable<long> objectIDs)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return false;

            // Check if the layer is already loaded.
            BasicFeatureLayer findLayer = FindLayer(layerName);

            // If the layer is not loaded.
            if (findLayer == null)
                return false;

            try
            {
                // Zoom to the extent of all of the objects.
                await _activeMapView.ZoomToAsync(findLayer, objectIDs, null, true);
            }
            catch
            {
                // Handle exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Zoom to a layer for a given ratio or scale.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="ratio"></param>
        /// <param name="scale"></param>
        /// <returns>bool</returns>
        public async Task<bool> ZoomToLayerAsync(string layerName, bool selectedOnly, double ratio = 1, double scale = 10000)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return false;

            // Check if the layer is already loaded.
            Layer findLayer = FindLayer(layerName);

            // If the layer is not loaded.
            if (findLayer == null)
                return false;

            try
            {
                // Zoom to the layer extent.
                await _activeMapView.ZoomToAsync(findLayer, selectedOnly);

                // Get the camera for the active view.
                var camera = _activeMapView.Camera;

                // Adjust the camera scale.
                if (ratio != 1)
                    camera.Scale *= ratio;
                else if (scale > 0)
                    camera.Scale = scale;

                // Zoom to the new camera position.
                await _activeMapView.ZoomToAsync(camera);
            }
            catch
            {
                // Handle exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Zoom to a layer for a given ratio or scale.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="ratio"></param>
        /// <param name="scale"></param>
        /// <returns>bool</returns>
        public bool ZoomToLayer(string layerName, double ratio = 1, double scale = 10000)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return false;

            // Check if the layer is already loaded.
            Layer findLayer = FindLayer(layerName);

            // If the layer is not loaded.
            if (findLayer == null)
                return false;

            try
            {
                // Zoom to the layer extent.
                _activeMapView.ZoomTo(findLayer, false);

                // Get the camera for the active view.
                var camera = _activeMapView.Camera;

                // Adjust the camera scale.
                if (ratio != 1)
                    camera.Scale *= ratio;
                else if (scale > 0)
                    camera.Scale = scale;

                // Zoom to the new camera position.
                _activeMapView.ZoomTo(camera);
            }
            catch
            {
                // Handle exception.
                return false;
            }

            return true;
        }

        #endregion Map

        #region Layers

        /// <summary>
        /// Find a feature layer by name in the active map.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>FeatureLayer</returns>
        internal FeatureLayer FindLayer(string layerName)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return null;

            // Finds layers by name and returns a read only list of feature layers.
            IEnumerable<FeatureLayer> layers = _activeMap.FindLayers(layerName, true).OfType<FeatureLayer>();

            // If no layers are loaded.
            if (layers == null)
                return null;

            try
            {
                while (layers.Any())
                {
                    // Get the first feature layer found by name.
                    FeatureLayer layer = layers.First();

                    // Check the feature layer is in the active map.
                    if (layer.Map.Name.Equals(_activeMap.Name, StringComparison.OrdinalIgnoreCase))
                        return layer;
                }
            }
            catch
            {
                // Handle exception.
                return null;
            }

            return null;
        }

        /// <summary>
        /// Find the position index for a feature layer by name in the active map.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>int</returns>
        internal int FindLayerIndex(string layerName)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return 0;

            // Finds layers by name and returns a read only list of feature layers.
            IEnumerable<FeatureLayer> layers = _activeMap.FindLayers(layerName, true).OfType<FeatureLayer>();

            // If no layers are loaded.
            if (layers == null)
                return 0;

            try
            {
                for (int index = 0; index < _activeMap.Layers.Count; index++)
                {
                    // Get the index of the first feature layer found by name.
                    if (_activeMap.Layers[index].Name == layerName)
                        return index;
                }
            }
            catch
            {
                // Handle exception.
                return 0;
            }

            return 0;
        }

        /// <summary>
        /// Remove a layer by name from the active map.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>bool</returns>
        public async Task<bool> RemoveLayerAsync(string layerName)
        {
            // Check there is an input layer name.
            if (String.IsNullOrEmpty(layerName))
                return false;

            try
            {
                // Find the layer in the active map.
                FeatureLayer layer = FindLayer(layerName);

                // Remove the layer.
                if (layer != null)
                    return await RemoveLayerAsync(layer);
            }
            catch
            {
                // Handle exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Remove a layer from the active map.
        /// </summary>
        /// <param name="layer"></param>
        /// <returns>bool</returns>
        public async Task<bool> RemoveLayerAsync(Layer layer)
        {
            // Check there is an input layer.
            if (layer == null)
                return false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Remove the layer.
                    if (layer != null)
                        _activeMap.RemoveLayer(layer);
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Add incremental numbers to the label field in a feature class.
        /// </summary>
        /// <param name="outputFeatureClass"></param>
        /// <param name="outputLayerName"></param>
        /// <param name="labelFieldName"></param>
        /// <param name="keyFieldName"></param>
        /// <param name="startNumber"></param>
        /// <returns>int</returns>
        public async Task<int> AddIncrementalNumbersAsync(string outputFeatureClass, string outputLayerName, string labelFieldName, string keyFieldName, int startNumber = 1)
        {
            // Check the input parameters.
            if (!await ArcGISFunctions.FeatureClassExistsAsync(outputFeatureClass))
                return -1;

            if (!await FieldExistsAsync(outputLayerName, labelFieldName))
                return -1;

            if (!await FieldIsNumericAsync(outputLayerName, labelFieldName))
                return -1;

            if (!await FieldExistsAsync(outputLayerName, keyFieldName))
                return -1;

            // Get the feature layer.
            FeatureLayer outputFeaturelayer = FindLayer(outputLayerName);
            if (outputFeaturelayer == null)
                return -1;

            int labelMax;
            if (startNumber > 1)
                labelMax = startNumber - 1;
            else
                labelMax = 0;
            int labelVal = labelMax;

            string keyValue;
            string lastKeyValue = "";

            // Create an edit operation.
            EditOperation editOperation = new();

            try
            {
                await QueuedTask.Run(() =>
                {
                    /// Get the feature class for the output feature layer.
                    FeatureClass featureClass = outputFeaturelayer.GetFeatureClass();

                    // Get the feature class defintion.
                    using FeatureClassDefinition featureClassDefinition = featureClass.GetDefinition();

                    // Get the key field from the feature class definition.
                    ArcGIS.Core.Data.Field keyField = featureClassDefinition.GetFields()
                      .First(x => x.Name.Equals(keyFieldName, StringComparison.OrdinalIgnoreCase));

                    // Create a SortDescription for the key field.
                    ArcGIS.Core.Data.SortDescription keySortDescription = new(keyField)
                    {
                        CaseSensitivity = CaseSensitivity.Insensitive,
                        SortOrder = ArcGIS.Core.Data.SortOrder.Ascending
                    };

                    // Create a TableSortDescription.
                    TableSortDescription tableSortDescription = new([keySortDescription]);

                    // Create a cursor of the sorted features.
                    using RowCursor rowCursor = featureClass.Sort(tableSortDescription);
                    while (rowCursor.MoveNext())
                    {
                        // Using the current row.
                        using Row record = rowCursor.Current;

                        // Get the key field value.
                        keyValue = record[keyFieldName].ToString();

                        // If the key value is different.
                        if (keyValue != lastKeyValue)
                        {
                            labelMax++;
                            labelVal = labelMax;
                        }

                        editOperation.Modify(record, labelFieldName, labelVal);

                        lastKeyValue = keyValue;
                    }
                });

                // Execute the edit operation.
                if (!editOperation.IsEmpty)
                {
                    if (!await editOperation.ExecuteAsync())
                    {
                        //MessageBox.Show(editOperation.ErrorMessage);
                        return -1;
                    }
                }

                // Check for unsaved edits.
                if (Project.Current.HasEdits)
                {
                    // Save edits.
                    await Project.Current.SaveEditsAsync();
                }
            }
            catch
            {
                // Handle Exception.
                return 0;
            }

            return labelMax;
        }

        /// <summary>
        /// Update the selected features in a feature class.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="siteColumn"></param>
        /// <param name="siteName"></param>
        /// <param name="orgColumn"></param>
        /// <param name="orgName"></param>
        /// <param name="radiusColumn"></param>
        /// <param name="radiusText"></param>
        /// <returns>bool</returns>
        public async Task<bool> UpdateFeaturesAsync(string layerName, string siteColumn, string siteName, string orgColumn, string orgName, string radiusColumn, string radiusText)
        {
            // Check the input parameters.
            if (String.IsNullOrEmpty(layerName))
                return false;

            if (String.IsNullOrEmpty(siteColumn) && String.IsNullOrEmpty(orgColumn) && String.IsNullOrEmpty(radiusColumn))
                return false;

            if (!string.IsNullOrEmpty(siteColumn) && !await FieldExistsAsync(layerName, siteColumn))
                return false;

            if (!string.IsNullOrEmpty(orgColumn) && !await FieldExistsAsync(layerName, orgColumn))
                return false;

            if (!string.IsNullOrEmpty(radiusColumn) && !await FieldExistsAsync(layerName, radiusColumn))
                return false;

            // Get the feature layer.
            FeatureLayer featurelayer = FindLayer(layerName);

            if (featurelayer == null)
                return false;

            // Create an edit operation.
            EditOperation editOperation = new();

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Get the oids for the selected features.
                    var gsSelection = featurelayer.GetSelection();
                    IReadOnlyList<long> selectedOIDs = gsSelection.GetObjectIDs();

                    // Update the attributes of the selected features.
                    var insp = new Inspector();
                    insp.Load(featurelayer, selectedOIDs);

                    if (!string.IsNullOrEmpty(siteColumn))
                    {
                        // Double check that attribute exists.
                        if (insp.FirstOrDefault(a => a.FieldName.Equals(siteColumn, StringComparison.OrdinalIgnoreCase)) != null)
                            insp[siteColumn] = siteName;
                    }

                    if (!string.IsNullOrEmpty(orgColumn))
                    {
                        // Double check that attribute exists.
                        if (insp.FirstOrDefault(a => a.FieldName.Equals(orgColumn, StringComparison.OrdinalIgnoreCase)) != null)
                            insp[orgColumn] = orgName;
                    }

                    if (!string.IsNullOrEmpty(radiusColumn))
                    {
                        // Double check that attribute exists.
                        if (insp.FirstOrDefault(a => a.FieldName.Equals(radiusColumn, StringComparison.OrdinalIgnoreCase)) != null)
                            insp[radiusColumn] = radiusText;
                    }

                    editOperation.Modify(insp);
                });

                // Execute the edit operation.
                if (!editOperation.IsEmpty)
                {
                    if (!await editOperation.ExecuteAsync())
                    {
                        //MessageBox.Show(editOperation.ErrorMessage);
                        return false;
                    }
                }

                // Check for unsaved edits.
                if (Project.Current.HasEdits)
                {
                    // Save edits.
                    return await Project.Current.SaveEditsAsync();
                }
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Select features in feature class by location.
        /// </summary>
        /// <param name="targetLayer"></param>
        /// <param name="searchLayer"></param>
        /// <param name="overlapType"></param>
        /// <param name="searchDistance"></param>
        /// <param name="selectionType"></param>
        /// <returns>bool</returns>
        public static async Task<bool> SelectLayerByLocationAsync(string targetLayer, string searchLayer,
            string overlapType = "INTERSECT", string searchDistance = "", string selectionType = "NEW_SELECTION")
        {
            // Check if there is an input target layer name.
            if (String.IsNullOrEmpty(targetLayer))
                return false;

            // Check if there is an input search layer name.
            if (String.IsNullOrEmpty(searchLayer))
                return false;

            // Make a value array of strings to be passed to the tool.
            IReadOnlyList<string> parameters = Geoprocessing.MakeValueArray(targetLayer, overlapType, searchLayer, searchDistance, selectionType);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.SelectLayerByLocation", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.SelectLayerByLocation", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Select features in layerName by attributes.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="whereClause"></param>
        /// <param name="selectionMethod"></param>
        /// <returns>bool</returns>
        public async Task<bool> SelectLayerByAttributesAsync(string layerName, string whereClause, SelectionCombinationMethod selectionMethod)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return false;

            try
            {
                // Find the feature layerName by name if it exists. Only search existing layers.
                FeatureLayer featurelayer = FindLayer(layerName);

                if (featurelayer == null)
                    return false;

                // Create a query filter using the where clause.
                QueryFilter queryFilter = new()
                {
                    WhereClause = whereClause
                };

                await QueuedTask.Run(() =>
                {
                    // Select the features matching the search clause.
                    featurelayer.Select(queryFilter, selectionMethod);
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clear selected features in a feature layer.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>bool</returns>
        public async Task<bool> ClearLayerSelectionAsync(string layerName)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return false;

            try
            {
                // Find the feature layerName by name if it exists. Only search existing layers.
                FeatureLayer featurelayer = FindLayer(layerName);

                if (featurelayer == null)
                    return false;

                await QueuedTask.Run(() =>
                {
                    // Clear the feature selection.
                    featurelayer.ClearSelection();
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get the list of fields for a feature class.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <returns>IReadOnlyList<ArcGIS.Core.Data.Field></returns>
        public async Task<IReadOnlyList<ArcGIS.Core.Data.Field>> GetFCFieldsAsync(string layerPath)
        {
            // Check there is an input feature layer path.
            if (String.IsNullOrEmpty(layerPath))
                return null;

            try
            {
                // Find the feature layer by name if it exists. Only search existing layers.
                FeatureLayer featurelayer = FindLayer(layerPath);

                if (featurelayer == null)
                    return null;

                IReadOnlyList<ArcGIS.Core.Data.Field> fields = null;
                List<string> fieldList = [];

                await QueuedTask.Run(() =>
                {
                    // Get the underlying feature class as a table.
                    ArcGIS.Core.Data.Table table = featurelayer.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        fields = tableDef.GetFields();
                    }

                    table.Dispose();
                });

                return fields;
            }
            catch
            {
                // Handle Exception.
                return null;
            }
        }

        /// <summary>
        /// Get the list of fields for a standalone table.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <returns>IReadOnlyList<ArcGIS.Core.Data.Field></returns>
        public async Task<IReadOnlyList<ArcGIS.Core.Data.Field>> GetTableFieldsAsync(string layerPath)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerPath))
                return null;

            try
            {
                // Find the table by name if it exists. Only search existing layers.
                StandaloneTable inputTable = FindTable(layerPath);

                if (inputTable == null)
                    return null;

                IReadOnlyList<ArcGIS.Core.Data.Field> fields = null;
                List<string> fieldList = [];

                await QueuedTask.Run(() =>
                {
                    // Get the underlying table.
                    ArcGIS.Core.Data.Table table = inputTable.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        fields = tableDef.GetFields();
                    }

                    table.Dispose();
                });

                return fields;
            }
            catch
            {
                // Handle Exception.
                return null;
            }
        }

        /// <summary>
        /// Check if a field exists in a list of fields.
        /// </summary>
        /// <param name="fields"></param>
        /// <param name="fieldName"></param>
        /// <returns>IReadOnlyList<ArcGIS.Core.Data.Field></returns>
        public static bool FieldExists(IReadOnlyList<ArcGIS.Core.Data.Field> fields, string fieldName)
        {
            bool fldFound = false;

            // Check there is an input field name.
            if (String.IsNullOrEmpty(fieldName))
                return false;

            foreach (ArcGIS.Core.Data.Field fld in fields)
            {
                if (fld.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                    fld.AliasName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    fldFound = true;
                    break;
                }
            }

            return fldFound;
        }

        /// <summary>
        /// Check if a field exists in a feature class.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <param name="fieldName"></param>
        /// <returns>bool</returns>
        public async Task<bool> FieldExistsAsync(string layerPath, string fieldName)
        {
            // Check there is an input feature layer path.
            if (String.IsNullOrEmpty(layerPath))
                return false;

            // Check there is an input field name.
            if (String.IsNullOrEmpty(fieldName))
                return false;

            try
            {
                // Find the feature layer by name if it exists. Only search existing layers.
                FeatureLayer featurelayer = FindLayer(layerPath);

                if (featurelayer == null)
                    return false;

                bool fldFound = false;

                await QueuedTask.Run(() =>
                {
                    // Get the underlying feature class as a table.
                    ArcGIS.Core.Data.Table table = featurelayer.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        IReadOnlyList<ArcGIS.Core.Data.Field> fields = tableDef.GetFields();

                        // Loop through all fields looking for a name match.
                        foreach (ArcGIS.Core.Data.Field fld in fields)
                        {
                            if (fld.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                                fld.AliasName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                fldFound = true;
                                break;
                            }
                        }
                    }

                    table.Dispose();
                });

                return fldFound;
            }
            catch
            {
                // Handle Exception.
                return false;
            }
        }

        /// <summary>
        /// Check if a list of fields exists in a feature class and
        /// return a list of those that do.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="fieldNames"></param>
        /// <returns>List<string></returns>
        public async Task<List<string>> GetExistingFieldsAsync(string layerName, List<string> fieldNames)
        {
            List<string> fieldsThatExist = [];
            foreach (string fieldName in fieldNames)
            {
                if (await FieldExistsAsync(layerName, fieldName))
                    fieldsThatExist.Add(fieldName);
            }

            return fieldsThatExist;
        }

        /// <summary>
        /// Check if a field is numeric in a feature class.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="fieldName"></param>
        /// <returns>bool</returns>
        public async Task<bool> FieldIsNumericAsync(string layerName, string fieldName)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return false;

            // Check there is an input field name.
            if (String.IsNullOrEmpty(fieldName))
                return false;

            try
            {
                // Find the feature layerName by name if it exists. Only search existing layers.
                FeatureLayer featurelayer = FindLayer(layerName);

                if (featurelayer == null)
                    return false;

                IReadOnlyList<ArcGIS.Core.Data.Field> fields = null;

                bool fldIsNumeric = false;

                await QueuedTask.Run(() =>
                {
                    // Get the underlying feature class as a table.
                    ArcGIS.Core.Data.Table table = featurelayer.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        fields = tableDef.GetFields();

                        // Loop through all fields looking for a name match.
                        foreach (ArcGIS.Core.Data.Field fld in fields)
                        {
                            if (fld.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                                fld.AliasName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                fldIsNumeric = fld.FieldType switch
                                {
                                    FieldType.SmallInteger => true,
                                    FieldType.BigInteger => true,
                                    FieldType.Integer => true,
                                    FieldType.Single => true,
                                    FieldType.Double => true,
                                    _ => false,
                                };

                                break;
                            }
                        }
                    }
                });

                return fldIsNumeric;
            }
            catch
            {
                // Handle Exception.
                return false;
            }
        }

        /// <summary>
        /// Calculate the total row length for a feature class
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>bool</returns>
        public async Task<int> GetFCRowLengthAsync(string layerName)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return 0;

            try
            {
                // Find the feature layerName by name if it exists. Only search existing layers.
                FeatureLayer featurelayer = FindLayer(layerName);

                if (featurelayer == null)
                    return 0;

                IReadOnlyList<ArcGIS.Core.Data.Field> fields = null;
                List<string> fieldList = [];

                int rowLength = 1;

                await QueuedTask.Run(() =>
                {
                    // Get the underlying feature class as a table.
                    ArcGIS.Core.Data.Table table = featurelayer.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        fields = tableDef.GetFields();

                        int fldLength;

                        // Loop through all fields.
                        foreach (ArcGIS.Core.Data.Field fld in fields)
                        {
                            if (fld.FieldType == FieldType.Integer)
                                fldLength = 10;
                            else if (fld.FieldType == FieldType.Geometry)
                                fldLength = 0;
                            else
                                fldLength = fld.Length;

                            rowLength += fldLength;
                        }
                    }
                });

                return rowLength;
            }
            catch
            {
                // Handle Exception.
                return 0;
            }
        }

        /// <summary>
        /// Deletes all the fields from a feature class that are not required.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="fieldList"></param>
        /// <returns></returns>
        public async Task<bool> KeepSelectedFieldsAsync(string layerName, List<string> fieldList)
        {
            // Check the input parameters.
            if (String.IsNullOrEmpty(layerName))
                return false;

            if (fieldList == null || fieldList.Count == 0)
                return false;

            // Add a FID field so that it isn't tried to be removed.
            //fieldList.Add("FID");

            // Get the list of fields for the input table.
            IReadOnlyList<ArcGIS.Core.Data.Field> inputfields = await GetFCFieldsAsync(layerName);

            // Check a list of fields is returned.
            if (inputfields == null || inputfields.Count == 0)
                return false;

            // Get the list of field names for the input table that
            // aren't required fields (e.g. excluding FID and Shape).
            List<string> inputFieldNames = inputfields.Where(x => !x.IsRequired).Select(y => y.Name).ToList();

            // Get the list of fields that do exist in the layer.
            List<string> existingFields = await GetExistingFieldsAsync(layerName, fieldList);

            // Get the list of layer fields that aren't in the field list.
            var remainingFields = inputFieldNames.Except(existingFields).ToList();

            if (remainingFields == null || remainingFields.Count == 0)
                return true;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(layerName, remainingFields);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.DeleteField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.DeleteField", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get the full layer path name for a layer in the map (i.e.
        /// to include any parent group names.
        /// </summary>
        /// <param name="layer"></param>
        /// <returns>string</returns>
        public string GetLayerPath(Layer layer)
        {
            // Check there is an input layer.
            if (layer == null)
                return null;

            string layerPath = "";

            try
            {
                // Get the parent for the layer.
                ILayerContainer layerParent = layer.Parent;

                // Loop while the parent is a group layer.
                while (layerParent is GroupLayer)
                {
                    // Get the parent layer.
                    Layer grouplayer = (Layer)layerParent;

                    // Append the parent name to the full layer path.
                    layerPath = grouplayer.Name + "/" + layerPath;

                    // Get the parent for the layer.
                    layerParent = grouplayer.Parent;
                }
            }
            catch
            {
                // Handle Exception.
                return null;
            }

            // Append the layer name to it's full path.
            return layerPath + layer.Name;
        }

        /// <summary>
        /// Get the full layer path name for a layer name in the map (i.e.
        /// to include any parent group names.
        /// </summary>
        /// <param name="layer"></param>
        /// <returns>string</returns>
        public string GetLayerPath(string layerName)
        {
            // Check there is an input layer name.
            if (String.IsNullOrEmpty(layerName))
                return null;

            try
            {
                // Find the layer in the active map.
                FeatureLayer layer = FindLayer(layerName);

                if (layer == null)
                    return null;

                // Get the full layer path.
                return GetLayerPath(layer);
            }
            catch
            {
                // Handle Exception.
                return null;
            }
        }

        /// <summary>
        /// Returns a simplified feature class shape type for a feature layer.
        /// </summary>
        /// <param name="featureLayer"></param>
        /// <returns>string: point, line, polygon</returns>
        public string GetFeatureClassType(FeatureLayer featureLayer)
        {
            // Check there is an input feature layer.
            if (featureLayer == null)
                return null;

            try
            {
                BasicFeatureLayer basicFeatureLayer = featureLayer as BasicFeatureLayer;
                esriGeometryType shapeType = basicFeatureLayer.ShapeType;

                return shapeType switch
                {
                    esriGeometryType.esriGeometryPoint => "point",
                    esriGeometryType.esriGeometryMultipoint => "point",
                    esriGeometryType.esriGeometryPolygon => "polygon",
                    esriGeometryType.esriGeometryRing => "polygon",
                    esriGeometryType.esriGeometryLine => "line",
                    esriGeometryType.esriGeometryPolyline => "line",
                    esriGeometryType.esriGeometryCircularArc => "line",
                    esriGeometryType.esriGeometryEllipticArc => "line",
                    esriGeometryType.esriGeometryBezier3Curve => "line",
                    esriGeometryType.esriGeometryPath => "line",
                    _ => "other",
                };
            }
            catch (Exception)
            {
                // Handle the exception.
                return null;
            }
        }

        /// <summary>
        /// Returns a simplified feature class shape type for a layer name.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>string: point, line, polygon</returns>
        public string GetFeatureClassType(string layerName)
        {
            // Check there is an input feature layer name.
            if (String.IsNullOrEmpty(layerName))
                return null;

            try
            {
                // Find the layer in the active map.
                FeatureLayer layer = FindLayer(layerName);

                if (layer == null)
                    return null;

                return GetFeatureClassType(layer);
            }
            catch
            {
                // Handle Exception.
                return null;
            }
        }

        #endregion Layers

        #region Group Layers

        /// <summary>
        /// Find a group layer by name in the active map.
        /// </summary>
        /// <param name="layerName"></param>
        /// <returns>GroupLayer</returns>
        internal GroupLayer FindGroupLayer(string layerName)
        {
            // Check there is an input group layer name.
            if (String.IsNullOrEmpty(layerName))
                return null;

            try
            {
                // Finds group layers by name and returns a read only list of group layers.
                IEnumerable<GroupLayer> groupLayers = _activeMap.FindLayers(layerName).OfType<GroupLayer>();

                while (groupLayers.Any())
                {
                    // Get the first group layer found by name.
                    GroupLayer groupLayer = groupLayers.First();

                    // Check the group layer is in the active map.
                    if (groupLayer.Map.Name.Equals(_activeMap.Name, StringComparison.OrdinalIgnoreCase))
                        return groupLayer;
                }
            }
            catch
            {
                // Handle exception.
                return null;
            }

            return null;
        }

        /// <summary>
        /// Move a layer into a group layer (creating the group layer if
        /// it doesn't already exist).
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="groupLayerName"></param>
        /// <param name="position"></param>
        /// <returns>bool</returns>
        public async Task<bool> MoveToGroupLayerAsync(Layer layer, string groupLayerName, int position = -1)
        {
            // Check if there is an input layer.
            if (layer == null)
                return false;

            // Check there is an input group layer name.
            if (String.IsNullOrEmpty(groupLayerName))
                return false;

            // Does the group layer exist?
            GroupLayer groupLayer = FindGroupLayer(groupLayerName);
            if (groupLayer == null)
            {
                // Add the group layer to the map.
                try
                {
                    await QueuedTask.Run(() =>
                    {
                        groupLayer = LayerFactory.Instance.CreateGroupLayer(_activeMap, 0, groupLayerName);
                    });
                }
                catch
                {
                    // Handle Exception.
                    return false;
                }
            }

            // Move the layer into the group.
            try
            {
                await QueuedTask.Run(() =>
                {
                    // Move the layer into the group.
                    _activeMap.MoveLayer(layer, groupLayer, position);

                    // Expand the group.
                    groupLayer.SetExpanded(true);
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Remove a group layer if it is empty.
        /// </summary>
        /// <param name="groupLayerName"></param>
        /// <returns>bool</returns>
        public async Task<bool> RemoveGroupLayerAsync(string groupLayerName)
        {
            // Check there is an input group layer name.
            if (String.IsNullOrEmpty(groupLayerName))
                return false;

            try
            {
                // Does the group layer exist?
                GroupLayer groupLayer = FindGroupLayer(groupLayerName);
                if (groupLayer == null)
                    return false;

                // Count the layers in the group.
                if (groupLayer.Layers.Count != 0)
                    return true;

                await QueuedTask.Run(() =>
                {
                    // Remove the group layer.
                    _activeMap.RemoveLayer(groupLayer);
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        #endregion Group Layers

        #region Tables

        /// <summary>
        /// Find a table by name in the active map.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>StandaloneTable</returns>
        internal StandaloneTable FindTable(string tableName)
        {
            // Check there is an input table name.
            if (String.IsNullOrEmpty(tableName))
                return null;

            try
            {
                // Finds tables by name and returns a read only list of standalone tables.
                IEnumerable<StandaloneTable> tables = _activeMap.FindStandaloneTables(tableName).OfType<StandaloneTable>();

                while (tables.Any())
                {
                    // Get the first table found by name.
                    StandaloneTable table = tables.First();

                    // Check the table is in the active map.
                    if (table.Map.Name.Equals(_activeMap.Name, StringComparison.OrdinalIgnoreCase))
                        return table;
                }
            }
            catch
            {
                // Handle exception.
                return null;
            }

            return null;
        }

        /// <summary>
        /// Remove a table from the active map.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>bool</returns>
        public async Task<bool> RemoveTableAsync(string tableName)
        {
            // Check there is an input table name.
            if (String.IsNullOrEmpty(tableName))
                return false;

            try
            {
                // Find the table in the active map.
                StandaloneTable table = FindTable(tableName);

                if (table != null)
                {
                    // Remove the table.
                    await RemoveTableAsync(table);
                }

                return true;
            }
            catch
            {
                // Handle exception.
                return false;
            }
        }

        /// <summary>
        /// Remove a standalone table from the active map.
        /// </summary>
        /// <param name="table"></param>
        /// <returns>bool</returns>
        public async Task<bool> RemoveTableAsync(StandaloneTable table)
        {
            // Check there is an input table name.
            if (table == null)
                return false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Remove the table.
                    _activeMap.RemoveStandaloneTable(table);
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        #endregion Tables

        #region Symbology

        /// <summary>
        /// Apply symbology to a layer by name using a lyrx file.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="layerFile"></param>
        /// <returns>bool</returns>
        public async Task<bool> ApplySymbologyFromLayerFileAsync(string layerName, string layerFile)
        {
            // Check there is an input layer name.
            if (String.IsNullOrEmpty(layerName))
                return false;

            // Check the lyrx file exists.
            if (!FileFunctions.FileExists(layerFile))
                return false;

            // Find the layer in the active map.
            FeatureLayer featureLayer = FindLayer(layerName);

            if (featureLayer != null)
            {
                // Apply the layer file symbology to the feature layer.
                try
                {
                    await QueuedTask.Run(() =>
                    {
                        // Get the Layer Document from the lyrx file.
                        LayerDocument lyrDocFromLyrxFile = new(layerFile);

                        CIMLayerDocument cimLyrDoc = lyrDocFromLyrxFile.GetCIMLayerDocument();

                        // Get the renderer from the layer file.
                        //CIMSimpleRenderer rendererFromLayerFile = ((CIMFeatureLayer)cimLyrDoc.LayerDefinitions[0]).Renderer as CIMSimpleRenderer;
                        var rendererFromLayerFile = ((CIMFeatureLayer)cimLyrDoc.LayerDefinitions[0]).Renderer;

                        // Apply the renderer to the feature layer.
                        if (featureLayer.CanSetRenderer(rendererFromLayerFile))
                            featureLayer.SetRenderer(rendererFromLayerFile);
                    });
                }
                catch
                {
                    // Handle Exception.
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Apply a label style to a label column of a layer by name.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="labelColumn"></param>
        /// <param name="labelFont"></param>
        /// <param name="labelSize"></param>
        /// <param name="labelStyle"></param>
        /// <param name="labelRed"></param>
        /// <param name="labelGreen"></param>
        /// <param name="labelBlue"></param>
        /// <param name="allowOverlap"></param>
        /// <param name="displayLabels"></param>
        /// <returns>bool</returns>
        public async Task<bool> LabelLayerAsync(string layerName, string labelColumn, string labelFont = "Arial", double labelSize = 10, string labelStyle = "Normal",
                            int labelRed = 0, int labelGreen = 0, int labelBlue = 0, bool allowOverlap = true, bool displayLabels = true)
        {
            // Check there is an input layer.
            if (String.IsNullOrEmpty(layerName))
                return false;

            // Check there is a label columns to set.
            if (String.IsNullOrEmpty(labelColumn))
                return false;

            // Get the input feature layer.
            FeatureLayer featurelayer = FindLayer(layerName);

            if (featurelayer == null)
                return false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    CIMColor textColor = ColorFactory.Instance.CreateRGBColor(labelRed, labelGreen, labelBlue);

                    CIMTextSymbol textSymbol = SymbolFactory.Instance.ConstructTextSymbol(textColor, labelSize, labelFont, labelStyle);

                    // Get the layer definition.
                    CIMFeatureLayer lyrDefn = featurelayer.GetDefinition() as CIMFeatureLayer;

                    // Get the label classes - we need the first one.
                    var listLabelClasses = lyrDefn.LabelClasses.ToList();
                    var labelClass = listLabelClasses.FirstOrDefault();

                    // Set the label text symbol.
                    labelClass.TextSymbol.Symbol = textSymbol;

                    // Set the label expression.
                    labelClass.Expression = "$feature." + labelColumn;

                    // Check if the label engine is Maplex or standard.
                    CIMGeneralPlacementProperties labelEngine =
                       MapView.Active.Map.GetDefinition().GeneralPlacementProperties;

                    // Modify label placement (if standard label engine).
                    if (labelEngine is CIMStandardGeneralPlacementProperties) //Current labeling engine is Standard labeling engine
                        labelClass.StandardLabelPlacementProperties.AllowOverlappingLabels = allowOverlap;

                    // Set the label definition back to the layer.
                    featurelayer.SetDefinition(lyrDefn);

                    // Set the label visibilty.
                    featurelayer.SetLabelVisibility(displayLabels);
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Switch if a layers labels are visible or not.
        /// </summary>
        /// <param name="layerName"></param>
        /// <param name="displayLabels"></param>
        /// <returns>bool</returns>
        public async Task<bool> SwitchLabelsAsync(string layerName, bool displayLabels)
        {
            // Check there is an input layer.
            if (String.IsNullOrEmpty(layerName))
                return false;

            // Get the input feature layer.
            FeatureLayer featurelayer = FindLayer(layerName);

            if (featurelayer == null)
                return false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Set the label visibilty.
                    featurelayer.SetLabelVisibility(displayLabels);
                });
            }
            catch
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        #endregion Symbology

        #region Export

        /// <summary>
        /// Copy a feature class to a text fiile.
        /// </summary>
        /// <param name="inputLayer"></param>
        /// <param name="outFile"></param>
        /// <param name="columns"></param>
        /// <param name="orderByColumns"></param>
        /// <param name="separator"></param>
        /// <param name="append"></param>
        /// <param name="includeHeader"></param>
        /// <returns>int</returns>
        public async Task<int> CopyFCToTextFileAsync(string inputLayer, string outFile, string columns, string orderByColumns,
             string separator, bool append = false, bool includeHeader = true)
        {
            // Check there is an input layer name.
            if (String.IsNullOrEmpty(inputLayer))
                return -1;

            // Check there is an output table name.
            if (String.IsNullOrEmpty(outFile))
                return -1;

            // Check there are columns to output.
            if (String.IsNullOrEmpty(columns))
                return -1;

            string outColumns;
            FeatureLayer inputFeaturelayer;
            List<string> outColumnsList = [];
            List<string> orderByColumnsList = [];
            IReadOnlyList<ArcGIS.Core.Data.Field> inputfields;

            try
            {
                // Get the input feature layer.
                inputFeaturelayer = FindLayer(inputLayer);

                if (inputFeaturelayer == null)
                    return -1;

                // Get the list of fields for the input table.
                inputfields = await GetFCFieldsAsync(inputLayer);

                // Check a list of fields is returned.
                if (inputfields == null || inputfields.Count == 0)
                    return -1;

                // Align the columns with what actually exists in the layer.
                List<string> columnsList = [.. columns.Split(',')];
                outColumns = "";
                foreach (string column in columnsList)
                {
                    string columnName = column.Trim();
                    if ((columnName.Substring(0, 1) == "\"") || (FieldExists(inputfields, columnName)))
                    {
                        outColumnsList.Add(columnName);
                        outColumns = outColumns + columnName + separator;
                    }
                }
            }
            catch
            {
                // Handle Exception.
                return -1;
            }

            // Stop if there aren't any columns.
            if (outColumnsList.Count == 0 || string.IsNullOrEmpty(outColumns))
                return -1;

            // Remove the final separator.
            outColumns = outColumns[..^1];

            // Open output file.
            StreamWriter txtFile = new(outFile, append);

            // Write the header if required.
            if (!append && includeHeader)
                txtFile.WriteLine(outColumns);

            int intLineCount = 0;
            try
            {
                await QueuedTask.Run(() =>
                {
                    /// Get the feature class for the input feature layer.
                    FeatureClass featureClass = inputFeaturelayer.GetFeatureClass();

                    // Get the feature class defintion.
                    using FeatureClassDefinition featureClassDefinition = featureClass.GetDefinition();

                    // Create a row cursor.
                    RowCursor rowCursor;

                    // Create a new list of sort descriptions.
                    List<ArcGIS.Core.Data.SortDescription> sortDescriptions = [];

                    if (!string.IsNullOrEmpty(orderByColumns))
                    {
                        orderByColumnsList = [.. orderByColumns.Split(',')];

                        // Build the list of sort descriptions for each orderby column in the input layer.
                        foreach (string column in orderByColumnsList)
                        {
                            // Get the column name (ignoring any trailing ASC/DESC sort order).
                            string columnName = column.Trim();
                            if (columnName.Contains(' '))
                                columnName = columnName.Split(" ")[0].Trim();

                            // Set the sort order to ascending or descending.
                            ArcGIS.Core.Data.SortOrder sortOrder = ArcGIS.Core.Data.SortOrder.Ascending;
                            if ((column.EndsWith(" DES", true, System.Globalization.CultureInfo.CurrentCulture)) ||
                               (column.EndsWith(" DESC", true, System.Globalization.CultureInfo.CurrentCulture)))
                                sortOrder = ArcGIS.Core.Data.SortOrder.Descending;

                            // If the column is in the input table use it for sorting.
                            if ((columnName.Substring(0, 1) != "\"") && (FieldExists(inputfields, columnName)))
                            {
                                // Get the field from the feature class definition.
                                ArcGIS.Core.Data.Field field = featureClassDefinition.GetFields()
                                  .First(x => x.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                                // Create a SortDescription for the field.
                                ArcGIS.Core.Data.SortDescription sortDescription = new(field)
                                {
                                    CaseSensitivity = CaseSensitivity.Insensitive,
                                    SortOrder = sortOrder
                                };

                                // Add the SortDescription to the list.
                                sortDescriptions.Add(sortDescription);
                            }
                        }

                        // Create a TableSortDescription.
                        TableSortDescription tableSortDescription = new(sortDescriptions);

                        // Create a cursor of the sorted features.
                        rowCursor = featureClass.Sort(tableSortDescription);
                    }
                    else
                    {
                        // Create a cursor of the features.
                        rowCursor = featureClass.Search();
                    }

                    // Loop through the feature class/table using the cursor.
                    while (rowCursor.MoveNext())
                    {
                        // Get the current row.
                        using Row record = rowCursor.Current;

                        string newRow = "";
                        foreach (string column in outColumnsList)
                        {
                            string columnName = column.Trim();

                            // If the column name isn't a literal.
                            if (columnName.Substring(0, 1) != "\"")
                            {
                                // Get the field value.
                                var columnValue = record[columnName];
                                columnValue ??= "";

                                // Wrap value if quotes if it is a string that contains a comma
                                if ((columnValue is string) && (columnValue.ToString().Contains(',')))
                                    columnValue = "\"" + columnValue.ToString() + "\"";

                                // Append the column value to the new row.
                                newRow = newRow + columnValue.ToString() + separator;
                            }
                            else
                            {
                                // Append the literal to the new row.
                                newRow = newRow + columnName + separator;
                            }
                        }

                        // Remove the final separator.
                        newRow = newRow[..^1];

                        // Write the new row.
                        txtFile.WriteLine(newRow);
                        intLineCount++;
                    }
                    // Dispose of the objects.
                    featureClass.Dispose();
                    featureClassDefinition.Dispose();
                    rowCursor.Dispose();
                    rowCursor = null;
                });
            }
            catch
            {
                // Handle Exception.
                return -1;
            }
            finally
            {
                // Close the file.
                txtFile.Close();

                // Dispose of the object.
                txtFile.Dispose();
            }

            return intLineCount;
        }

        /// <summary>
        /// Copy a table to a text file.
        /// </summary>
        /// <param name="inputLayer"></param>
        /// <param name="outFile"></param>
        /// <param name="columns"></param>
        /// <param name="orderByColumns"></param>
        /// <param name="separator"></param>
        /// <param name="append"></param>
        /// <param name="includeHeader"></param>
        /// <returns>int</returns>
        public async Task<int> CopyTableToTextFileAsync(string inputLayer, string outFile, string columns, string orderByColumns,
            string separator, bool append = false, bool includeHeader = true)
        {
            // Check there is an input table name.
            if (String.IsNullOrEmpty(inputLayer))
                return -1;

            // Check there is an output table name.
            if (String.IsNullOrEmpty(outFile))
                return -1;

            // Check there are columns to output.
            if (String.IsNullOrEmpty(columns))
                return -1;

            bool missingColumns = false;
            StandaloneTable inputTable;
            List<string> columnsList = [];
            List<string> orderByColumnsList = [];
            IReadOnlyList<ArcGIS.Core.Data.Field> inputfields;

            try
            {
                // Get the input feature layer.
                inputTable = FindTable(inputLayer);

                if (inputTable == null)
                    return -1;

                // Get the list of fields for the input table.
                inputfields = await GetTableFieldsAsync(inputLayer);

                // Check a list of fields is returned.
                if (inputfields == null || inputfields.Count == 0)
                    return -1;

                // Align the columns with what actually exists in the layer.
                columnsList = [.. columns.Split(',')];
                columns = "";
                foreach (string column in columnsList)
                {
                    string columnName = column.Trim();
                    if ((columnName.Substring(0, 1) == "\"") || (FieldExists(inputfields, columnName)))
                        columns = columns + columnName + separator;
                    else
                    {
                        missingColumns = true;
                        break;
                    }
                }
            }
            catch
            {
                // Handle Exception.
                return -1;
            }

            // Stop if there are any missing columns.
            if (missingColumns || string.IsNullOrEmpty(columns))
                return -1;

            // Remove the final separator.
            columns = columns[..^1];

            // Open output file.
            StreamWriter txtFile = new(outFile, append);

            // Write the header if required.
            if (!append && includeHeader)
                txtFile.WriteLine(columns);

            int intLineCount = 0;
            try
            {
                await QueuedTask.Run(() =>
                {
                    /// Get the underlying table for the input layer.
                    ArcGIS.Core.Data.Table table = inputTable.GetTable();

                    // Get the table defintion.
                    using TableDefinition tableDefinition = table.GetDefinition();

                    // Create a row cursor.
                    RowCursor rowCursor;

                    // Create a new list of sort descriptions.
                    List<ArcGIS.Core.Data.SortDescription> sortDescriptions = [];

                    if (!string.IsNullOrEmpty(orderByColumns))
                    {
                        orderByColumnsList = [.. orderByColumns.Split(',')];

                        // Build the list of sort descriptions for each orderby column in the input layer.
                        foreach (string column in orderByColumnsList)
                        {
                            // Get the column name (ignoring any trailing ASC/DESC sort order).
                            string columnName = column.Trim();
                            if (columnName.Contains(' '))
                                columnName = columnName.Split(" ")[0].Trim();

                            // Set the sort order to ascending or descending.
                            ArcGIS.Core.Data.SortOrder sortOrder = ArcGIS.Core.Data.SortOrder.Ascending;
                            if ((column.EndsWith(" DES", true, System.Globalization.CultureInfo.CurrentCulture)) ||
                               (column.EndsWith(" DESC", true, System.Globalization.CultureInfo.CurrentCulture)))
                                sortOrder = ArcGIS.Core.Data.SortOrder.Descending;

                            // If the column is in the input table use it for sorting.
                            if ((columnName.Substring(0, 1) != "\"") && (FieldExists(inputfields, columnName)))
                            {
                                // Get the field from the feature class definition.
                                ArcGIS.Core.Data.Field field = tableDefinition.GetFields()
                                  .First(x => x.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));

                                // Create a SortDescription for the field.
                                ArcGIS.Core.Data.SortDescription sortDescription = new(field)
                                {
                                    CaseSensitivity = CaseSensitivity.Insensitive,
                                    SortOrder = sortOrder
                                };

                                // Add the SortDescription to the list.
                                sortDescriptions.Add(sortDescription);
                            }
                        }

                        // Create a TableSortDescription.
                        TableSortDescription tableSortDescription = new(sortDescriptions);

                        // Create a cursor of the sorted features.
                        rowCursor = table.Sort(tableSortDescription);
                    }
                    else
                    {
                        // Create a cursor of the features.
                        rowCursor = table.Search();
                    }

                    // Loop through the feature class/table using the cursor.
                    while (rowCursor.MoveNext())
                    {
                        // Get the current row.
                        using Row record = rowCursor.Current;

                        string newRow = "";
                        foreach (string column in columnsList)
                        {
                            string columnName = column.Trim();
                            if (columnName.Substring(0, 1) != "\"")
                            {
                                // Get the field value.
                                var columnValue = record[columnName];
                                columnValue ??= "";

                                // Wrap value if quotes if it is a string that contains a comma
                                if ((columnValue is string) && (columnValue.ToString().Contains(',')))
                                    columnValue = "\"" + columnValue.ToString() + "\"";

                                // Append the column value to the new row.
                                newRow = newRow + columnValue.ToString() + separator;
                            }
                            else
                            {
                                newRow = newRow + columnName + separator;
                            }
                        }

                        // Remove the final separator.
                        newRow = newRow[..^1];

                        // Write the new row.
                        txtFile.WriteLine(newRow);
                        intLineCount++;
                    }
                    // Dispose of the objects.
                    table.Dispose();
                    tableDefinition.Dispose();
                    rowCursor.Dispose();
                    rowCursor = null;
                });
            }
            catch
            {
                // Handle Exception.
                return -1;
            }
            finally
            {
                // Close the file.
                txtFile.Close();

                // Dispose of the object.
                txtFile.Dispose();
            }

            return intLineCount;
        }

        /// <summary>
        /// Copy a table to a text file.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outFile"></param>
        /// <param name="isSpatial"></param>
        /// <param name="append"></param>
        /// <returns>int</returns>
        public async Task<int> CopyToCSVAsync(string inTable, string outFile, bool isSpatial, bool append)
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return -1;

            // Check if there is an output file.
            if (String.IsNullOrEmpty(outFile))
                return -1;

            string separator = ",";
            return await CopyToTextFileAsync(inTable, outFile, separator, isSpatial, append);
        }

        /// <summary>
        /// Copy a table to a text file.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outFile"></param>
        /// <param name="isSpatial"></param>
        /// <param name="append"></param>
        /// <returns>int</returns>
        public async Task<int> CopyToTabAsync(string inTable, string outFile, bool isSpatial, bool append)
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return -1;

            // Check if there is an output file.
            if (String.IsNullOrEmpty(outFile))
                return -1;

            string separator = "\t";
            return await CopyToTextFileAsync(inTable, outFile, separator, isSpatial, append);
        }

        /// <summary>
        /// Copy a table to a text file.
        /// </summary>
        /// <param name="inputLayer"></param>
        /// <param name="outFile"></param>
        /// <param name="separator"></param>
        /// <param name="isSpatial"></param>
        /// <param name="append"></param>
        /// <param name="includeHeader"></param>
        /// <returns>int</returns>
        public async Task<int> CopyToTextFileAsync(string inputLayer, string outFile, string separator, bool isSpatial, bool append = false, bool includeHeader = true)
        {
            // Check there is an input table name.
            if (String.IsNullOrEmpty(inputLayer))
                return -1;

            // Check there is an output file.
            if (String.IsNullOrEmpty(outFile))
                return -1;

            string fieldName = null;
            string header = "";
            int ignoreField = -1;

            int intFieldCount;
            IReadOnlyList<ArcGIS.Core.Data.Field> fields;

            try
            {
                if (isSpatial)
                {
                    // Get the list of fields for the input table.
                    fields = await GetFCFieldsAsync(inputLayer);
                }
                else
                {
                    // Get the list of fields for the input table.
                    fields = await GetTableFieldsAsync(inputLayer);
                }

                // Check a list of fields is returned.
                if (fields == null || fields.Count == 0)
                    return -1;

                intFieldCount = fields.Count;

                // Iterate through the fields in the collection to create header
                // and flag which fields to ignore.
                for (int i = 0; i < intFieldCount; i++)
                {
                    // Get the fieldName name.
                    fieldName = fields[i].Name;

                    ArcGIS.Core.Data.Field field = fields[i];

                    // Get the fieldName type.
                    FieldType fieldType = field.FieldType;

                    string fieldTypeName = fieldType.ToString();

                    if (fieldName.Equals("sp_geometry", StringComparison.OrdinalIgnoreCase) || fieldName.Equals("shape", StringComparison.OrdinalIgnoreCase))
                        ignoreField = i;
                    else
                        header = header + fieldName + separator;
                }

                if (!append && includeHeader)
                {
                    // Remove the final separator from the header.
                    header = header.Substring(0, header.Length - 1);

                    // Write the header to the output file.
                    FileFunctions.WriteEmptyTextFile(outFile, header);
                }
            }
            catch
            {
                // Handle Exception.
                return -1;
            }

            // Open output file.
            StreamWriter txtFile = new(outFile, append);

            int intLineCount = 0;
            try
            {
                await QueuedTask.Run(() =>
                {
                    // Create a row cursor.
                    RowCursor rowCursor;

                    if (isSpatial)
                    {
                        FeatureLayer inputFC;

                        // Get the input feature layer.
                        inputFC = FindLayer(inputLayer);

                        /// Get the underlying table for the input layer.
                        using FeatureClass featureClass = inputFC.GetFeatureClass();

                        // Create a cursor of the features.
                        rowCursor = featureClass.Search();
                    }
                    else
                    {
                        StandaloneTable inputTable;

                        // Get the input table.
                        inputTable = FindTable(inputLayer);

                        /// Get the underlying table for the input layer.
                        using Table table = inputTable.GetTable();

                        // Create a cursor of the features.
                        rowCursor = table.Search();
                    }

                    // Loop through the feature class/table using the cursor.
                    while (rowCursor.MoveNext())
                    {
                        // Get the current row.
                        using Row row = rowCursor.Current;

                        // Loop through the fields.
                        string rowStr = "";
                        for (int i = 0; i < intFieldCount; i++)
                        {
                            // String the column values together (if they are not to be ignored).
                            if (i != ignoreField)
                            {
                                // Get the column value.
                                var colValue = row.GetOriginalValue(i);

                                // Wrap the value if quotes if it is a string that contains a comma
                                string colStr = null;
                                if (colValue != null)
                                {
                                    if ((colValue is string) && (colValue.ToString().Contains(',')))
                                        colStr = "\"" + colValue.ToString() + "\"";
                                    else
                                        colStr = colValue.ToString();
                                }

                                // Add the column string to the row string.
                                rowStr += colStr;

                                // Add the column separator (if not the last column).
                                if (i < intFieldCount - 1)
                                    rowStr += separator;
                            }
                        }

                        // Write the row string to the output file.
                        txtFile.WriteLine(rowStr);
                    }
                    // Dispose of the objects.
                    rowCursor.Dispose();
                    rowCursor = null;
                });
            }
            catch
            {
                // Handle Exception.
                return -1;
            }
            finally
            {
                // Close the output file and dispose of the object.
                txtFile.Close();
                txtFile.Dispose();
            }

            return intLineCount;
        }

        #endregion Export
    }

    /// <summary>
    /// This helper class provides ArcGIS Pro feature class and layer functions.
    /// </summary>
    internal static class ArcGISFunctions
    {
        #region Feature Class

        /// <summary>
        /// Check if the feature class exists in the file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> FeatureClassExistsAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
                return false;

            if (fileName.Substring(fileName.Length - 4, 1) == ".")
            {
                // It's a file.
                if (FileFunctions.FileExists(filePath + @"\" + fileName))
                    return true;
                else
                    return false;
            }
            else if (filePath.Substring(filePath.Length - 3, 3).Equals("sde", StringComparison.OrdinalIgnoreCase))
            {
                // It's an SDE class.
                // Not handled. We know the layer exists.
                return true;
            }
            else // It is a geodatabase class.
            {
                try
                {
                    return await FeatureClassExistsGDBAsync(filePath, fileName);
                }
                catch
                {
                    // GetDefinition throws an exception if the definition doesn't exist.
                    return false;
                }
            }
        }

        /// <summary>
        /// Check if the feature class exists.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>bool</returns>
        public static async Task<bool> FeatureClassExistsAsync(string fullPath)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(fullPath))
                return false;

            return await FeatureClassExistsAsync(FileFunctions.GetDirectoryName(fullPath), FileFunctions.GetFileName(fullPath));
        }

        /// <summary>
        /// Delete a feature class from a geodatabase.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteGeodatabaseFCAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
                return false;

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(filePath)));

                    // Create a SchemaBuilder object
                    SchemaBuilder schemaBuilder = new(geodatabase);

                    // Create a FeatureClassDescription object.
                    using FeatureClassDefinition featureClassDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(fileName);

                    // Create a FeatureClassDescription object
                    FeatureClassDescription featureClassDescription = new(featureClassDefinition);

                    // Add the deletion for the feature class to the list of DDL tasks
                    schemaBuilder.Delete(featureClassDescription);

                    // Execute the DDL
                    success = schemaBuilder.Build();
                });
            }
            catch (GeodatabaseNotFoundOrOpenedException)
            {
                // Handle Exception.
                return false;
            }
            catch (GeodatabaseTableException)
            {
                // Handle Exception.
                return false;
            }

            return success;
        }

        /// <summary>
        /// Delete a feature class from a geodatabase.
        /// </summary>
        /// <param name="geodatabase"></param>
        /// <param name="featureClassName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteGeodatabaseFCAsync(Geodatabase geodatabase, string featureClassName)
        {
            // Check there is an input geodatabase.
            if (geodatabase == null)
                return false;

            // Check there is an input feature class name.
            if (String.IsNullOrEmpty(featureClassName))
                return false;

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Create a SchemaBuilder object
                    SchemaBuilder schemaBuilder = new(geodatabase);

                    // Create a FeatureClassDescription object.
                    using FeatureClassDefinition featureClassDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(featureClassName);

                    // Create a FeatureClassDescription object
                    FeatureClassDescription featureClassDescription = new(featureClassDefinition);

                    // Add the deletion for the feature class to the list of DDL tasks
                    schemaBuilder.Delete(featureClassDescription);

                    // Execute the DDL
                    success = schemaBuilder.Build();
                });
            }
            catch
            {
                // Handle exception.
                return false;
            }

            return success;
        }

        /// <summary>
        /// Delete a feature class by file path and file name.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteFeatureClassAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
                return false;

            string featureClass = filePath + @"\" + fileName;

            return await DeleteFeatureClassAsync(featureClass);
        }

        /// <summary>
        /// Delete a feature class by file name.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteFeatureClassAsync(string fileName)
        {
            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(fileName);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.Delete", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.Delete", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Add a field to a feature class or table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="fieldName"></param>
        /// <param name="fieldType"></param>
        /// <param name="fieldPrecision"></param>
        /// <param name="fieldScale"></param>
        /// <param name="fieldLength"></param>
        /// <param name="fieldAlias"></param>
        /// <param name="fieldIsNullable"></param>
        /// <param name="fieldIsRequred"></param>
        /// <param name="fieldDomain"></param>
        /// <returns>bool</returns>
        public static async Task<bool> AddFieldAsync(string inTable, string fieldName, string fieldType = "TEXT",
            long fieldPrecision = -1, long fieldScale = -1, long fieldLength = -1, string fieldAlias = null,
            bool fieldIsNullable = true, bool fieldIsRequred = false, string fieldDomain = null)
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an input field name.
            if (String.IsNullOrEmpty(fieldName))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, fieldName, fieldType,
                fieldPrecision > 0 ? fieldPrecision : null, fieldScale > 0 ? fieldScale : null, fieldLength > 0 ? fieldLength : null,
                fieldAlias ?? null, fieldIsNullable ? "NULLABLE" : "NON_NULLABLE",
                fieldIsRequred ? "REQUIRED" : "NON_REQUIRED", fieldDomain);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.AddField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.AddField", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Rename a field in a feature class or table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="fieldName"></param>
        /// <param name="newFieldName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> RenameFieldAsync(string inTable, string fieldName, string newFieldName)
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an input old field name.
            if (String.IsNullOrEmpty(fieldName))
                return false;

            // Check if there is an input new field name.
            if (String.IsNullOrEmpty(newFieldName))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, fieldName, newFieldName);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.AlterField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.AlterField", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculate a field in a feature class or table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="fieldName"></param>
        /// <param name="fieldCalc"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CalculateFieldAsync(string inTable, string fieldName, string fieldCalc)
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an input field name.
            if (String.IsNullOrEmpty(fieldName))
                return false;

            // Check if there is an input field calculcation string.
            if (String.IsNullOrEmpty(fieldCalc))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, fieldName, fieldCalc);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.CalculateField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.CalculateField", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculate the geometry of a feature class.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="geometryProperty"></param>
        /// <param name="lineUnit"></param>
        /// <param name="areaUnit"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CalculateGeometryAsync(string inTable, string geometryProperty, string lineUnit = "", string areaUnit = "")
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an input geometry property.
            if (String.IsNullOrEmpty(geometryProperty))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, geometryProperty, lineUnit, areaUnit);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; //| GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("management.CalculateGeometryAttributes", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.CalculateGeometryAttributes", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Count the features in a layer.
        /// </summary>
        /// <param name="layer"></param>
        /// <returns>bool</returns>
        public static async Task<long> CountFeaturesAsync(FeatureLayer layer)
        {
            long featureCount = 0;

            // Check if there is an input layer name.
            if (layer == null)
                return featureCount;

            try
            {
                featureCount = await QueuedTask.Run(() =>
                {
                    /// Count the number of features matching the search clause.
                    FeatureClass featureClass = layer.GetFeatureClass();

                    return featureClass.GetCount();
                });
            }
            catch
            {
                // Handle Exception.
                return 0;
            }

            return featureCount;
        }

        /// <summary>
        /// Count the features in a layer using a search where clause.
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="whereClause"></param>
        /// <returns>bool</returns>
        public static async Task<long> CountFeaturesAsync(FeatureLayer layer, string whereClause)
        {
            long featureCount = 0;

            // Check if there is an input layer name.
            if (layer == null)
                return featureCount;

            try
            {
                // Create a query filter using the where clause.
                QueryFilter queryFilter = new()
                {
                    WhereClause = whereClause
                };

                featureCount = await QueuedTask.Run(() =>
                {
                    /// Count the number of features matching the search clause.
                    FeatureClass featureClass = layer.GetFeatureClass();

                    return featureClass.GetCount(queryFilter);
                });
            }
            catch
            {
                // Handle Exception.
                return 0;
            }

            return featureCount;
        }

        /// <summary>
        /// Buffer the features in a feature class with a specified distance.
        /// </summary>
        /// <param name="inFeatureClass"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="bufferDistance"></param>
        /// <param name="lineSide"></param>
        /// <param name="lineEndType"></param>
        /// <param name="dissolveOption"></param>
        /// <param name="dissolveFields"></param>
        /// <param name="method"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> BufferFeaturesAsync(string inFeatureClass, string outFeatureClass, string bufferDistance,
            string lineSide = "FULL", string lineEndType = "ROUND", string dissolveOption = "NONE", string dissolveFields = "", string method = "PLANAR", bool addToMap = false)
        {
            // Check if there is an input feature class.
            if (String.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(outFeatureClass))
                return false;

            // Check if there is an input buffer distance.
            if (String.IsNullOrEmpty(bufferDistance))
                return false;

            // Make a value array of strings to be passed to the tool.
            //List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatureClass, outFeatureClass, bufferDistance, lineSide, lineEndType, method, dissolveOption)];
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatureClass, outFeatureClass, bufferDistance, lineSide, lineEndType, dissolveOption)];
            if (!string.IsNullOrEmpty(dissolveFields))
                parameters.Add(dissolveFields);
            parameters.Add(method);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("analysis.Buffer", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.Buffer", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Clip the features in a feature class using a clip feature layer.
        /// </summary>
        /// <param name="inFeatureClass"></param>
        /// <param name="clipFeatureClass"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> ClipFeaturesAsync(string inFeatureClass, string clipFeatureClass, string outFeatureClass, bool addToMap = false)
        {
            // Check if there is an input feature class.
            if (String.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an input clip feature class.
            if (String.IsNullOrEmpty(clipFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(outFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatureClass, clipFeatureClass, outFeatureClass)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("analysis.Clip", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.Clip", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Intersect the features in a feature class with another feature class.
        /// </summary>
        /// <param name="inFeatures"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="joinAttributes"></param>
        /// <param name="outputType"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> IntersectFeaturesAsync(string inFeatures, string outFeatureClass, string joinAttributes = "ALL", string outputType = "INPUT", bool addToMap = false)
        {
            // Check if there is an input feature class.
            if (String.IsNullOrEmpty(inFeatures))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(outFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatures, outFeatureClass, joinAttributes, outputType)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("analysis.Intersect", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.Intersect", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Spatially join a feature class with another feature class.
        /// </summary>
        /// <param name="targetFeatures"></param>
        /// <param name="joinFeatures"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="joinOperation"></param>
        /// <param name="joinType"></param>
        /// <param name="fieldMapping"></param>
        /// <param name="matchOption"></param>
        /// <param name="searchRadius"></param>
        /// <param name="distanceField"></param>
        /// <param name="matchFields"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> SpatialJoinAsync(string targetFeatures, string joinFeatures, string outFeatureClass, string joinOperation = "JOIN_ONE_TO_ONE",
            string joinType = "KEEP_ALL", string fieldMapping = "", string matchOption = "INTERSECT", string searchRadius = "0", string distanceField = "",
            string matchFields = "", bool addToMap = false)
        {
            // Check if there is an input target feature class.
            if (String.IsNullOrEmpty(targetFeatures))
                return false;

            // Check if there is an input join feature class.
            if (String.IsNullOrEmpty(joinFeatures))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(outFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(targetFeatures, joinFeatures, outFeatureClass, joinOperation, joinType, fieldMapping,
                matchOption, searchRadius, distanceField, matchFields)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("analysis.SpatialJoin", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.SpatialJoin", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Permanently join fields from one feature class to another feature class.
        /// </summary>
        /// <param name="inFeatures"></param>
        /// <param name="inField"
        /// <param name="joinFeatures"></param>
        /// <param name="joinField"></param>
        /// <param name="fields"></param>
        /// <param name="fmOption"></param>
        /// <param name="fieldMapping"></param>
        /// <param name="indexJoinFields"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> JoinFieldsAsync(string inFeatures, string inField, string joinFeatures, string joinField,
            string fields = "", string fmOption = "NOT_USE_FM", string fieldMapping = "", string indexJoinFields = "NO_INDEXES",
            bool addToMap = false)
        {
            // Check if there is an input target feature class.
            if (String.IsNullOrEmpty(inFeatures))
                return false;

            // Check if there is an input field name.
            if (String.IsNullOrEmpty(inField))
                return false;

            // Check if there is a join feature class.
            if (String.IsNullOrEmpty(joinFeatures))
                return false;

            // Check if there is a join field name.
            if (String.IsNullOrEmpty(joinField))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatures, inField, joinFeatures, joinField, fields,
                fmOption, fieldMapping, indexJoinFields)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("management.JoinField", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.JoinField", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Calculate the summary statistics for a feature class or table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outTable"></param>
        /// <param name="statisticsFields"></param>
        /// <param name="caseFields"></param>
        /// <param name="concatenationSeparator"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CalculateSummaryStatisticsAsync(string inTable, string outTable, string statisticsFields,
            string caseFields = "", string concatenationSeparator = "", bool addToMap = false)
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an output table name.
            if (String.IsNullOrEmpty(outTable))
                return false;

            // Check if there is an input statistics fields string.
            if (String.IsNullOrEmpty(statisticsFields))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inTable, outTable, statisticsFields, caseFields, concatenationSeparator)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("analysis.Statistics", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.Statistics", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Convert the features in a feature class to a point feature class.
        /// </summary>
        /// <param name="inFeatureClass"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="pointLocation"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> FeatureToPointAsync(string inFeatureClass, string outFeatureClass, string pointLocation = "CENTROID", bool addToMap = false)
        {
            // Check if there is an input feature class.
            if (String.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(outFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatureClass, outFeatureClass, pointLocation)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("management.FeatureToPoint", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.FeatureToPoint", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Convert the features in a feature class to a point feature class.
        /// </summary>
        /// <param name="inFeatureClass"></param>
        /// <param name="nearFeatureClass"></param>
        /// <param name="searchRadius"></param>
        /// <param name="location"></param>
        /// <param name="angle"></param>
        /// <param name="method"></param>
        /// <param name="fieldNames"></param>
        /// <param name="distanceUnit"></param>
        /// <returns>bool</returns>
        public static async Task<bool> NearAnalysisAsync(string inFeatureClass, string nearFeatureClass, string searchRadius = "",
            string location = "NO_LOCATION", string angle = "NO_ANGLE", string method = "PLANAR", string fieldNames = "", string distanceUnit = "")
        {
            // Check if there is an input feature class.
            if (String.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(nearFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            List<string> parameters = [.. Geoprocessing.MakeValueArray(inFeatureClass, nearFeatureClass, searchRadius, location, angle, method, fieldNames, distanceUnit)];

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;

            //Geoprocessing.OpenToolDialog("analysis.Near", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("analysis.Near", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        #endregion Feature Class

        #region Geodatabase

        /// <summary>
        /// Create a new file geodatabase.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>bool</returns>
        public static Geodatabase CreateFileGeodatabase(string fullPath)
        {
            // Check if there is an input full path.
            if (string.IsNullOrEmpty(fullPath))
                return null;

            Geodatabase geodatabase;

            try
            {
                // Create a FileGeodatabaseConnectionPath with the name of the file geodatabase you wish to create
                FileGeodatabaseConnectionPath fileGeodatabaseConnectionPath = new(new Uri(fullPath));

                // Create and use the file geodatabase
                geodatabase = SchemaBuilder.CreateGeodatabase(fileGeodatabaseConnectionPath);
            }
            catch
            {
                // Handle Exception.
                return null;
            }

            return geodatabase;
        }

        /// <summary>
        /// Check if a feature class exists in a geodatabase.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> FeatureClassExistsGDBAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
                return false;

            bool exists = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(filePath)));

                    // Create a FeatureClassDefinition object.
                    using FeatureClassDefinition featureClassDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(fileName);

                    if (featureClassDefinition != null)
                        exists = true;
                });
            }
            catch (GeodatabaseNotFoundOrOpenedException)
            {
                // Handle Exception.
                return false;
            }
            catch (GeodatabaseTableException)
            {
                // Handle Exception.
                return false;
            }

            return exists;
        }

        /// <summary>
        /// Check if a layer exists in a geodatabase.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> TableExistsGDBAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
                return false;

            bool exists = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(filePath)));

                    // Create a TableDefinition object.
                    using TableDefinition tableDefinition = geodatabase.GetDefinition<TableDefinition>(fileName);

                    if (tableDefinition != null)
                        exists = true;
                });
            }
            catch (GeodatabaseNotFoundOrOpenedException)
            {
                // Handle Exception.
                return false;
            }
            catch (GeodatabaseTableException)
            {
                // Handle Exception.
                return false;
            }

            return exists;
        }

        #endregion Geodatabase

        #region Table

        /// <summary>
        /// Check if a feature class exists in the file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> TableExistsAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
                return false;

            if (fileName.Substring(fileName.Length - 4, 1) == ".")
            {
                // It's a file.
                if (FileFunctions.FileExists(filePath + @"\" + fileName))
                    return true;
                else
                    return false;
            }
            else if (filePath.Substring(filePath.Length - 3, 3).Equals("sde", StringComparison.OrdinalIgnoreCase))
            {
                // It's an SDE class
                // Not handled. We know the layer exists.
                return true;
            }
            else // it is a geodatabase class.
            {
                try
                {
                    bool exists = await TableExistsGDBAsync(filePath, fileName);

                    return exists;
                }
                catch
                {
                    // GetDefinition throws an exception if the definition doesn't exist.
                    return false;
                }
            }
        }

        /// <summary>
        /// Check if a feature class exists.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>bool</returns>
        public static async Task<bool> TableExistsAsync(string fullPath)
        {
            // Check there is an input full path.
            if (String.IsNullOrEmpty(fullPath))
                return false;

            return await TableExistsAsync(FileFunctions.GetDirectoryName(fullPath), FileFunctions.GetFileName(fullPath));
        }

        /// <summary>
        /// Check a layer exists in the file path.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static bool TableExists(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
                return false;

            if (fileName.Substring(fileName.Length - 4, 1) == ".")
            {
                // It's a file.
                if (FileFunctions.FileExists(filePath + @"\" + fileName))
                    return true;
                else
                    return false;
            }
            else if (filePath.Substring(filePath.Length - 3, 3).Equals("sde", StringComparison.OrdinalIgnoreCase))
            {
                // It's an SDE class.
                // Not handled. We know the layer exists.
                return true;
            }
            else // It is a geodatabase class.
            {
                //IWorkspaceFactory pWSF = GetWorkspaceFactory(filePath);
                //IWorkspace2 pWS = (IWorkspace2)pWSF.OpenFromFile(filePath, 0);
                //if (pWS.get_NameExists(ESRI.ArcGIS.Geodatabase.esriDatasetType.esriDTTable, tableName))
                //    return true;
                //else
                //    return false;
                return false;
            }
        }

        /// <summary>
        /// Check if a layer exists.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>bool</returns>
        public static bool TableExists(string fullPath)
        {
            // Check there is an input full path.
            if (String.IsNullOrEmpty(fullPath))
                return false;

            return TableExists(FileFunctions.GetDirectoryName(fullPath), FileFunctions.GetFileName(fullPath));
        }

        /// <summary>
        /// Delete a table from a geodatabase.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteGeodatabaseTableAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(filePath))
                return false;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
                return false;

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(filePath)));

                    // Create a SchemaBuilder object
                    SchemaBuilder schemaBuilder = new(geodatabase);

                    // Create a FeatureClassDescription object.
                    using TableDefinition tableDefinition = geodatabase.GetDefinition<TableDefinition>(fileName);

                    // Create a FeatureClassDescription object
                    TableDescription tableDescription = new(tableDefinition);

                    // Add the deletion for the feature class to the list of DDL tasks
                    schemaBuilder.Delete(tableDescription);

                    // Execute the DDL
                    success = schemaBuilder.Build();
                });
            }
            catch
            {
                return false;
            }

            return success;
        }

        /// <summary>
        /// Delete a table from a geodatabase.
        /// </summary>
        /// <param name="geodatabase"></param>
        /// <param name="tableName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> DeleteGeodatabaseTableAsync(Geodatabase geodatabase, string tableName)
        {
            // Check if the is an input geodatabase
            if (geodatabase == null)
                return false;

            // Check if there is an input table name.
            if (string.IsNullOrEmpty(tableName))
                return false;

            bool success = false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Create a SchemaBuilder object
                    SchemaBuilder schemaBuilder = new(geodatabase);

                    // Create a FeatureClassDescription object.
                    using TableDefinition tableDefinition = geodatabase.GetDefinition<TableDefinition>(tableName);

                    // Create a FeatureClassDescription object
                    TableDescription tableDescription = new(tableDefinition);

                    // Add the deletion for the feature class to the list of DDL tasks
                    schemaBuilder.Delete(tableDescription);

                    // Execute the DDL
                    success = schemaBuilder.Build();
                });
            }
            catch
            {
                return false;
            }

            return success;
        }

        #endregion Table

        #region Outputs

        /// <summary>
        /// Prompt the user to specify an output file in the required format.
        /// </summary>
        /// <param name="fileType"></param>
        /// <param name="initialDirectory"></param>
        /// <returns>string</returns>
        public static string GetOutputFileName(string fileType, string initialDirectory = @"C:\")
        {
            BrowseProjectFilter bf = fileType switch
            {
                "Geodatabase FC" => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_geodatabaseItems_featureClasses"),
                "Geodatabase Table" => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_geodatabaseItems_tables"),
                "Shapefile" => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_shapefiles"),
                "CSV file (comma delimited)" => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_textFiles_csv"),
                "Text file (tab delimited)" => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_textFiles_txt"),
                _ => BrowseProjectFilter.GetFilter("esri_browseDialogFilters_all"),
            };

            // Display the saveItemDlg in an Open Item dialog.
            SaveItemDialog saveItemDlg = new()
            {
                Title = "Save Output As...",
                InitialLocation = initialDirectory,
                //AlwaysUseInitialLocation = true,
                //Filter = ItemFilters.Files_All,
                OverwritePrompt = false,    // This will be done later.
                BrowseFilter = bf
            };

            bool? ok = saveItemDlg.ShowDialog();

            string strOutFile = null;
            if (ok.HasValue)
                strOutFile = saveItemDlg.FilePath;

            return strOutFile; // Null if user pressed exit
        }

        #endregion Outputs

        #region CopyFeatures

        /// <summary>
        /// Copy the input feature class to the output feature class.
        /// </summary>
        /// <param name="inFeatureClass"></param>
        /// <param name="outFeatureClass"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyFeaturesAsync(string inFeatureClass, string outFeatureClass, bool addToMap = false)
        {
            // Check if there is an input feature class.
            if (String.IsNullOrEmpty(inFeatureClass))
                return false;

            // Check if there is an output feature class.
            if (String.IsNullOrEmpty(outFeatureClass))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inFeatureClass, outFeatureClass);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("management.CopyFeatures", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.CopyFeatures", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Copy the input dataset name to the output feature class.
        /// </summary>
        /// <param name="inputWorkspace"></param>
        /// <param name="inputDatasetName"></param>
        /// <param name="outputFeatureClass"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyFeaturesAsync(string inputWorkspace, string inputDatasetName, string outputFeatureClass, bool addToMap = false)
        {
            // Check there is an input workspace.
            if (String.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (String.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output feature class.
            if (String.IsNullOrEmpty(outputFeatureClass))
                return false;

            string inFeatureClass = inputWorkspace + @"\" + inputDatasetName;

            return await CopyFeaturesAsync(inFeatureClass, outputFeatureClass, addToMap);
        }

        /// <summary>
        /// Copy the input dataset to the output dataset.
        /// </summary>
        /// <param name="inputWorkspace"></param>
        /// <param name="inputDatasetName"></param>
        /// <param name="outputWorkspace"></param>
        /// <param name="outputDatasetName"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyFeaturesAsync(string inputWorkspace, string inputDatasetName, string outputWorkspace, string outputDatasetName, bool addToMap = false)
        {
            // Check there is an input workspace.
            if (String.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (String.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output workspace.
            if (String.IsNullOrEmpty(outputWorkspace))
                return false;

            // Check there is an output dataset name.
            if (String.IsNullOrEmpty(outputDatasetName))
                return false;

            string inFeatureClass = inputWorkspace + @"\" + inputDatasetName;
            string outFeatureClass = outputWorkspace + @"\" + outputDatasetName;

            return await CopyFeaturesAsync(inFeatureClass, outFeatureClass, addToMap);
        }

        #endregion CopyFeatures

        #region Export Features

        /// <summary>
        /// Export the input table to the output table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outTable"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> ExportFeaturesAsync(string inTable, string outTable, bool addToMap = false)
        {
            // Check there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check there is an output table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, outTable);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("conversion.ExportTable", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("conversion.ExportTable", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        #endregion Export Features

        #region Copy Table

        /// <summary>
        /// Copy the input table to the output table.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outTable"></param>
        /// <param name="addToMap"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyTableAsync(string inTable, string outTable, bool addToMap = false)
        {
            // Check there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check there is an output table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Make a value array of strings to be passed to the tool.
            var parameters = Geoprocessing.MakeValueArray(inTable, outTable);

            // Make a value array of the environments to be passed to the tool.
            var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

            // Set the geprocessing flags.
            GPExecuteToolFlags executeFlags = GPExecuteToolFlags.GPThread; // | GPExecuteToolFlags.RefreshProjectItems;
            if (addToMap)
                executeFlags |= GPExecuteToolFlags.AddOutputsToMap;

            //Geoprocessing.OpenToolDialog("management.CopyRows", parameters);  // Useful for debugging.

            // Execute the tool.
            try
            {
                IGPResult gp_result = await Geoprocessing.ExecuteToolAsync("management.CopyRows", parameters, environments, null, null, executeFlags);

                if (gp_result.IsFailed)
                {
                    Geoprocessing.ShowMessageBox(gp_result.Messages, "GP Messages", GPMessageBoxStyle.Error);

                    var messages = gp_result.Messages;
                    var errMessages = gp_result.ErrorMessages;
                    return false;
                }
            }
            catch (Exception)
            {
                // Handle Exception.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Copy the input dataset name to the output table.
        /// </summary>
        /// <param name="inputWorkspace"></param>
        /// <param name="inputDatasetName"></param>
        /// <param name="outputTable"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyTableAsync(string inputWorkspace, string inputDatasetName, string outputTable)
        {
            // Check there is an input workspace.
            if (String.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (String.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output feature class.
            if (String.IsNullOrEmpty(outputTable))
                return false;

            string inputTable = inputWorkspace + @"\" + inputDatasetName;

            return await CopyTableAsync(inputTable, outputTable);
        }

        /// <summary>
        /// Copy the input dataset to the output dataset.
        /// </summary>
        /// <param name="inputWorkspace"></param>
        /// <param name="inputDatasetName"></param>
        /// <param name="outputWorkspace"></param>
        /// <param name="outputDatasetName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CopyTableAsync(string inputWorkspace, string inputDatasetName, string outputWorkspace, string outputDatasetName)
        {
            // Check there is an input workspace.
            if (String.IsNullOrEmpty(inputWorkspace))
                return false;

            // Check there is an input dataset name.
            if (String.IsNullOrEmpty(inputDatasetName))
                return false;

            // Check there is an output workspace.
            if (String.IsNullOrEmpty(outputWorkspace))
                return false;

            // Check there is an output dataset name.
            if (String.IsNullOrEmpty(outputDatasetName))
                return false;

            string inputTable = inputWorkspace + @"\" + inputDatasetName;
            string outputTable = outputWorkspace + @"\" + outputDatasetName;

            return await CopyTableAsync(inputTable, outputTable);
        }

        #endregion Copy Table
    }
}