using MerlinClientApi.Helpers;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
//using System.Web.Script.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Merlin.Platform.Standard.Data.Models;
using MerlinClientApi.Classes;
using Microsoft.Extensions.Logging;
using Memex.Merlin.Client;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Merlin.Mes.Model.Models;
using Merlin.Mes.Model;
using NLog;
using MerlinClientApi.Models;
using DevExpress.Xpo;

namespace MerlinClientApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class APIRelayController : ControllerBase
    {
        // ----------------------------------------------------------------------------------

        #region Contructors and Variables

        private readonly ILogger<MerlinClient> _logger;
        private readonly IOptions<MerlinClientOptions> _merlinClientOptions;
        private readonly IServiceProvider _serviceProvider;
        private static MerlinClient client;
        private static MerlinClientApiSDK merlinClientApiSDK;

        private static Logger logger = LogManager.GetCurrentClassLogger();
        //private DataTableToObjectConverter converter;
        public enum LoggingType
        {
            Error,
            Warn,
            Info
        }

        public APIRelayController(IOptions<MerlinClientOptions> merlinClientOptions, ILogger<MerlinClient> logger, IServiceProvider serviceProvider)
        {
            try
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _merlinClientOptions = merlinClientOptions ?? throw new ArgumentNullException(nameof(merlinClientOptions));
                _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

                _logger.LogInformation("Starting [MerlinClienApiSDK]...");
                var httpClientFactory = _serviceProvider.GetService<IHttpClientFactory>();
                var _client = new MerlinClient(httpClientFactory, _merlinClientOptions, _logger);
                client = _client;
                merlinClientApiSDK = new MerlinClientApiSDK(_client);

                //converter = new DataTableToObjectConverter();
            }
            catch(Exception ex)
            {
                LogError("APIRelayController", ex, ex.Message);
            }
        }

        #endregion

        // ----------------------------------------------------------------------------------

        #region Test Functions

        [Route("{id}")]
        public async Task<ActionResult<String>> GetTestData1(string id)
        {
            return id;
        }

        [Route("data/{id}")]
        public async Task<ActionResult<String>> GetTestDatas2(string id)
        {
            return id;
        }

        [HttpPost]
        [Route("postjsonbody")]
        public async Task<ActionResult<String>> GetJsonData()
        {
            using var reader = new StreamReader(HttpContext.Request.Body);
            var jsonText = await reader.ReadToEndAsync();

            dynamic data = JObject.Parse(jsonText);
            string query = (data.query != null) ? data.query : "";

            return "TEST";
        }

        //[Route("UpdateCycleTimes")]
        //public async Task<ActionResult<String>> UpdateCycleTimes()
        //{
        //    //Get List of WOs

        //    //Cycle through list

        //    //Get ProductStandard data
        //    //RODETAIL.Cycletime for WO

        //    //Update ProductStandard clycle time with RODETAIL.Cycletime 

        //    //Save  ProductStandard data

        //    return "TEST";
        //}


        //////Pass in a Query and and convert executed results to Datatable
        //[HttpPost]
        //[Route("getdatatablefromquery")]
        //public async Task<ActionResult<DataTable>> GetDataTableFromQuery([FromBody] JToken jsonbody)
        //{
        //    string jsonText = jsonbody.ToString();
        //    DataTable dt = new DataTable();
        //    string query = "";
        //    string json = "";
        //    string message = "";

        //    try
        //    {
        //        dynamic data = JObject.Parse(jsonText);
        //        query = (data.query != null) ? data.query : "";

        //        DataTableToObjectConverter converter = new DataTableToObjectConverter();
        //        dt = await converter.GetDepartmentRowDataTable(query);
        //    }
        //    catch (Exception ex)
        //    {
        //        message = ex.Message;
        //        //logger.LogError("getdatatablefromquery", ex, ex.Message, "");
        //        //return Request.CreateErrorResponse(HttpStatusCode.BadRequest, message);
        //    }

        //    return dt;
        //}

        [Route("querytest/{id}")]
        public async Task<ActionResult<String>> QueryTest(string id)
        {
            string woName = " 103453";
            string lineItemName = "20";

            string _sono = "";
            int _opno = 0;

            _sono = woName.PadLeft(8, ' ');
            int.TryParse(lineItemName, out _opno);

            string query = "UPDATE [JAM].[dbo].[DepartmentQueue] SET [in_memex] = 1 WHERE [sono] = '{0}' AND [opno] = {1} ";
            string fullQuery = string.Format(query, _sono, _opno);

            using (var uow = new UnitOfWork())
            {
                uow.ExecuteQuery(fullQuery);
            }

            return id;
        }


        #endregion

        // ----------------------------------------------------------------------------------

        #region WO Process API call

        [Route("CreateOpstep")]
        public async Task<WorkOrderExtObject> CreateOpstep(CreateOpstepObject createOpstep)
        {
            WorkOrderExtObject workOrderExt =  await merlinClientApiSDK.CreateOpstep(
                createOpstep.MachineNo
                , createOpstep.ProductId
                , createOpstep.ProductName
                , createOpstep.ProductDescription
                , createOpstep.Category
                , createOpstep.CycleTime
                , createOpstep.WoName
                , createOpstep.SalesOrder
                , createOpstep.LineItemName
                , createOpstep.NumberofParts
                , createOpstep.Location
                , createOpstep.CustomerPartNumber);

            return workOrderExt;
        }

        [Route("LoadWorkOrderIntoMachine/{woName}/{opstep}/{machineNo}")]
        public async Task<bool> LoadWorkOrderIntoMachine(string woName, int opstep, int machineNo)
        {
            await merlinClientApiSDK.LoadWorkOrderIntoMachine(woName.Trim(), opstep, machineNo);
            return true;
        }

        [Route("LoadWorkOrderIntoMachine")]
        public async Task<bool> LoadWorkOrderIntoMachine(LoadWorkOrderIntoMachineObject loadWorkOrderIntoMachine)
        {
            await merlinClientApiSDK.LoadWorkOrderIntoMachine(loadWorkOrderIntoMachine.woName.Trim(), loadWorkOrderIntoMachine.opstep, loadWorkOrderIntoMachine.machineNo);
            return true;
        }
        
        [Route("StartJob")]
        public async Task<bool> StartJob(LoadWorkOrderIntoMachineObject loadWorkOrderIntoMachine)
        {
            await merlinClientApiSDK.LoadWorkOrderIntoMachine(loadWorkOrderIntoMachine.woName, loadWorkOrderIntoMachine.opstep, loadWorkOrderIntoMachine.machineNo);
            return true;
        }

        [Route("LoginOperatorToMachine")]
        public async Task<bool> LoginOperatorToMachine(LoginOperatorToMachineObject loginOperatorToMachine)
        {
            await merlinClientApiSDK.LoginOperatorToMachine(loginOperatorToMachine.MachineNo, loginOperatorToMachine.OperatorUserId, true);
            return true;
        }
        
        [Route("LogoutOperatorFromMachine")]
        public async Task<bool> LogoutOperatorFromMachine(LoginOperatorToMachineObject logoutOperatorFromMachine)
        {
            await merlinClientApiSDK.LoginOperatorToMachine(logoutOperatorFromMachine.MachineNo, logoutOperatorFromMachine.OperatorUserId, false);
            return true;
        }

        [Route("GetCount/{machineId}")]
        public async Task<int> GetCount(string machineId)
        {            
            return await merlinClientApiSDK.GetCount(machineId);
        }

        //[Route("GetMachineCount/{machineId}")]
        //public async Task<int> GetMachineCount(string machineId)
        //{
        //    return await merlinClientApiSDK.GetMachineCountTEST(machineId);
        //}

        [Route("IsWoInMemex/{sono}/{opno}")]
        public async Task<bool> IsWoInMemex(string sono, string opno)
        {
            List<PendingOpStepsModel> result = await merlinClientApiSDK.GetOpStepsForWorkOrderAndLineItem(sono, opno);

            return (result.Count == 0) ? false : true;
        }
        [Route("IsWoInMemex")]
        public async Task<bool> IsWoInMemex(WorkOrderInfoObject workOrderInfoObject)
        {
            List<PendingOpStepsModel> result = await merlinClientApiSDK.GetOpStepsForWorkOrderAndLineItem(workOrderInfoObject.WorkOrderName, workOrderInfoObject.OpStepName);

            return (result.Count == 0) ? false : true;
        }

        [Route("AdjustGoodPartsCount/{workcenter}/{sono}/{opno}/{qty}/{reason_id}")] //"AdjustGoodPartsCount/workcenter/sono/opno/qty/reason_id/"
        public async Task<HttpResponseMessage> AdjustGoodPartsCount(string workcenter, string sono, int opno, int qty, string reason_id)
        {
            //var result = await merlinClientApiSDK.SetCount(workcenter, sono, opno, qty, reason_id);

            //return new HttpResponseMessage(System.Net.HttpStatusCode.OK);

            //return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);

            string path = "AdjustGoodPartsCount/{0}/{1}/{2}/{3}/{4}";
            string fullPath = string.Format(path, workcenter, sono, opno.ToString(), qty.ToString(), reason_id);
            Log(LoggingType.Info, "AdjustGoodPartsCount", fullPath);

            return await merlinClientApiSDK.SetCount(workcenter, sono, opno, qty, reason_id);
        }

        [Route("CheckAndCreateOpstep")]
        public async Task<WorkOrderExtObject> CheckAndCreateOpstep(CreateOpstepObject createOpstep)
        {
            string path = "CheckAndCreateOpstep/{0}/{1}/{2}/{3}/{4}/";
            string fullPath = string.Format(path, createOpstep.WoName, createOpstep.LineItemName, createOpstep.ProductName, createOpstep.MachineNo, createOpstep.CycleTime.ToString());
            Log(LoggingType.Info, "APIRelay/CheckAndCreateOpstep", fullPath);

            WorkOrderExtObject workOrderExt = new WorkOrderExtObject();

            // Check if sono+opno in Memex
            var opStepList = await merlinClientApiSDK.GetOpStepsForWorkOrderAndLineItem(createOpstep.WoName, createOpstep.LineItemName);
            if (opStepList.Count() == 0)
            {
                workOrderExt = await merlinClientApiSDK.CreateOpstep(
                    createOpstep.MachineNo
                    , createOpstep.ProductId
                    , createOpstep.ProductName
                    , createOpstep.ProductDescription
                    , createOpstep.Category
                    , createOpstep.CycleTime
                    , createOpstep.WoName.Trim()
                    , createOpstep.SalesOrder
                    , createOpstep.LineItemName
                    , createOpstep.NumberofParts
                    , createOpstep.Location
                    , createOpstep.CustomerPartNumber);
            }
            else
            {
                // get the work order by WOName
                var wo = await merlinClientApiSDK.GetWorkOrder(createOpstep.WoName.Trim());

                // get the opstep list agains the WOName and opstepName
                var opStepList3 = await merlinClientApiSDK.GetPendingOpStep(createOpstep.WoName.Trim(), opStepList.First().OpStepName);

                //// recall the product standard
                //var productStandardByName = await _client.GetProductStandardByName(prodStd.ProductName);

                //// recall the product profile by name
                //var productProfileByName = await _client.GetProductProfileByName(productProfile.ProductName);

                workOrderExt.WorkOrder = wo;
                workOrderExt.ProductStandard = new ProductStandardModel();// productStandardByName;
                workOrderExt.ProductProfile = new ProductTemplateModel(); // productProfileByName;
                workOrderExt.OpStepList = opStepList; // opStepList2;
                workOrderExt.OpStep = opStepList3;


                //Update DepartmentQueue InMemex field
                string _sono = "";
                int _opno = 0;
                _sono = createOpstep.WoName.PadLeft(8, ' ');
                int.TryParse(createOpstep.LineItemName, out _opno);

                try
                {
                    string query = "UPDATE [JAM].[dbo].[DepartmentQueue] SET [in_memex] = 1 WHERE [sono] = '{0}' AND [opno] = {1} ";
                    string fullQuery = string.Format(query, _sono, _opno);

                    using (var uow = new UnitOfWork())
                    {
                        uow.ExecuteQuery(fullQuery);
                    }
                }
                catch (Exception ex)
                {
                    LogError("CheckAndCreateOpstep - Update in_memex", ex, ex.Message);
                }
            }

            return workOrderExt;
        }

        [Route("RelayProcess")]
        public async Task<HttpResponseMessage> RelayProcess(RelayProcessObject relayProcessObject)
        {
            string path = "RelayProcess/{0}/{1}/{2}/{3}/{4}/";
            string fullPath = string.Format(path, relayProcessObject.process, relayProcessObject.input1, relayProcessObject.input2, relayProcessObject.input3, relayProcessObject.input4);
            Log(LoggingType.Info, "APIRelay/RelayProcessObject", fullPath);

            return await RelayProcess(relayProcessObject.process, relayProcessObject.input1, relayProcessObject.input2, relayProcessObject.input3, relayProcessObject.input4);
        }

        //[Route("RelayProcess/{process}/{input1}/{input2}/{input3}/{input4}")]
        public async Task<HttpResponseMessage> RelayProcess(string process, string input1, string input2, string input3, string input4)
        {
            process = process.Trim().ToUpper();
            string path = "RelayProcess/{0}/{1}/{2}/{3}/{4}/";
            string fullPath = string.Format(path, process, input1, input2, input3, input4);
            Log(LoggingType.Info, "APIRelay/RelayProcess", fullPath);

            string machineNo = "";
            string productNo = "";
            string woName = "";
            string lineItemName = "";
            string productName = "";
            string productDescription = "";
            string category = "";
            int cycleTime = 5;
            string salesOrder = "";
            int numberofParts = 0;
            string location = "";
            string customerPartNumber = "";
            string operatorUserId = "";

            int _machineNo = 0;
            int _jobState = 0;
            string _sono = "";
            int _opno = 0;
            DateTime _queuedDate = DateTime.Now;


            //bool testingMode = false;
            //if (testingMode)
            //    return new HttpResponseMessage(System.Net.HttpStatusCode.OK);

            switch (process)
            {
                case "IS WORKORDER IN MEMEX":
                    woName = input1.Trim();
                    lineItemName = input2.Trim();
                    productNo = input3;
                    productName = productNo + "-" + lineItemName;

                    Log(LoggingType.Info, "APIRelay/RelayProcess", string.Format("GetOpStepsForWorkOrderAndLineItem {0} {1}", woName, lineItemName));

                    List<PendingOpStepsModel> result = await merlinClientApiSDK.GetOpStepsForWorkOrderAndLineItem(woName, lineItemName);
                    if (result.Count == 0)
                        return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                    else
                        return new HttpResponseMessage(System.Net.HttpStatusCode.Found);
                //break;

                case "ADD WORKORDER":
                    machineNo = input1;
                    productNo = input2;
                    woName = input3.Trim();
                    lineItemName = input4.Trim();

                    _sono = woName.PadLeft(8, ' ');
                    int.TryParse(lineItemName, out _opno);

                    if (string.IsNullOrWhiteSpace(productNo))
                    {
                        string getProductQuery = string.Format("SELECT [partno] FROM [jade01].dbo.SOHEADER WHERE sono = '{0}'", _sono);

                        // get product info
                        using (var uow = new UnitOfWork())
                        {
                            DevExpress.Xpo.DB.SelectedData selectedData = await uow.ExecuteQueryAsync(getProductQuery);

                            if (selectedData.ResultSet.Length > 0 && selectedData.ResultSet[0].Rows.Count() > 0)
                            {
                                productNo = selectedData.ResultSet[0].Rows[0].Values[0].ToString().Trim();
                            }
                        }
                    }

                    Log(LoggingType.Info, "RelayProcess - ADD WORKORDER", string.Format("machineNo={0}|sono={1}|opno={2}|productNo={3}|", machineNo, _sono, _opno.ToString(), productNo));

                    productName = productNo + "-" + lineItemName;
                    productDescription = "";
                    category = "ProductStandard";
                    cycleTime = 5; // TimeSpan.FromMinutes(1),
                    salesOrder = "";
                    numberofParts = 1;
                    location = "";
                    customerPartNumber = "";

                    //CheckAndCreateOpstep
                    WorkOrderExtObject workOrderExt = new WorkOrderExtObject();

                    // Check if sono+opno in Memex
                    var opStepList = await merlinClientApiSDK.GetOpStepsForWorkOrderAndLineItem(woName, lineItemName);

                    if (opStepList is null)
                    {
                        LogError("RelayProcess - ADD WORKORDER - GetOpStepsForWorkOrderAndLineItem", null, "Error Check if sono+opno in Memex");
                        return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
                    }

                    if (opStepList.Count() == 0)
                    {
                        int.TryParse(machineNo, out _machineNo);

                        string query = "SELECT [cycletime] FROM [jade01].[dbo].[SOROUTE] WHERE [sono] = '{0}' AND [opno] = {1} ";
                        string fullQuery = string.Format(query, _sono, _opno);

                        Log(LoggingType.Info, "RelayProcess - ADD WORKORDER - Get CycleTime", fullQuery);

                        // Get cycle time in Alere SOROUTE from sono+opno
                        try
                        {
                            using (var uow = new UnitOfWork())
                            {
                                DevExpress.Xpo.DB.SelectedData selectedData = await uow.ExecuteQueryAsync(fullQuery);
                                string _cycleTimeInSeconds = "";
                                decimal cycleTimeInSeconds = 0;

                                if (selectedData.ResultSet.Length > 0)
                                {
                                    if (selectedData.ResultSet[0].Rows.Count() > 0)
                                    {
                                        _cycleTimeInSeconds = selectedData.ResultSet[0].Rows[0].Values[0].ToString().Trim();
                                        if (decimal.TryParse(_cycleTimeInSeconds, out cycleTimeInSeconds))
                                        {
                                            cycleTime = (int)(cycleTimeInSeconds / 60);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError("RelayProcess - ADD WORKORDER - Get CycleTime", ex, fullQuery);
                            return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                        }

                        workOrderExt = await merlinClientApiSDK.CreateOpstep(
                             _machineNo
                            , productName // ProductId
                            , productName
                            , productDescription
                            , category
                            , cycleTime
                            , woName.Trim()
                            , salesOrder
                            , lineItemName
                            , numberofParts
                            , location
                            , customerPartNumber);

                        opStepList = workOrderExt.OpStepList;
                    }

                    if (opStepList.Count() > 0)
                    {
                        //Update DepartmentQueue InMemex field
                        _sono = woName.PadLeft(8, ' ');
                        int.TryParse(lineItemName, out _opno);

                        try
                        {
                            string query = "UPDATE [JAM].[dbo].[DepartmentQueue] SET [in_memex] = 1 WHERE [sono] = '{0}' AND [opno] = {1} ";
                            string fullQuery = string.Format(query, _sono, _opno);

                            using (var uow = new UnitOfWork())
                            {
                                uow.ExecuteQuery(fullQuery);
                            }

                            //return reportTable;
                            return new HttpResponseMessage(System.Net.HttpStatusCode.Created);
                        }
                        catch (Exception ex)
                        {
                            LogError("RelayProcess - ADD WORKORDER - Update in_memex", ex, ex.Message);
                            return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                        }
                    }
                    else
                    {
                        return new HttpResponseMessage(System.Net.HttpStatusCode.Conflict);
                    }

                    //if (opStepList.Count() > 0)
                    //{
                    //    return new HttpResponseMessage(System.Net.HttpStatusCode.Created);
                    //}
                    //else
                    //{
                    //    return new HttpResponseMessage(System.Net.HttpStatusCode.Conflict);
                    //}

                //break;

                case "LOAD WORKORDER INTO MACHINE":
                    woName = input1.Trim();
                    lineItemName = input2.Trim();
                    machineNo = input3.Trim();

                    _sono = woName.PadLeft(8, ' ');
                    int.TryParse(lineItemName, out _opno);
                    int.TryParse(machineNo, out _machineNo);

                    //LoadWorkOrderIntoMachine
                    try
                    {
                        Log(LoggingType.Info, "APIRelay/RelayProcess", string.Format("LoadWorkOrderIntoMachine {0} {1} {2}", woName, _opno, _machineNo));

                        await merlinClientApiSDK.LoadWorkOrderIntoMachine(woName, _opno, _machineNo);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message != "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.")
                            LogError("RelayProcess - LOAD WORKORDER INTO MACHINE", ex, ex.Message);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
                    }
                //break;

                case "START JOB":
                    woName = input1.Trim();
                    lineItemName = input2.Trim();
                    machineNo = input3.Trim();

                    _sono = woName.PadLeft(8, ' ');
                    int.TryParse(lineItemName, out _opno);
                    int.TryParse(machineNo, out _machineNo);

                    //StartJob
                    try
                    {
                        Log(LoggingType.Info, "APIRelay/RelayProcess", string.Format("LoadWorkOrderIntoMachine {0} {1} {2}", woName, _opno, _machineNo));

                        await merlinClientApiSDK.LoadWorkOrderIntoMachine(woName, _opno, _machineNo);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                    }
                    catch (Exception ex)
                    {
                        LogError("RelayProcess - START JOB", ex, ex.Message);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
                    }
                //break;

                case "LOGIN OPERATOR INTO MACHINE":
                    machineNo = input1;
                    operatorUserId = input2;

                    int.TryParse(machineNo, out _machineNo);

                    //LoginOperatorToMachine
                    try
                    {
                        Log(LoggingType.Info, "APIRelay/RelayProcess", string.Format("LoginOperatorToMachine {0} {1} true", _machineNo, operatorUserId));

                        await merlinClientApiSDK.LoginOperatorToMachine(_machineNo, operatorUserId, true);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message != "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.")
                            LogError("RelayProcess - LOGIN OPERATOR INTO MACHINE", ex, ex.Message);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
                    }
                //break;

                case "LOGOUT OPERATOR FROM MACHINE":
                    machineNo = input1;
                    operatorUserId = input2;
                    int.TryParse(machineNo, out _machineNo);

                    //LogoutOperatorFromMachine
                    try
                    {
                        Log(LoggingType.Info, "APIRelay/RelayProcess", string.Format("LoginOperatorToMachine {0} {1} false", _machineNo, operatorUserId));

                        await merlinClientApiSDK.LoginOperatorToMachine(_machineNo, operatorUserId, false);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message != "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.")
                            LogError("RelayProcess - LOGOUT OPERATOR FROM MACHINE", ex, ex.Message);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
                    }
                //break;

                case "LOGOUT OPSTEP":
                    machineNo = input1;
                    string jobState = input2;
                    string queuedDate = input3;

                    /// Note: jobState is an enumerated value where
                    ///        Pending     = 0     Opstep is available for all machines.Product Standard dictate what machines this is capable on.
                    ///        Queued      = 1     Opstep has been assigned to a specific machine.  Once it has been set to "Queued", other machines that are capable of running the Opstep can't see the opstep.
                    ///        Running     = 2     This is the current running Opstep for the given machine asset.
                    ///        Completed   = 3     The Opstep is considered to be completed.
                    ///        
                    int.TryParse(machineNo, out _machineNo);
                    int.TryParse(jobState, out _jobState);
                    DateTime.TryParse(queuedDate, out _queuedDate);

                    //LogoutOpStep
                    try
                    {
                        Log(LoggingType.Info, "APIRelay/RelayProcess", string.Format("LogoutOpStep {0} {1} {2}", _machineNo, _jobState, _queuedDate.ToLongDateString()));

                        await merlinClientApiSDK.LogoutOpStep(_machineNo, _jobState, _queuedDate);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.Accepted);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message != "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.")
                            LogError("RelayProcess - LOGOUT OPSTEP", ex, ex.Message);
                        return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
                    }
                //break;

                default:
                    return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                    //break;
            }
        }

        #endregion

        // ----------------------------------------------------------------------------------

        #region Memex Functions

        [Route("GetMachineAssets")]
        public async Task<ActionResult<List<AssetModel>>> GetMachineAssets()
        {
            List<AssetModel> machines = await merlinClientApiSDK.GetMachineAssets();
            return machines;
        }

        [Route("GetOperatorAssets")]
        public async Task<ActionResult<List<AssetModel>>> GetOperatorAssets()
        {
            List<AssetModel> operators = await merlinClientApiSDK.GetOperatorAssets();
            return operators;
        }

        [Route("GetAssetById/{assetId}")]
        public async Task<AssetModel> GetAssetById(string assetId) // (Guid assetId)
        {
            return await merlinClientApiSDK.GetAssetById(new Guid(assetId));
        }

        [Route("GetAssetByTag/{assetTag}")]
        public async Task<AssetModel> GetAssetByTag(string assetTag) // assetTag
        {
            return await merlinClientApiSDK.GetAssetByTag(assetTag);
        }

        [Route("GetMachineStateAssociationsForAsset/{machineNo}")]
        //public async Task<List<MesMachineStateAssociationModel>> GetMachineStateAssociationsForAsset(Guid assetId)
        //{
        //    return await merlinClientApiSDK.GetMachineStateAssociationsForAsset(assetId);
        //}
        public async Task<List<MesMachineStateAssociationModel>> GetMachineStateAssociationsForAsset(string machineNo) // machineNo
        {
            return await merlinClientApiSDK.GetMachineStateAssociationsForAsset(machineNo);
        }

        [Route("GetRejectStateAssociationsForAsset/{machineNo}")]
        //public async Task<List<MesMachinePartRejectAssociationModel>> GetRejectStateAssociationsForAsset(Guid assetId)
        //{
        //    return await merlinClientApiSDK.GetRejectStateAssociationsForAsset(assetId);
        //}
        public async Task<List<MesMachinePartRejectAssociationModel>> GetRejectStateAssociationsForAsset(string machineNo)
        {
            return await merlinClientApiSDK.GetRejectStateAssociationsForAsset(machineNo);
        }

        [Route("ReturnMachineControlForAsset/{assetId}")]
        public async Task ReturnMachineControlForAsset(string assetId) //Guid assetId
        {
            //// get the last known machine state for the asset
            //return await merlinClientApiSDK.ReturnMachineControlForAsset(assetId);

            // get the last known machine state for the asset
            var lastMachineStateEvent = await GetLastMachineStateForMachine(assetId);
            if (lastMachineStateEvent != null && lastMachineStateEvent.Count != 0)
            {
                try
                {
                    var stateDef = ((MesMachineStateEventModel)(lastMachineStateEvent[0].EventData)).StateDefinition;
                    if (stateDef != null)
                    {
                        if (stateDef.IsModal)
                        {
                            await client.ClearModal(new Guid(assetId));
                        }
                        else
                        {
                            await client.RequestStateChange(new Guid(assetId), Guid.Empty);
                        }
                    }
                }
                catch(Exception ex)
                {
                    LogError("ReturnMachineControlForAsset", ex, ex.Message);
                }
            }
            await Task.CompletedTask;
        }

        [Route("GetLastMachineStateForMachine/{machineId}")]
        public async Task<List<EventModel>> GetLastMachineStateForMachine(string machineId) // (Guid machineId)
        {
            return await merlinClientApiSDK.GetLastMachineStateForMachine(new Guid(machineId));
        }

        [Route("SetMachineStateForAsset")]
        //public async Task SetMachineStateForAsset(Guid assetId, Guid stateId)
        public async Task SetMachineStateForAsset(AssetDataObject assetDataObject)
        {
            // check the machine state
            await merlinClientApiSDK.SetMachineStateForAsset(new Guid(assetDataObject.AssetId), new Guid(assetDataObject.StateId));
        }

        [Route("RequestPartCount/{assetId}/{count}/{isReject}/{reasonId}")]
        public async Task RequestPartCount(string assetId, int count, bool isReject, string reasonId = null)
        {
            string path = "RequestPartCount/{0}/{1}/{2}/{3}";
            string fullPath = string.Format(path, assetId, count.ToString(), isReject.ToString(), reasonId);
            Log(LoggingType.Info, "APIRelay/RequestPartCount", fullPath);

            await merlinClientApiSDK.RequestPartCount(new Guid(assetId), count, isReject, new Guid(reasonId));
        }
        [Route("RequestPartCount")]
        public async Task RequestPartCount(PartCountObject partCount)
        {
            string path = "RequestPartCount (PartCountObject)/{0}/{1}/{2}/{3}";
            string fullPath = string.Format(path, partCount.AssetId, partCount.Count.ToString(), partCount.IsReject.ToString(), partCount.ReasonId);
            Log(LoggingType.Info, "APIRelay/RequestPartCount", fullPath);

            await merlinClientApiSDK.RequestPartCount(new Guid(partCount.AssetId), partCount.Count, partCount.IsReject, new Guid(partCount.ReasonId));
        }

        [Route("LoginOperator")]
        //public async Task LoginOperator(Guid assetId, Guid operatorId)
        public async Task LoginOperator(AssetDataObject assetDataObject)
        {
            await merlinClientApiSDK.LoginOperator(new Guid(assetDataObject.AssetId), new Guid(assetDataObject.OperatorId));
        }

        [Route("LogoutOperator/{assetId}")]
        public async Task LogoutOperator(string assetId) // Guid assetId
        {
            await merlinClientApiSDK.LogoutOperator(assetId);
        }

        [Route("SaveProductStandard")]
        //public async Task SaveProductStandard(ProductStandardModel productStandardModel, string productProfileName, string productProfileDescription)
        //{
        //    await merlinClientApiSDK.SaveProductStandard(productStandardModel, productProfileName, productProfileDescription);
        //}
        public async Task SaveProductStandard(ProductStandardModel productStandardModel)
        {
            await merlinClientApiSDK.SaveProductStandard(productStandardModel, productStandardModel.ProductName, productStandardModel.ProductDescription);
        }

        [Route("UpdateProductStandard")]
        //public async Task UpdateProductStandard(ProductStandardModel productStandardModel, string productProfileName, string productProfileDescription)
        //{
        //    await merlinClientApiSDK.UpdateProductStandard(productStandardModel, productProfileName, productProfileDescription);
        //}
        public async Task UpdateProductStandard(ProductStandardModel productStandardModel)
        {
            await merlinClientApiSDK.UpdateProductStandard(productStandardModel, productStandardModel.ProductName, productStandardModel.ProductDescription);
        }

        [Route("GetProductStandardByName/{prodStdName}")]
        public async Task<ProductStandardModel> GetProductStandardByName(string prodStdName)
        {
            return await merlinClientApiSDK.GetProductStandardByName(prodStdName);
        }

        [Route("GetProductProfileByName/{prodProfName}")]
        public async Task<ProductTemplateModel> GetProductProfileByName(string prodProfName)
        {
            return await merlinClientApiSDK.GetProductProfileByName(prodProfName);
        }

        [Route("SaveWorkOrder")]
        public async Task<WorkOrderModel> SaveWorkOrder(WorkOrderModel workOrderModel)
        {
            return await merlinClientApiSDK.SaveWorkOrder(workOrderModel);
        }

        [Route("GetWorkOrderByWOName/{workOrderName}")]
        public async Task<WorkOrderModel> GetWorkOrderByWOName(String workOrderName)
        {
            return await merlinClientApiSDK.GetWorkOrderByWOName(workOrderName);
        }

        [Route("SaveWorkOrderLineItems")]
        public async Task SaveWorkOrderLineItems(IList<WorkOrderLineItemsModel> workOrderLineItemsModels)
        {
            await merlinClientApiSDK.SaveWorkOrderLineItems(workOrderLineItemsModels);
        }

        [Route("CreateMultiOpStepsFromWorkOrder/{workOrderGuidId}")]
        public async Task CreateMultiOpStepsFromWorkOrder(string workOrderGuidId)
        {
            await merlinClientApiSDK.CreateMultiOpStepsFromWorkOrder(new Guid(workOrderGuidId));
        }

        [Route("GetPendingOpStepByWorkOrderNameAndOpstepName")]
        //public async Task<PendingOpStepsModel> GetPendingOpStepByWorkOrderNameAndOpstepName(string workOrderName, string opStepName)
        public async Task<PendingOpStepsModel> GetPendingOpStepByWorkOrderNameAndOpstepName(WorkOrderInfoObject workOrderInfoObject)
        {
            return await merlinClientApiSDK.GetPendingOpStepByWorkOrderNameAndOpstepName(workOrderInfoObject.WorkOrderName, workOrderInfoObject.OpStepName);
        }

        [Route("GetOpStepsForWorkOrderAndLineItem")]
        //public async Task<List<PendingOpStepsModel>> GetOpStepsForWorkOrderAndLineItem(string workOrderName, string workOrderLineItemName)
        public async Task<List<PendingOpStepsModel>> GetOpStepsForWorkOrderAndLineItem(WorkOrderInfoObject workOrderInfoObject)
        {
            return await merlinClientApiSDK.GetOpStepsForWorkOrderAndLineItem(workOrderInfoObject.WorkOrderName, workOrderInfoObject.OpStepName);
        }

        [Route("GetOpStepsForWorkOrderLineItemById/{workOrderLineItemId}")]
        public async Task GetOpStepsForWorkOrderLineItemById(string workOrderLineItemId) // Guid workOrderLineItemId
        {
            await merlinClientApiSDK.GetOpStepsForWorkOrderLineItemById(new Guid(workOrderLineItemId));
        }

        [Route("SaveOpSteps")]
        public async Task SaveOpSteps(List<PendingOpStepsModel> opsteps)
        {
            await merlinClientApiSDK.SaveOpSteps(opsteps);
        }

        [Route("RunOpstepOnMachineAssetNow/{assetId}/{opstepId}")]
        public async Task RunOpstepOnMachineAssetNow(string assetId, string opstepId)
        {
            merlinClientApiSDK.RunOpstepOnMachineAssetNow(new Guid(assetId), new Guid(opstepId));
        }
        [Route("RunOpstepOnMachineAssetNow")]
        public async Task RunOpstepOnMachineAssetNow(AssetDataObject assetDataObject)
        {
            await merlinClientApiSDK.RunOpstepOnMachineAssetNow(new Guid(assetDataObject.AssetId), new Guid(assetDataObject.OpstepId));
        }

        [Route("LogoutOpStep")]
        //public async Task LogoutOpStep(Guid assetId, int jobState, DateTime queuedDate)
        //{
        //    await merlinClientApiSDK.LogoutOpStep(assetId, jobState, queuedDate);
        //}
        public async Task LogoutOpStep(LogoutOpStepObject logoutOpStep)
        {
            ///Logs the opstep that is currently assigned to the machine asset id.
            /// Note: jobState is an enumerated value where
            ///        Pending     = 0     Opstep is available for all machines.Product Standard dictate what machines this is capable on.
            ///        Queued      = 1     Opstep has been assigned to a specific machine.  Once it has been set to "Queued", other machines that are capable of running the Opstep can't see the opstep.
            ///        Running     = 2     This is the current running Opstep for the given machine asset.
            ///        Completed   = 3     The Opstep is considered to be completed.
            /// Note: queuedDate is the date where you would expect the Opstep to get rerun again.  The queuedDate should always be set to a valid value if JobState is either Pending or Queued.
            
            if (logoutOpStep.MachineNo > 0)
                await merlinClientApiSDK.LogoutOpStep(logoutOpStep.MachineNo, logoutOpStep.JobState, logoutOpStep.QueuedDate);
            else
                await merlinClientApiSDK.LogoutOpStep(new Guid(logoutOpStep.AssetId), logoutOpStep.JobState, logoutOpStep.QueuedDate);
        }

        [Route("GetMetricTypes")]
        public async Task<List<string>> GetMetricTypes()
        {
            return await merlinClientApiSDK.GetMetricTypes();
        }

        [Route("GetMetricGroups")]
        public async Task<List<string>> GetMetricGroups()
        {
            return await merlinClientApiSDK.GetMetricGroups();
        }

        [Route("GetFinalMetricsForRange")]
        //public async Task<List<MetricPartModel>> GetFinalMetricsForRange(Guid assetId, string metricType, string metricGroup, DateTime startDate, DateTime endDate)
        //{
        //    return await merlinClientApiSDK.GetFinalMetricsForRange(assetId, metricType, metricGroup, startDate, endDate);
        //}
        public async Task<List<MetricPartModel>> GetFinalMetricsForRange(MetricsRangeObject rebuildRangeByAssetObject)
        {
            //return await merlinClientApiSDK.GetFinalMetricsForRange(new Guid(rebuildRangeByAssetObject.AssetId), rebuildRangeByAssetObject.MetricType, rebuildRangeByAssetObject.MetricGroup, rebuildRangeByAssetObject.StartDate, rebuildRangeByAssetObject.EndDate);

            return await merlinClientApiSDK.GetFinalMetricsForRangeDirect(new Guid(rebuildRangeByAssetObject.AssetId), rebuildRangeByAssetObject.MetricType, rebuildRangeByAssetObject.MetricGroup, rebuildRangeByAssetObject.StartDate, rebuildRangeByAssetObject.EndDate);
        }

        //[Route("GetFinalMetricsForRangeForAssets")]
        //public async Task<List<MetricPartModel>> GetFinalMetricsForRangeForAssets(MetricsRequest requestData)
        //{
        //    return await merlinClientApiSDK.GetFinalMetricsForRangeForAssets(requestData);
        //}

        [Route("RebuildRangeByAssetByMetric")]
        //public async Task RebuildRangeByAssetByMetric(Guid assetId, string metricType, DateTime startDate, DateTime endDate)
        //{
        //    await merlinClientApiSDK.RebuildRangeByAssetByMetric(assetId, metricType, startDate, endDate);
        //}
        public async Task RebuildRangeByAssetByMetric(MetricsRangeObject rebuildRangeByAssetObject)
        {
            //await merlinClientApiSDK.RebuildRangeByAssetByMetric(new Guid(rebuildRangeByAssetObject.assetId), rebuildRangeByAssetObject.metricType, DateTime.Parse(rebuildRangeByAssetObject.startDate), DateTime.Parse(rebuildRangeByAssetObject.endDate));

            await merlinClientApiSDK.RebuildRangeByAssetByMetric(new Guid(rebuildRangeByAssetObject.AssetId), rebuildRangeByAssetObject.MetricType, rebuildRangeByAssetObject.StartDate, rebuildRangeByAssetObject.EndDate);
        }

        [Route("RebuildRangeByAsset")]
        //public async Task RebuildRangeByAsset(Guid assetId, DateTime startDate, DateTime endDate)
        //{
        //    await merlinClientApiSDK.RebuildRangeByAsset(assetId, startDate, endDate);
        //}
        public async Task RebuildRangeByAsset(MetricsRangeObject rebuildRangeByAssetObject)
        {
            //await merlinClientApiSDK.RebuildRangeByAsset(new Guid(rebuildRangeByAssetObject.assetId), DateTime.Parse(rebuildRangeByAssetObject.startDate), DateTime.Parse(rebuildRangeByAssetObject.endDate));

            await merlinClientApiSDK.RebuildRangeByAsset(new Guid(rebuildRangeByAssetObject.AssetId), rebuildRangeByAssetObject.StartDate, rebuildRangeByAssetObject.EndDate);
        }


        #endregion

        // ----------------------------------------------------------------------------------

        #region Error Functions

        public void LogError(String functionName, Exception ex, String errorMessage, string note = "")
        {
            try
            {
                logger.Error(ex, functionName + " error: " + errorMessage + ((string.IsNullOrWhiteSpace(note)) ? "" : Environment.NewLine + " Note: " + note));
            }
            catch (Exception excep)
            {

            }
        }
        public void Log(LoggingType loggingTYpe, String functionName, String message, string note = "")
        {
            try
            {
                if (loggingTYpe == LoggingType.Info)
                {
                    logger.Info(functionName + ": " + message + ((string.IsNullOrWhiteSpace(note)) ? "" : Environment.NewLine + " Note: " + note));
                }
                if (loggingTYpe == LoggingType.Warn)
                {
                    logger.Warn(functionName + ": " + message + ((string.IsNullOrWhiteSpace(note)) ? "" : Environment.NewLine + " Note: " + note));
                }
                if (loggingTYpe == LoggingType.Error)
                {
                    LogError(functionName, null, message, note = "");
                }
            }
            catch (Exception ex)
            {
                LogError("Log", ex, ex.Message);
            }
        }

        #endregion

        // ----------------------------------------------------------------------------------

    }
}
