using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
//using NLog;
using DevExpress.Xpo.DB;
using DevExpress.Xpo;

namespace MerlinClientApi.Helpers
{
    public class DataTableToObjectConverter
    {
        #region Variables

        //private static Logger logger = LogManager.GetCurrentClassLogger();

        public enum LoggingType
        {
            Error,
            Warn,
            Info
        }

        #endregion

        #region Converter Functions

        public async Task<DataTable> GetDepartmentRowDataTable(string query)
        {
            DataTable reportTable;
            DevExpress.Xpo.DB.SelectedData selectedData;

            try
            {
                using (var uow = new UnitOfWork())
                {
                    Log(LoggingType.Info, "GetDepartmentRowDataTable", "Executing select statement.");

                    selectedData = await uow.ExecuteQueryWithMetadataAsync(query);

                    var results = selectedData.ResultSet;

                    // build a datatable based on the result information
                    reportTable = BuildTableFromMetaData(results[0]);

                    foreach (var qrow in results[1].Rows)
                    {
                        reportTable.Rows.Add(qrow.Values);
                    }
                }

                return reportTable;
            }
            catch (Exception ex)
            {
                LogError("GetDepartmentRowDataTable", ex, ex.Message);
            }

            return null;
        }

        public static DataTable BuildTableFromMetaData(SelectStatementResult columns)
        {
            var dt = new DataTable();

            foreach (var col in columns.Rows)
            {
                var dotNetType = col.Values[2].ToString();
                var dataColumn = new DataColumn(col.Values[0].ToString());
                switch (dotNetType)
                {
                    case "String":
                        dataColumn.DataType = typeof(string);
                        dataColumn.DefaultValue = "";
                        dt.Columns.Add(dataColumn);
                        break;
                    case "Int32":
                    case "Int64":
                        dataColumn.DataType = typeof(int);
                        dataColumn.DefaultValue = 0;
                        dt.Columns.Add(dataColumn);
                        break;
                    case "Decimal":
                        dataColumn.DataType = typeof(decimal);
                        dataColumn.DefaultValue = 0m;
                        dt.Columns.Add(dataColumn);
                        break;
                    case "DateTime":
                        dataColumn.DataType = typeof(DateTime);
                        dataColumn.AllowDBNull = false;
                        dataColumn.DefaultValue = new DateTime(1900, 1, 1);
                        dt.Columns.Add(dataColumn);
                        break;
                    case "Boolean":
                        dataColumn.DataType = typeof(Boolean);
                        dataColumn.AllowDBNull = false;
                        dataColumn.DefaultValue = false;
                        dt.Columns.Add(dataColumn);
                        break;
                    default:
                        throw new Exception($"Unrecognized column type, '{dotNetType}'.");
                }
            }

            return dt;
        }

        public async Task<List<T>> GetObjectListFromDataTable<T>(DataTable dataTable, Dictionary<string, string> specialMappings)
        {
            List<T> returnList = new List<T>();
            string[] columnNames = dataTable.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray();
            string[] mappingFields = specialMappings.Keys.ToArray();

            try
            {
                foreach (DataRow dr in dataTable.Rows)
                {
                    T newObj = (T)Activator.CreateInstance(typeof(T));

                    foreach (PropertyInfo prop in newObj.GetType().GetProperties())
                    {
                        var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                        string objFieldName = prop.Name;
                        string drColName = prop.Name;

                        if (dataTable.Columns.Contains(drColName))
                        {
                            try
                            {
                                if (newObj.GetType().GetProperty(objFieldName).PropertyType == typeof(string))
                                    newObj.GetType().GetProperty(objFieldName).SetValue(newObj, dr[drColName].ToString().TrimEnd());
                                else
                                    newObj.GetType().GetProperty(objFieldName).SetValue(newObj, Convert.ChangeType(dr[drColName], type));
                            }
                            catch (Exception ex)
                            {
                                LogError("GetObjectListFromDataTable", ex, ex.Message, "newObj.GetType().GetProperty(objFieldName).SetValue(newObj, Convert.ChangeType(dr[drColName], type)");
                            }
                        }
                        else
                        {
                            // Use default value
                            if (type == typeof(DateTime))
                                newObj.GetType().GetProperty(objFieldName).SetValue(newObj, new DateTime(1900, 1, 1));
                            if (type == typeof(Decimal))
                                newObj.GetType().GetProperty(objFieldName).SetValue(newObj, 0.0M);
                            if (type == typeof(int) || type == typeof(Int16) || type == typeof(Int32) || type == typeof(Int64))
                                newObj.GetType().GetProperty(objFieldName).SetValue(newObj, 0);
                            if (type == typeof(Boolean))
                                newObj.GetType().GetProperty(objFieldName).SetValue(newObj, false);
                            if (type == typeof(string))
                                newObj.GetType().GetProperty(objFieldName).SetValue(newObj, "");
                        }
                    }

                    if (columnNames.Intersect(mappingFields).Count() > 0)
                    {
                        string maping = "";
                        foreach (string key in specialMappings.Keys)
                        {
                            maping = specialMappings[key];

                            try
                            {
                                if (dataTable.Columns.Contains(key))
                                {
                                    if (newObj.GetType().GetProperty(maping).PropertyType == typeof(string))
                                        newObj.GetType().GetProperty(maping).SetValue(newObj, dr[key].ToString().TrimEnd());
                                    else
                                        newObj.GetType().GetProperty(maping).SetValue(newObj, Convert.ChangeType(dr[key], Nullable.GetUnderlyingType(newObj.GetType().GetProperty(maping).PropertyType) ?? newObj.GetType().GetProperty(maping).PropertyType));
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError("GetObjectListFromDataTable", ex, ex.Message, "specialMappings");
                            }
                        }
                    }

                    returnList.Add((T)newObj);
                }
            }
            catch (Exception ex)
            {
                LogError("GetObjectListFromDataTable", ex, ex.Message, "");
            }

            return returnList;
        }

        //public string ConvertDataTabletoString(DataTable dt)
        //{
        //    System.Web.Script.Serialization.JavaScriptSerializer serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
        //    List<Dictionary<string, object>> rows = new List<Dictionary<string, object>>();
        //    Dictionary<string, object> row;

        //    try
        //    {
        //        foreach (DataRow dr in dt.Rows)
        //        {
        //            row = new Dictionary<string, object>();
        //            foreach (DataColumn col in dt.Columns)
        //            {
        //                row.Add(col.ColumnName, dr[col]);
        //            }
        //            rows.Add(row);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        LogError("ConvertDataTabletoString", ex, ex.Message, "");
        //    }

        //    return serializer.Serialize(rows);
        //}

        #endregion

        #region Error Functions

        public void LogError(String functionName, Exception ex, String errorMessage, string note = "")
        {
            //try
            //{
            //    logger.Error(ex, functionName + " error: " + errorMessage);
            //}
            //catch (Exception excep)
            //{

            //}
        }
        public void Log(LoggingType loggingTYpe, String functionName, String message, string note = "")
        {
            //try
            //{
            //    if (loggingTYpe == LoggingType.Info)
            //    {
            //        logger.Info(functionName + ": " + message);
            //    }
            //    if (loggingTYpe == LoggingType.Warn)
            //    {
            //        logger.Warn(functionName + ": " + message);
            //    }
            //    if (loggingTYpe == LoggingType.Error)
            //    {
            //        LogError(functionName, null, message, note = "");
            //    }
            //}
            //catch (Exception ex)
            //{
            //    LogError("Log", ex, ex.Message);
            //}
        }

        #endregion
    }
}
