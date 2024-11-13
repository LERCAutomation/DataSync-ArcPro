// The DataTools are a suite of ArcGIS Pro addins used to extract
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

using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Internal.CIM;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using DataSync.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DataTools
{
    public class SQLServerFunctions
    {
        #region Fields

        private readonly string _sdeFileName;

        #endregion Fields

        #region Constructor

        public SQLServerFunctions(string sdeFileName)
        {
            _sdeFileName = sdeFileName;

            // Open a connection to the geodatabase (don't wait it will be checked later).
            OpenGeodatabase();
        }

        #endregion Constructor

        #region Properties

        private List<String> _tableNames;

        public List<String> TableNames
        {
            get { return _tableNames; }
        }

        private Geodatabase _geodatabase = null;

        public bool GeodatabaseOpen
        {
            get { return (_geodatabase != null); }
        }

        #endregion Properties

        #region Geodatabase

        /// <summary>
        /// Open a SQL Server database using a .sde connection file to check
        /// the SDE connection works.
        /// </summary>
        /// <param name="sdeFileName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> CheckSDEConnection(string sdeFileName)
        {
            bool _sdeConnectionValid = false;

            if (!FileFunctions.FileExists(sdeFileName))
                return false;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Try and open the geodatabase using the connection file.
                    Uri sdeUri = new(sdeFileName);
                    DatabaseConnectionFile sdeDBConnFile = new(sdeUri);
                    Geodatabase geodatabase = new(sdeDBConnFile);
                    _sdeConnectionValid = true;
                }
                catch (GeodatabaseNotFoundOrOpenedException)
                {
                    // Geodatabase throws an exception.
                    _sdeConnectionValid = false;
                }
                catch (Exception)
                {
                    // Unexpected error.
                    _sdeConnectionValid = false;
                }
            });

            return _sdeConnectionValid;
        }

        /// <summary>
        /// Open a SQL Server database using a .sde connection file.
        /// </summary>
        /// <returns></returns>
        public async Task OpenGeodatabase()
        {
            _geodatabase = null;

            if (!FileFunctions.FileExists(_sdeFileName))
                return;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Try and open the geodatabase using the connection file.
                    _geodatabase = new Geodatabase(new DatabaseConnectionFile(new Uri(_sdeFileName)));
                }
                catch (GeodatabaseNotFoundOrOpenedException)
                {
                    // Geodatabase throws an exception.
                    return;
                }
                catch (Exception)
                {
                    // Unexpected error.
                    return;
                }
            });

            return;
        }

        /// <summary>
        /// Get all of the feature class and table names from the geodatabase.
        /// </summary>
        /// <returns></returns>
        public async Task GetTableNamesAsync()
        {
            _tableNames = [];

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Open the table.
                    using Table table = _geodatabase.OpenDataset<Table>("Spatial_Objects");

                    // Create a row cursor.
                    RowCursor rowCursor;

                    // Create a cursor on the table.
                    rowCursor = table.Search(null);

                    // Loop through the feature class/table using the cursor.
                    while (rowCursor.MoveNext())
                    {
                        // Get the current row.
                        using Row row = rowCursor.Current;

                        // Get the name of the table/view.
                        string tableViewName = Convert.ToString(row["ObjectName"]);

                        // Add the name of the table/view to the list.
                        _tableNames.Add(tableViewName);
                    }
                }
                catch (GeodatabaseTableException)
                {
                    // OpenDataset throws an exception if the table doesn't exist.
                    return;
                }
                catch (Exception)
                {
                    // Unexpected error.
                    return;
                }
            });

            return;
        }

        /// <summary>
        /// Get the fieldName names for the specified table in the geodatabase.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>List<string></returns>
        public async Task<List<string>> GetFieldNamesListAsync(string tableName)
        {
            List<string> fieldNames = [];

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return fieldNames;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Get the table definition.
                    using TableDefinition tableDefinition = _geodatabase.GetDefinition<TableDefinition>(tableName);

                    // Get the fields in the table and add them to the list.
                    IReadOnlyList<Field> tableFields = tableDefinition.GetFields();

                    foreach (var field in tableFields)
                    {
                        fieldNames.Add(field.Name);
                    }
                }
                catch
                {
                    // GetDefinition throws an exception if the definition doesn't exist.
                    return;
                }
            });

            return fieldNames;
        }

        /// <summary>
        /// Get the fields for the specified table in the geodatabase.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>IReadOnlyList<Field></returns>
        public async Task<IReadOnlyList<Field>> GetFieldsAsync(string fullPath)
        {
            IReadOnlyList<Field> fields = null;

            // Get the file name from the specified path.
            string fileName = FileFunctions.GetFileName(fullPath);

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return fields;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Get the table definition.
                    using TableDefinition tableDefinition = _geodatabase.GetDefinition<TableDefinition>(fileName);

                    // Get the fields in the table.
                    fields = tableDefinition.GetFields();
                }
                catch
                {
                    // GetDefinition throws an exception if the definition doesn't exist.
                    return;
                }
            });

            return fields;
        }

        /// <summary>
        /// Get the fields for the specified table in the geodatabase.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="fileName"></param>
        /// <returns>IReadOnlyList<Field></returns>
        public static async Task<IReadOnlyList<Field>> GetFieldNamesAsync(string filePath, string fileName)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(filePath))
                return null;

            // Check there is an input file name.
            if (String.IsNullOrEmpty(fileName))
                return null;

            IReadOnlyList<Field> fields = null;

            try
            {
                // Open the SQLServer geodatabase via the .sde connection file.
                await QueuedTask.Run(() =>
                {
                    // Open the file geodatabase. This will open the geodatabase if the folder exists and contains a valid geodatabase.
                    using Geodatabase geodatabase = new(new FileGeodatabaseConnectionPath(new Uri(filePath)));

                    // Get the feature class definition.
                    using FeatureClassDefinition featureClassDefinition = geodatabase.GetDefinition<FeatureClassDefinition>(fileName);
                    //using TableDefinition tableDefinition = geodatabase.GetDefinition<TableDefinition>(fileName);

                    // Get the fields in the table.
                    IReadOnlyList<Field> tableFields = featureClassDefinition.GetFields();
                });
            }
            catch
            {
                // GetDefinition throws an exception if the definition doesn't exist.
                return null;
            }

            return fields;
        }

        /// <summary>
        /// Get the fields for the specified table in the geodatabase.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns>IReadOnlyList<Field></returns>
        public static async Task<IReadOnlyList<Field>> GetFieldNamesAsync(string fullPath)
        {
            // Check there is an input file path.
            if (String.IsNullOrEmpty(fullPath))
                return null;

            return await GetFieldNamesAsync(FileFunctions.GetDirectoryName(fullPath), FileFunctions.GetFileName(fullPath));
        }

        /// <summary>
        /// Check if a field exists in a geodatbase.
        /// </summary>
        /// <param name="fullPath"></param>
        /// <param name="fieldName"></param>
        /// <returns>bool</returns>
        public static async Task<bool> FieldExistsAsync(string fullPath, string fieldName)
        {
            // Get the file name from the specified path.
            string fileName = FileFunctions.GetFileName(fullPath);

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return fields;

            bool fldFound = false;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Get the table definition.
                    using TableDefinition tableDefinition = _geodatabase.GetDefinition<TableDefinition>(fileName);

                    if (tableDefinition == null)
                        return;

                    Field field = tableDefinition.GetFields().First(x => x.Name.Equals(fieldName) || x.AliasName.Equals(fieldName));

                    if (field != null)
                        fldFound = true;
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
            });

            return fldFound;
        }

        /// <summary>
        /// Get all of the table sync results from the geodatabase.
        /// </summary>
        /// <returns></returns>
        public async Task<List<ResultDetail>> GetSyncResultsAsync(string resultsTable, string resultTypeColumn, string newRefColumn, string oldRefColumn, string newAreaColumn, string oldAreaColumn)
        {
            // Check there is an input remote table name.
            if (String.IsNullOrEmpty(resultsTable))
                return null;

            List<ResultDetail> resultDetailList = [];

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return null;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Open the table.
                    using Table table = _geodatabase.OpenDataset<Table>(resultsTable);

                    // Create a new list of sort descriptions.
                    List<ArcGIS.Core.Data.SortDescription> sortDescriptions = [];

                    // Create a query filter using the partner clause.
                    ArcGIS.Core.Data.QueryFilter queryFilter = new()
                    {
                        PostfixClause = "ORDER BY " + resultTypeColumn + "," + newRefColumn
                    };

                    // Create a cursor on the table.
                    using RowCursor rowCursor = table.Search(null, false);

                    // Loop through the feature class/table using the cursor.
                    while (rowCursor.MoveNext())
                    {
                        // Get the current row.
                        using Row row = rowCursor.Current;

                        // Create a new result detail item for this row.
                        ResultDetail resultDetail = new()
                        {
                            ResultType = Convert.ToString(row[resultTypeColumn]),
                            NewRef = Convert.ToString(row[newRefColumn]),
                            OldRef = Convert.ToString(row[oldRefColumn]),
                            NewArea = Convert.ToString(row[newAreaColumn]) == "0" ? "" : Convert.ToString(row[newAreaColumn]),
                            OldArea = Convert.ToString(row[oldAreaColumn]) == "0" ? "" : Convert.ToString(row[oldAreaColumn])
                        };

                        // Add the result details to the list.
                        resultDetailList.Add(resultDetail);
                    }

                    // Dispose of the objects.
                    rowCursor.Dispose();
                }
                catch (GeodatabaseTableException)
                {
                    // OpenDataset throws an exception if the table doesn't exist.
                    return;
                }
                catch (Exception)
                {
                    // Unexpected error.
                    return;
                }
            });

            return resultDetailList;
        }

        #endregion Geodatabase

        #region Execute SQL

        /// <summary>
        /// Execute a raw SQL statement on the underlying database management system.
        /// Any SQL is permitted (DDL or DML), but no results can be returned.
        /// </summary>
        /// <param name="sqlStatement"></param>
        /// <returns>bool</returns>
        public async Task<bool> ExecuteSQLOnGeodatabase(string sqlStatement)
        {
            // Check there is a sql statement.
            if (String.IsNullOrEmpty(sqlStatement))
                return false;

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return false;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Try and execute the SQL statement.
                    DatabaseClient.ExecuteStatement(_geodatabase, sqlStatement);
                }
                catch (Exception)
                {
                    // ExecuteStatement throws an exception.
                    return;
                }
            }, TaskCreationOptions.LongRunning);

            return true;
        }

        #endregion Execute SQL

        #region Text Files

        /// <summary>
        /// Copy a table to a text file.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outFile"></param>
        /// <param name="isSpatial"></param>
        /// <param name="append"></param>
        /// <returns>bool</returns>
        public async Task<bool> CopyToCSVAsync(string inTable, string outFile, bool isSpatial, bool append)
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an output file.
            if (String.IsNullOrEmpty(outFile))
                return false;

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
        /// <returns>bool</returns>
        public async Task<bool> CopyToTabAsync(string inTable, string outFile, bool isSpatial, bool append)
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an output file.
            if (String.IsNullOrEmpty(outFile))
                return false;

            string separator = "\t";
            return await CopyToTextFileAsync(inTable, outFile, separator, isSpatial, append);
        }

        /// <summary>
        /// Copy a table to a text file.
        /// </summary>
        /// <param name="inTable"></param>
        /// <param name="outFile"></param>
        /// <param name="separator"></param>
        /// <param name="isSpatial"></param>
        /// <param name="append"></param>
        /// <param name="includeHeader"></param>
        /// <returns>bool</returns>
        public async Task<bool> CopyToTextFileAsync(string inTable, string outFile, string separator, bool isSpatial, bool append, bool includeHeader = true)
        {
            // Check if there is an input table name.
            if (String.IsNullOrEmpty(inTable))
                return false;

            // Check if there is an output file.
            if (String.IsNullOrEmpty(outFile))
                return false;

            string filePath = FileFunctions.GetDirectoryName(inTable);
            string fileName = FileFunctions.GetFileName(inTable);

            string fieldName = null;
            string header = "";
            int ignoreField = -1;

            // Get the list of fields for the input table.
            IReadOnlyList<Field> fields = await GetFieldsAsync(inTable);

            // Check a list of fields is returned.
            if (fields == null || fields.Count == 0)
                return false;

            int intFieldCount = fields.Count;

            // Iterate through the fields in the collection to create header
            // and flag which fields to ignore.
            for (int i = 0; i < intFieldCount; i++)
            {
                // Get the fieldName name.
                fieldName = fields[i].Name;

                Field field = fields[i];

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

            // Open output file.
            StreamWriter txtFile = new(outFile, true);

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return false;

            try
            {
                await QueuedTask.Run(() =>
                {
                    // Create a row cursor.
                    RowCursor rowCursor;

                    if (isSpatial)
                    {
                        // Open the feature class.
                        using FeatureClass featureClass = _geodatabase.OpenDataset<FeatureClass>(fileName);

                        // Create a cursor on the table.
                        rowCursor = featureClass.Search(null);
                    }
                    else
                    {
                        // Open the table.
                        using Table table = _geodatabase.OpenDataset<Table>(fileName);

                        // Create a cursor on the table.
                        rowCursor = table.Search(null);
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
                });
            }
            catch (Exception)
            {
                // logger.Error(exception.Message);
                return false;
            }

            // Close the output file and dispose of the object.
            txtFile.Close();
            txtFile.Dispose();

            return true;
        }

        #endregion Text Files

        #region FeatureClasses

        /// <summary>
        /// Check if a feature class exists in the database.
        /// </summary>
        /// <param name="featureClassName"></param>
        /// <returns>bool</returns>
        public async Task<bool> FeatureClassExistsAsync(string featureClassName)
        {
            // Check there is an input feature class name.
            if (String.IsNullOrEmpty(featureClassName))
                return false;

            bool exists = false;

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return exists;

            // Check to see if the feature class is a feature class.
            await QueuedTask.Run(() =>
            {
                try
                {
                    // Try and get the feature class definition.
                    using FeatureClassDefinition featureClassDefinition = _geodatabase.GetDefinition<FeatureClassDefinition>(featureClassName);

                    exists = true;

                    // Dispose of the feature class definition.
                    //featureClassDefinition.Dispose();
                }
                catch
                {
                    // GetDefinition throws an exception if the definition doesn't exist.
                    exists = false;
                }
            });

            return exists;
        }

        /// <summary>
        /// Count the rows in a feature class within the database.
        /// </summary>
        /// <param name="featureClassName"></param>
        /// <returns></returns>
        public async Task<long> FeatureClassCountRowsAsync(string featureClassName)
        {
            // Check there is an input feature class name.
            if (String.IsNullOrEmpty(featureClassName))
                return -1;

            long rows = 0;

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return rows;

            // Check to see if the table is a feature class.
            await QueuedTask.Run(() =>
            {
                try
                {
                    // Try and get the feature class definition.
                    using FeatureClassDefinition featureClassDefinition = _geodatabase.GetDefinition<FeatureClassDefinition>(featureClassName);

                    // Open the feature class.
                    using FeatureClass featureClass = _geodatabase.OpenDataset<FeatureClass>(featureClassName);

                    // Count the rows in the feature class.
                    rows = featureClass.GetCount();
                }
                catch
                {
                    // GetDefinition or open dataset throw an exception if the definition doesn't exist.
                    rows = -1;
                }
            });

            return rows;
        }

        #endregion FeatureClasses

        #region Tables

        /// <summary>
        /// Check if a table exists in the database.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>bool</returns>
        public async Task<bool> TableExistsAsync(string tableName)
        {
            // Check there is an input table name.
            if (String.IsNullOrEmpty(tableName))
                return false;

            bool exists = false;

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return exists;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Try and get the table definition.
                    using TableDefinition tableDefinition = _geodatabase.GetDefinition<TableDefinition>(tableName);

                    exists = true;

                    // Dispose of the table definition.
                    //tableDefinition.Dispose();
                }
                catch
                {
                    // GetDefinition throws an exception if the definition doesn't exist.
                    exists = false;
                }
            });

            return exists;
        }

        /// <summary>
        /// Count the rows in a table within the database.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>long</returns>
        public async Task<long> TableCountRowsAsync(string tableName)
        {
            // Check there is an input table name.
            if (String.IsNullOrEmpty(tableName))
                return -1;

            long rows = 0;

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return rows;

            // Check to see if the table is a table.
            await QueuedTask.Run(() =>
            {
                try
                {
                    // Try and get the table definition.
                    using TableDefinition tableDefinition = _geodatabase.GetDefinition<TableDefinition>(tableName);

                    // Open the table.
                    using Table table = _geodatabase.OpenDataset<Table>(tableName);

                    // Count the rows in the table.
                    rows = table.GetCount();
                }
                catch
                {
                    // GetDefinition or open dataset throw an exception if the definition doesn't exist.
                    rows = -1;
                }
            });

            return rows;
        }

        /// <summary>
        /// Calculate the total row length in a table within the database.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>bool</returns>
        public async Task<int> TableRowLength(string tableName)
        {
            // Check there is an input table name.
            if (String.IsNullOrEmpty(tableName))
                return -1;

            int rowLength = 0;

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return -1;

            try
            {
                // Open the SQLServer geodatabase via the .sde connection file.
                await QueuedTask.Run(() =>
                {
                    // Try and get the table definition.
                    using TableDefinition tableDefinition = _geodatabase.GetDefinition<TableDefinition>(tableName);

                    // Get the fields in the table.
                    IReadOnlyList<Field> tableFields = tableDefinition.GetFields();

                    int fldLength;

                    // Loop through all fields.
                    foreach (Field fld in tableFields)
                    {
                        if (fld.FieldType == FieldType.Geometry)
                            fldLength = 0;
                        else
                            fldLength = fld.Length;

                        rowLength += fldLength;
                    }
                });
            }
            catch
            {
                // Handle Exception.
                return -1;
            }

            return rowLength;
        }

        /// <summary>
        /// Check if a table exists in the database.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns>bool</returns>
        public async Task<bool> DeleteTableAsync(string tableName)
        {
            // Check there is an input table name.
            if (String.IsNullOrEmpty(tableName))
                return false;

            bool success = false;

            // Open a connection to the geodatabase if not already open.
            if (!GeodatabaseOpen) await OpenGeodatabase();

            // If still not open.
            if (!GeodatabaseOpen) return success;

            await QueuedTask.Run(() =>
            {
                try
                {
                    // Create a SchemaBuilder object
                    SchemaBuilder schemaBuilder = new(_geodatabase);

                    // Create a TableDefinition object.
                    using TableDefinition tableDefinition = _geodatabase.GetDefinition<TableDefinition>(tableName);

                    // Create a TableDescription object
                    TableDescription tableDescription = new(tableDefinition);

                    // Add the deletion for the table to the list of DDL tasks
                    schemaBuilder.Delete(tableDescription);

                    // Execute the DDL
                    success = schemaBuilder.Build();
                }
                catch
                {
                    success = false;
                }
            });

            return success;
        }

        #endregion Tables
    }
}