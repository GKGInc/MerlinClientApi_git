using Memex.Merlin.Client;
using Merlin.Mes.Engine.Metrics;
using Merlin.Mes.Model.Models;
using Merlin.Mes.Model;
using Merlin.Platform.Common;
using Merlin.Platform.Standard.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using MerlinClientApi.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DevExpress.Xpo;

namespace MerlinClientApi.Classes
{
    public class MerlinClientApiSDK
    {
        // ----------------------------------------------------------------------------------

        #region Contructors and Variables

        private MerlinClient _client;
        private HttpClient _httpClient;
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public MerlinClientApiSDK(MerlinClient client)
        {
            _client = client;
        }

        public enum LoggingType
        {
            Error,
            Warn,
            Info
        }

        public HttpClient GetHttpClient()
        {
            if (_httpClient is null)
            {
                var configuration = new ConfigurationBuilder()
                  .SetBasePath(Directory.GetCurrentDirectory())
                  .AddJsonFile("appsettings.json", optional: false)
                  .AddEnvironmentVariables()
                  .Build();
                IConfigurationSection config = configuration.GetSection("MerlinClientOptions");

                string baseAddress = config.GetValue<string>("BaseAddress");                //http://jep-merlin.jadeplastics.local
                string bearerToken = config.GetValue<string>("BearerToken");                //"65DBC16AFD5B0C8668B161944E17E1E3346408DF4CB6CBBF30B9A5A97D61D2F3"
                int numberOfRetryAttempts = config.GetValue<int>("NumberOfRetryAttempts");  //"10"

                _httpClient = new HttpClient();
                //_httpClient.BaseAddress = new Uri("http://jep-merlin.jadeplastics.local");
                _httpClient.BaseAddress = new Uri(baseAddress);
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //_httpClient.DefaultRequestHeaders.Add("BearerToken", bearerToken);
                _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + bearerToken);
                _httpClient.DefaultRequestHeaders.Add("NumberOfRetryAttempts", numberOfRetryAttempts.ToString());
            }

            return _httpClient;
        }

        #endregion

        // ----------------------------------------------------------------------------------

        #region Process calls

        public async Task<WorkOrderExtObject> CreateOpstep(
            int machineNo
            , string productId
            , string productName
            , string productDescription
            , string category
            , int cycleTime
            , string woName
            , string salesOrder
            , string lineItemName // opstep
            , int numberofParts
            , string location
            , string customerPartNumber
               )
        {
            string message = "";
            string errorMessage = "";

            string path = "machineNo={0}|productName={1}|woName={2}|lineItemName={3}|cycleTime={4}";
            string fullPath = string.Format(path, machineNo, productName, woName, lineItemName, cycleTime.ToString());
            Log(LoggingType.Info, "CreateOpstep", fullPath);

            //// --------------------------------------------------------------------------------------------- 
            //// Create an Opstep
            ////  The creation of an opstep requires the following steps:
            ////      1. SaveProductStandard
            ////          a. Add any machine overrides necessary for that product standard.
            ////      2. SaveProductTemplate
            ////      3. Associate ProductTemplate to Product Standard
            ////      4. SaveWorkOrder
            ////      5. SaveWorkOrderLineItems
            ////      6. CreateMultiOpStepsFromWorkOrder
            ////          a. SaveOpSteps can be called indendently to update specific information against the opstep in case fiels like the OpStepName differs from the default OpstepName provided which is usually the product standard name
            ////  The steps will be outlined below with the corresponding API calls.

            // ---------------------------------------------------------------------------------------------

            WorkOrderExtObject workOrderExt = new WorkOrderExtObject();

            //NOTE: By default the Opstep name is generated value that references WorkOrderModel, WorkOrderLineItem, Product Standard, and ProductProfile.
            //  For example: 
            //      WorkOrderModel.Name = “Test Work Order”
            //      WorkOrderLinItem.Name = “Test Line Item”
            //      TemplateSteps.Name = "Test Product"
            //      ProductProfile.Name = "Test Product Template"
            //
            //  The resulting name is: “Test Work Order - Test Line Item-Test Product”
            //
            //  If the format for the automatic naming convention setup for the opstep is insufficient for your use, then you would need to resave the Opstep with appropriate opstep name.

            try
            {
                // Recall the list of Machines
                var machines = await GetMachineAssets();
                //var operators = await GetOperatorAssets();

                Guid machineAssetId = Guid.NewGuid();
                machineAssetId = machines.Find(i => i.AssetTag == machineNo.ToString()).Id;

                //NOTE:
                //[Tempus].[dbo].[ProductStandards].[SetUpTime] = Set Up Time
                //[Tempus].[dbo].[ProductStandards].[CycleTime] = Machine Cut Time
                //[Tempus].[dbo].[ProductStandards].[MaterialConveyanceTime] = Material Conveyance Time
                //[CycleTime] + [MaterialConveyanceTime]        = Cycle Time

                ////      1. SaveProductStandard
                var prodStd = new ProductStandardModel()
                {
                    FarmId = Guid.Empty,
                    Id = Guid.NewGuid(),
                    AnyMachine = true,
                    SetUpTime = TimeSpan.FromMinutes(1),                //  TimeSpan.FromSeconds(SOROUTE.[setuptime])
                    CycleTime = TimeSpan.FromMinutes(1),                //
                    MachineCutTime = TimeSpan.FromMinutes(cycleTime),   //  TimeSpan.FromSeconds(SOROUTE.[cycletime])
                    MaterialConveyanceTime = TimeSpan.FromMinutes(1),   //
                    AcceptedCycleTime = TimeSpan.FromMinutes(1),
                    CyclePerParts = 1,                                  //
                    PartsPerCycle = 1,                                  //
                    ProductId = productId,
                    ProductName = productName,
                    ProductDescription = productDescription,
                    Category = category,
                    ProductStandardMachineOverrides = new List<ProductStandardMachineOverridesModel>()
                };

                if (machines.Count != 0)
                {
                    // add the specific machines that this product standard is allowed to run on.
                    // for this example, we will use the first machine as the asset to add.
                    // The machine override should start off as a copy of the original product standard.
                    var prodStdMachOverridForAsset = new ProductStandardMachineOverridesModel()
                    {
                        FarmId = Guid.Empty,
                        Id = Guid.NewGuid(),
                        AssetId = machineAssetId, // machines[0].Id,
                        AcceptedCycleTime = prodStd.AcceptedCycleTime,
                        CyclePerParts = prodStd.CyclePerParts,
                        CycleTime = prodStd.CycleTime,
                        MachineCutTime = prodStd.MachineCutTime,
                        MaterialConveyanceTime = prodStd.MaterialConveyanceTime,
                        PartsPerCycle = prodStd.PartsPerCycle,
                        ProductId = prodStd.ProductId,
                        ProductName = prodStd.ProductName,
                        ProductDescription = prodStd.ProductDescription,
                        Category = prodStd.Category,
                        SetUpTime = prodStd.SetUpTime,
                        ProductStandardId = prodStd.Id

                    };
                    prodStd.ProductStandardMachineOverrides.Add(prodStdMachOverridForAsset);
                }
                //

                await SaveProductStandard(prodStd, "", "");

                // recall the product standard
                var productStandardByName = await _client.GetProductStandardByName(prodStd.ProductName);

                // Note Check to see if i can add the machine overrides as well to the product standard using this call.

                //      2. SaveProductTemplate
                var productProfile = new ProductTemplateModel()
                {
                    FarmId = Guid.Empty,
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    ProductName = productName,
                    ProductDescription = productDescription,
                    Category = category,
                };
                await _client.SaveProductProfile(productProfile);

                // recall the product profile by name
                var productProfileByName = await _client.GetProductProfileByName(productProfile.ProductName);

                //      3. Associate ProductTemplate to Product Standard
                // associate the product profile with the product standard
                await _client.AssociateProductStandardToProductProfile(prodStd, productProfile, out var modified);
                await _client.SaveProductProfile(productProfile);

                //      4. SaveWorkOrder
                var workOrder = new WorkOrderModel()
                {
                    FarmId = Guid.Empty,
                    Id = Guid.NewGuid(),
                    WOName = woName,
                    SalesOrder = salesOrder,
                    OrderDate = DateTime.Now,
                    DeliveryDate = DateTime.Now,
                    State = WorkOrderState.Released,
                    Priority = 1,
                    Committed = true
                };
                var testWO = await SaveWorkOrder(workOrder);

                // get the work order by WOName
                var wo = await _client.GetWorkOrder(testWO.WOName);

                //      5. SaveWorkOrderLineItems
                var woLineItem = new WorkOrderLineItemsModel()
                {
                    FarmId = Guid.Empty,
                    Id = Guid.NewGuid(),
                    Name = lineItemName,
                    NumberofParts = numberofParts,
                    Location = location,
                    StartDate = DateTime.Now,
                    RequiredDate = DateTime.Now,
                    CustomerPartNumber = customerPartNumber,
                    WorkOrderId = testWO.Id,
                    ProductTemplateId = productProfile.Id
                };
                await _client.SaveWorkOrderLineItems(new List<WorkOrderLineItemsModel>() { woLineItem });

                //      6. CreateMultiOpStepsFromWorkOrder
                // create the opsteps for the created work order mdoel.
                var multiOpStepsFromWorkOrder = await _client.CreateMultiOpStepsFromWorkOrder(testWO.Id);

                Log(LoggingType.Info, "CreateOpstep", "GetOpStepsForWorkOrderAndLineItem for " + testWO.WOName + "+" + woLineItem.Name, "");

                // Find Opstep and update the opstep name to lineItemName
                // TODO....
                var opStepItem = await _client.GetOpStepsForWorkOrderAndLineItem(testWO.WOName, woLineItem.Name);
                if (opStepItem.Count == 1)
                {
                    if (opStepItem[0].OpStepName != lineItemName)
                    {
                        Log(LoggingType.Info, "CreateOpstep", "Opstep Name missmatch for " + testWO.WOName + "+" + woLineItem.Name, opStepItem[0].OpStepName);

                        // get the opstep list agains the WOName and opstepName
                        var _opStepList = await _client.GetOpStepsForWorkOrderLineItemById(woLineItem.Id);
                        foreach(var item in _opStepList)
                        {
                            if (item.OpStepName == opStepItem[0].OpStepName)
                            {
                                item.OpStepName = lineItemName;
                                await _client.SaveOpSteps(_opStepList);
                                break;
                            }
                        }

                    }
                }

                // get the opstep list agains the WOName and woLineItem.Name
                var opStepList = await _client.GetOpStepsForWorkOrderAndLineItem(testWO.WOName, woLineItem.Name);

                // get the opstep list agains the woLineItem.Id
                var opStepList2 = await _client.GetOpStepsForWorkOrderLineItemById(woLineItem.Id);

                // get the opstep list agains the WOName and opstepName
                //var opStepList3 = await _client.GetPendingOpStep(testWO.WOName, opStepList.First().OpStepName);
                var opStepList3 = await _client.GetPendingOpStep(testWO.WOName, woLineItem.Name);

                if (opStepList.Count() > 0)
                {
                    string _sono = "";
                    int _opno = 0;

                    //Update DepartmentQueue InMemex field
                    _sono = woName.PadLeft(8, ' ');
                    int.TryParse(lineItemName, out _opno);

                    string query = "UPDATE [JAM].[dbo].[DepartmentQueue] SET [in_memex] = 1 WHERE [sono] = '{0}' AND [opno] = {1} ";
                    string fullQuery = string.Format(query, _sono, _opno);

                    try
                    {

                        using (var uow = new UnitOfWork())
                        {
                            uow.ExecuteQuery(fullQuery);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError("CreateOpstep - Update in_memex: " + fullQuery, ex, ex.Message);
                    }
                }          

                //// ---------------------------------------------------------------------------------------------
                ///

                //wo = WorkOrderModel()
                //productStandardByName = ProductStandardModel
                //productProfileByName = ProductTemplateModel
                //opStepList/opStepList2 = List<PendingOpStepsModel>
                //opStepList3 = PendingOpStepsModel

                workOrderExt.WorkOrder = wo;
                workOrderExt.ProductStandard = productStandardByName;
                workOrderExt.ProductProfile = productProfileByName;
                workOrderExt.OpStepList = opStepList; // opStepList2;
                workOrderExt.OpStep = opStepList3;

            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                LogError("CreateOpstep " + fullPath, ex, ex.Message);
            }
            
            return workOrderExt;
        }

        public async Task LoadWorkOrderIntoMachine(string wo, int opStep, int machineNo)
        {
            // get the opstep list agains the WOName and opstepName
            var opStepList3 = await _client.GetPendingOpStep(wo, opStep.ToString());

            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await GetMachineAssets();
            //Guid machineAssetId = Guid.NewGuid();

            string path = "wo={0}|opno={1}|machineNo={2}";
            string fullPath = string.Format(path, wo, opStep.ToString(), machineNo.ToString());
            Log(LoggingType.Info, "LoadWorkOrderIntoMachine", fullPath);

            Guid machineAssetId = machines.Find(i => i.AssetTag == machineNo.ToString()).Id;

            Log(LoggingType.Info, "LoadWorkOrderIntoMachine", string.Format("wo={0}|opStep={1}|machineNo={2}|machineAssetId={3}|", wo, opStep.ToString(), machineNo.ToString(), machineAssetId.ToString()));

            //// ---------------------------------------------------------------------------------------------

            //// Logout or Remove the Opstep that is running on a Machine Asset
            //// Determine the machine that you want to logout the Opstep from.
            if (machines.Count != 0)// && operators.Count != 0)
            {
                // Note:    Jobstate and QueuedDate is required as valid values if there is an Opstep that is currently running on the Machine.
                //          For the previous opstep, Jobstate must be Pending, Queued, or Completed.

                //      Opstep exists?  |   JobState    |   QueuedDate  |   CommentLoadWorkOrderIntoMachine - GetLatestOpStepChange/LogoutOpStep 
                //      No              |                                   there isn't an opstep that is currently assigned to the machine.  JobState and Queued Date will be ignored.
                //      Yes             |   Pending(0)  |   No          |   Current Opstep will be set to a Jobstate of PENDING.  QueuedDate does not need to be valid.  Set to current date and time.
                //      Yes             |   Queued (1)  |   Yes         |   Current Opstep will be set to a Jobstate of QUEUED.  Asset will remaing the same.  Next run time for the Opstep will be set according to QueuedDate
                //      Yes             |   Running(2)  |   No          |   This is an INVALID condition.  QueuedDate does not need to be valid.  Set to current date and time.
                //      Yes             |   Completed(3)|   No          |   Current Opstep will be set to a Jobstate of COMPLETED.  QueuedDate does not need to be valid.  Set to current date and time.

                var jobState = 0;
                var queuedDate = DateTime.Now;

                // ---------------------------------------------------------------------------------------------

                Log(LoggingType.Info, "LoadWorkOrderIntoMachine - GetLatestOpStepChange", string.Format("machineAssetId={0}|", machineAssetId.ToString()));

                try
                {
                    var lastOpstep = await _client.GetLatestOpStepChange(machineAssetId);

                    //if (lastOpstep != null && lastOpstep.Id != Guid.Empty)
                    //{
                    //    // Set Current Running Opstep to Pending
                    //    machineId = machines[0].Id; // Set to a specific machine id
                    //    jobState = 0;
                    //    queuedDate = DateTime.Now;
                    //    await LogoutOpStep(machineId, jobState, queuedDate);
                    //}

                    //lastOpstep = await _client.GetLatestOpStepChange(machineId);

                    //if (lastOpstep != null && lastOpstep.Id != Guid.Empty)
                    //{
                    //    // Set Current Running Opstep to Queued
                    //    machineId = machines[0].Id;     // Set to a specific machine id
                    //    jobState = 1;
                    //    queuedDate = DateTime.Now;  // This can be set to the previous run date for the opstep or can be set to sometime in the future.  If unknown, then it is best to set the value to previous run date.
                    //    await LogoutOpStep(machineId, jobState, queuedDate);
                    //}

                    //lastOpstep = await _client.GetLatestOpStepChange(machineAssetId);

                    if (lastOpstep != null && lastOpstep.Id != Guid.Empty)
                    {
                        Log(LoggingType.Info, "LoadWorkOrderIntoMachine - LogoutOpStep", string.Format("wo={0}|opStep={1}|machineNo={2}|machineAssetId={3}|lastOpstepId={5}", wo, opStep.ToString(), machineNo.ToString(), machineAssetId.ToString()), lastOpstep.Id.ToString());

                        // Set Current Running Opstep to Completed
                        //machineId = machineAssetId; // machines[0].Id; // Set to a specific machine id
                        jobState = 3;
                        queuedDate = DateTime.Now;  // This can be set to the previous run date for the opstep or can be set to sometime in the future.  If unknown, then it is best to set the value to previous run date.
                        await LogoutOpStep(machineAssetId, jobState, queuedDate);
                    }
                }
                catch (Exception ex)
                {
                    LogError("LoadWorkOrderIntoMachine - GetLatestOpStepChange/LogoutOpStep. " + string.Format("wo={0}|opStep={1}|machineNo={2}|machineAssetId={3}|", wo, opStep.ToString(), machineNo.ToString(), machineAssetId.ToString()), ex, ex.Message);
                }

                // ---------------------------------------------------------------------------------------------

                //// Load an Opstep from a Work Order into the machine asset. 
                //// 1. Recall the opstep from the work order that you want to assign to the Machine
                //// 2. Determine the machne Id that you want to assign the opstep to
                //// 3. Request to assign the opstep to the machine
                var woName = opStepList3 != null ? opStepList3.WorkOrderModel.WOName : "";
                var opstepName = opStepList3 != null ? opStepList3.OpStepName : "";
                Log(LoggingType.Info, "LoadWorkOrderIntoMachine - GetPendingOpStepByWorkOrderNameAndOpstepName", string.Format("woName={0}|opstepName={1}", woName, opstepName));

                var opstep = await GetPendingOpStepByWorkOrderNameAndOpstepName(woName, opstepName);
                if (opstep != null)
                {
                    Log(LoggingType.Info, "LoadWorkOrderIntoMachine - RunOpstepOnMachineAssetNow", string.Format("machineAssetId={0}|opstepId={1}", machineAssetId.ToString(), opstep.Id.ToString()));

                    var opstepId = opstep.Id;
                    await RunOpstepOnMachineAssetNow(machineAssetId, opstepId);
                }

            }
            // ---------------------------------------------------------------------------------------------
        }

        public async Task LoginOperatorToMachine(int machineNo, string operatorUserId, bool login = true)
        {
            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await GetMachineAssets();
            var operators = await GetOperatorAssets();

            Guid machineAssetId = Guid.NewGuid();
            // Note: AssetTag is "0000" format
            Guid operatorAssetId = Guid.NewGuid();
            operatorUserId = operatorUserId.PadLeft(4, '0');

            string path = "machineNo={0}|operatorUserId={1}|login={2}";
            string fullPath = string.Format(path, machineNo.ToString(), operatorUserId, login.ToString());
            Log(LoggingType.Info, "LoginOperatorToMachine", fullPath);

            try
            {
                machineAssetId = machines.Find(i => i.AssetTag == machineNo.ToString()).Id;

                //operatorAssetId = operators.Find(i => i.AssetTag == operatorUserId).Id;
                AssetModel am = operators.Find(i => i.AssetTag == operatorUserId);
                if (am != null)
                {
                    operatorAssetId = am.Id;
                }
                else
                {
                    Log(LoggingType.Warn, "LoginOperatorToMachine issue: Using TEST OPERATOR", "");

                    am = operators.Find(i => i.AssetTag == "000"); // TEST OPERATOR 
                    if (am != null)
                    {
                        operatorAssetId = am.Id;
                    }
                    else
                    {
                        Log(LoggingType.Warn, "LoginOperatorToMachine Error: USER NOT FOUND", "");
                        LogError("LoginOperatorToMachine: USER NOT FOUND", null, "");

                        return;
                    }
                }

                if (login)
                {
                    // ---------------------------------------------------------------------------------------------
                    // Log IN an Operator to a machine
                    // 1. Determine what the Machine Id you want to log the Operator to log into
                    // 2. Determine the Operator Id of the specific operator
                    // 3. Request to login the operator to the machine
                    // Note: Changing the operator uses the same steps.
                    if (machines.Count != 0 && operators.Count != 0)
                    {
                        Log(LoggingType.Info, "LoginOperatorToMachine - Starting LoginOperator...", "");
                        await LoginOperator(machineAssetId, operatorAssetId);
                    }
                }
                else
                {
                    // ---------------------------------------------------------------------------------------------
                    //// Log OUT an Operator to a machine
                    //// 1. Determine what the Machine Id you want to log the Operator to log into
                    //// 2. Request to logout the operator to the machine
                    if (machines.Count != 0)
                    {
                        Log(LoggingType.Info, "LoginOperatorToMachine - Starting LogoutOperator...", "");
                        await LogoutOperator(machineAssetId);
                    }
                }
                //// ---------------------------------------------------------------------------------------------
            }
            catch (Exception ex)
            {
                path = "machineNo={0}|operatorUserId={1}|login={2}|machineAssetId={3}|operatorAssetId={4}";
                fullPath = string.Format(path, machineNo.ToString(), operatorUserId, login.ToString(), machineAssetId.ToString(), operatorAssetId.ToString());

                Log(LoggingType.Warn, "LoginOperatorToMachine Error: " + ex.Message, fullPath);
                LogError("LoginOperatorToMachine " + fullPath, ex, ex.Message, fullPath);
            }
        }

        public async Task<int> GetCount(string machineNo)
        {
            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await GetMachineAssets();
            //var operators = await GetOperatorAssets();

            Guid machineAssetId = Guid.NewGuid();
            machineAssetId = machines.Find(i => i.AssetTag == machineNo).Id;

            //// Note: AssetTag is "0000" format
            //Guid operatorAssetId = Guid.NewGuid();
            //operatorAssetId = operators.Find(i => i.AssetTag == operatorUserId).Id;

            //// ---------------------------------------------------------------------------------------------
            //// Generate a Part Count against a Machine Asset.
            if (machines.Count != 0)// && operators.Count != 0)
            {
                // generate a positive good part count against an asset
                var machineId = machineAssetId; // machines[0].Id; // Set to a specific machine id
                var partCount = 1;
                await RequestPartCount(machineId, partCount, false, null);

                // generate a negative good part count against an asset
                machineId = machineAssetId; // machines[0].Id; // Set to a specific machine id
                partCount = -1;
                await RequestPartCount(machineId, partCount, false, null);

                var rejectReasons = GetRejectStateAssociationsForAsset(machineId);
                if (rejectReasons != null)
                {
                    // Note: If the ReasonId is left blank or is an empty guid, then the reject will not be entered into Merlin.  ReasonId needs to be valid.

                    // generate a positive reject part count against an asset
                    // adds a reject part count to a reason 
                    machineId = machineAssetId; // machines[0].Id; // Set to a specific machine id
                    partCount = 1;
                    var rejectReasonId = rejectReasons.Result[0].RejectDefinitionId; // Select an appropriate reason code as indicated int he reject reasons that has been assoicated to the machine
                    await RequestPartCount(machineId, partCount, true, rejectReasonId);

                    // generate a negative good part count against an asset
                    // removes a reject part count to a reason 
                    machineId = machineAssetId; // machines[0].Id; // Set to a specific machine id
                    partCount = -1;
                    rejectReasonId = rejectReasons.Result[0].RejectDefinitionId; // Select an appropriate reason code as indicated int he reject reasons that has been assoicated to the machine
                    await RequestPartCount(machineId, partCount, true, rejectReasonId);

                }
                return partCount;
            }
            //// ---------------------------------------------------------------------------------------------
          
            return 0;
        }

        public async Task<HttpResponseMessage> SetCount(string workcenter, string sono, int opno, int partCount, string reason_id)
        {
            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await GetMachineAssets();
            //var operators = await GetOperatorAssets();

            Guid machineAssetId = Guid.NewGuid();

            try
            {
                machineAssetId = machines.Find(i => i.AssetTag == workcenter).Id;

                //// ---------------------------------------------------------------------------------------------
                //// Generate a Part Count against a Machine Asset.
                if (machines.Count != 0)// && operators.Count != 0)
                {
                    // generate a positive good part count against an asset
                    //var machineId = machineAssetId; // machines[0].Id; // Set to a specific machine id

                    string path = "machineId={0}|sono={1}|opno={2}|qty={3}|reason_id={4}";
                    string fullPath = string.Format(path, machineAssetId, sono, opno.ToString(), partCount.ToString(), reason_id);
                    Log(LoggingType.Info, "SetCount", fullPath);

                    if (partCount > 0)
                    {
                        await RequestPartCount(machineAssetId, partCount, false, null);

                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                    }
                    if (partCount < 0 && !string.IsNullOrWhiteSpace(reason_id))
                    {
                        await RequestPartCount(machineAssetId, partCount, true, new Guid(reason_id));

                        return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                    }

                    return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                string path = "SetCount/{0}/{1}/{2}/{3}/{4}";
                string fullPath = string.Format(path, machineAssetId, sono, opno.ToString(), partCount.ToString(), reason_id);

                Log(LoggingType.Warn, "SetCount Error: " + ex.Message, fullPath);
                LogError("SetCount Error: " + fullPath, ex, ex.Message, fullPath);
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            }
        }

        //public async Task ChangeMachineState(string machineNo, string stateName)
        //{
        //    // ---------------------------------------------------------------------------------------------
        //    // Recall the list of Machines
        //    var machines = await GetMachineAssets();

        //    Guid machineAssetId = Guid.NewGuid();
        //    machineAssetId = machines.Find(i => i.AssetTag == machineNo).Id;

        //    // ---------------------------------------------------------------------------------------------
        //    // Change Machine state for a Machine Asset 
        //    if (machines.Count != 0)
        //    {
        //        var machineId = machineAssetId; // machines[0].Id; // select the appropriate Machine's Id
        //        var assocStates = await GetMachineStateAssociationsForAsset(machineId); // this call is meant to show what associated states are available.
        //        if (assocStates.Count != 0)
        //        {
        //            //var assocState = assocStates[0];
        //            var assocState = assocStates.Find(i => i.StateDefinition == stateName)[0];

        //            await SetMachineStateForAsset(machineId, assocState.StateDefinition.Id); // this will handle the modal vs non modal condition                     
        //        }

        //        // return the machine state
        //        // the call will use the appropriate call depending on the current machine state.
        //        await ReturnMachineControlForAsset(machineId);

        //    }
        //    // --------------------------------------------------------------------------------------------- 
        //}

        #endregion

        // ----------------------------------------------------------------------------------

        #region Asset Data API calls

        public async Task<WorkOrderModel> GetWorkOrder(string woName)
        {
            // get the work order by WOName
            return await _client.GetWorkOrder(woName);
        }

        public async Task<PendingOpStepsModel> GetPendingOpStep(string woName, string opStepName)
        {
            // get the opstep list agains the WOName and opstepName
            return await _client.GetPendingOpStep(woName, opStepName);
        }


        /// <summary>
        ///  Get a list of all the available Machine Assets in Tempus
        /// </summary>
        /// <returns></returns>
        public async Task<List<AssetModel>> GetMachineAssets()
        {
            return await _client.GetAssetsOfType("MachineAsset");
        }

        /// <summary>
        /// Get a list of all the available Operator Assets in Tempus
        /// </summary>
        /// <returns></returns>
        public async Task<List<AssetModel>> GetOperatorAssets()
        {
            return await _client.GetAssetsOfType("OperatorAsset");
        }

        /// <summary>
        /// Get a specific asset by Guid
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        public async Task<AssetModel> GetAssetById(Guid assetId)
        {
            return await _client.GetAssetById(assetId);
        }

        /// <summary>
        /// Get a specific asset by asset tag.  
        /// </summary>
        /// <param name="assetTag"></param>
        /// <returns></returns>
        public async Task<AssetModel> GetAssetByTag(string assetTag)
        {
            return await _client.GetAssetByTag(assetTag);
        }

        /// <summary>
        /// Get all the Machine States that are currently associated with a Machine Asset.  The Guid Id is used as the reference.
        /// Used to assign a particular state to a machine asset.
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        public async Task<List<MesMachineStateAssociationModel>> GetMachineStateAssociationsForAsset(Guid assetId)
        {
            return await _client.GetMachineStateAssociations(assetId);
        }
        public async Task<List<MesMachineStateAssociationModel>> GetMachineStateAssociationsForAsset(string machineNo)
        {
            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await GetMachineAssets();

            Guid machineAssetId = Guid.NewGuid();
            machineAssetId = machines.Find(i => i.AssetTag == machineNo).Id;

            // ---------------------------------------------------------------------------------------------

            return await _client.GetMachineStateAssociations(machineAssetId);
        }

        /// <summary>
        /// Get all the Machine States that are currently associated with a Machine Asset.  The Guid Id is used as the reference.
        /// This will be used to generate reject part counts
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        public async Task<List<MesMachinePartRejectAssociationModel>> GetRejectStateAssociationsForAsset(Guid assetId)
        {
            return await _client.GetMachineRejectAssociations(assetId);
        }
        public async Task<List<MesMachinePartRejectAssociationModel>> GetRejectStateAssociationsForAsset(string machineNo)
        {
            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await GetMachineAssets();

            Guid machineAssetId = Guid.NewGuid();
            machineAssetId = machines.Find(i => i.AssetTag == machineNo).Id;

            // ---------------------------------------------------------------------------------------------

            //var  machineId = machines[0].Id; // Set to a specific machine id
            //var rejectReasons = await _client.GetMachineRejectAssociations(machineId);

            return await _client.GetMachineRejectAssociations(machineAssetId);
        }
        #endregion

        // ----------------------------------------------------------------------------------

        #region Machine asset's machine state api calls

        /// <summary>
        /// Return the machine asset's machine state according to the logic processor.
        /// This will handle both modal and non modal condition
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        public async Task ReturnMachineControlForAsset(Guid assetId)
        {
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
                            await _client.ClearModal(assetId);
                        }
                        else
                        {
                            await _client.RequestStateChange(assetId, Guid.Empty);
                        }
                    }
                }
                catch
                {

                }
                //var currentEvent = lastMachineStateEvents[0];
                //var machineStateEvent = (MesMachineStateEventModel)currentEvent.EventData;
                //if (machineStateEvent.StateDefinition.IsModal)
                //{
                //    // clear the modal machine state for a machine and return it to the current running machine state.  This will take the last known state indicated by the Logic Processor.
                //    await _client.ClearModal(machineId);
                //}
                //else
                //{
                //    // This will force the logic processor to re-evaluate the current running machine state.  For example, if the machine was in an Operator Down Reason, then the typical behaviour is that the machine will return to idle.  
                //    // The behaviour will be depnendent on how the logic processor is configured.
                //    await _client.RequestStateChange(machineId, Guid.Empty);
                //}
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// Check what the last known machine state.
        /// </summary>
        /// <param name="machineId"></param>
        /// <returns></returns>
        public async Task<List<EventModel>> GetLastMachineStateForMachine(Guid machineId)
        {
            return await _client.GetLastNEventsOfType(machineId, 1, "MesMachineStateChange");
        }

        /// <summary>
        /// Request the state change against an asset according to the state id.
        /// This could be simplified to remove the api request for the state association if the state associations for the asset is stored locally.
          /// </summary>
          /// <param name="assetId"></param>
          /// <param name="stateId"></param>
          /// <returns></returns>
        public async Task SetMachineStateForAsset(Guid assetId, Guid stateId)
        {
            // check the machine state
            var stateAssocs = await GetMachineStateAssociationsForAsset(assetId);
            if (stateAssocs != null)
            {
                var stateAssoc = stateAssocs.Find(el => el.Id == stateId);
                if (stateAssoc != null)
                {
                    if (stateAssoc.StateDefinition.IsModal)
                    {
                        await _client.RequestModalChange(assetId, stateId);
                    }
                    else
                    {
                        await _client.RequestStateChange(assetId, stateId);
                    }
                }
            }
            await Task.CompletedTask;
        }

        /// <summary>
        ///Request a part count.
        ///Note: the call to the GetMachineRejectAssociations can be modified if the reject associations is stored locally.
        ///  TODO: Request for Part Count needs to be created.  Currently you have to push the event in.  There is a better api to use for this.
        /// Note: should this compensate for the good part reasons as well as the reject reason ?  Technically the difference is determined by the category
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="count"></param>
        /// <param name="isReject"></param>
        /// <param name="reasonId"></param>
        /// <returns></returns>
        public async Task RequestPartCount(Guid assetId, int count, bool isReject, Guid? reasonId = null)
        {
            await _client.RequestPartCount(assetId, count, isReject, reasonId);
        }

        #endregion

        #region Operator login api calls

        /// <summary>
        /// Login an operator to a machine
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="operatorId"></param>
        /// <returns></returns>
        public async Task LoginOperator(Guid assetId, Guid operatorId)
        {
            await _client.ChangeOperator(assetId, operatorId);
        }
        public async Task LoginOperator(int machineNo, string operatorUserId)
        {
            operatorUserId = operatorUserId.PadLeft(4, '0');

            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await GetMachineAssets();
            var operators = await GetOperatorAssets();

            Guid machineAssetId = Guid.NewGuid();
            // Note: AssetTag is "0000" format
            Guid operatorAssetId = Guid.NewGuid();

            // ---------------------------------------------------------------------------------------------

            try
            {
                string path = "machineNo={0}|operatorUserId={1}|login={2}";
                string fullPath = string.Format(path, machineNo.ToString(), operatorUserId, "true");
                Log(LoggingType.Info, "LoginOperator", fullPath);

                machineAssetId = machines.Find(i => i.AssetTag == machineNo.ToString()).Id;

                //operatorAssetId = operators.Find(i => i.AssetTag == operatorUserId).Id;
                AssetModel am = operators.Find(i => i.AssetTag == operatorUserId);
                if (am != null)
                {
                    operatorAssetId = am.Id;
                }
                else
                {
                    Log(LoggingType.Warn, "LoginOperator issue: Using TEST OPERATOR", "");

                    am = operators.Find(i => i.AssetTag == "000"); // TEST OPERATOR 
                    if (am != null)
                    {
                        operatorAssetId = am.Id;
                    }
                    else
                    {
                        Log(LoggingType.Warn, "LoginOperator Error: USER NOT FOUND", "");
                        LogError("LoginOperator: USER NOT FOUND", null, "");

                        return;
                    }
                }

                await _client.ChangeOperator(machineAssetId, operatorAssetId);
            }
            catch (Exception ex)
            {
                string path = "machineNo={0}|operatorUserId={1}|machineAssetId={2}|operatorAssetId={3}";
                string fullPath = string.Format(path, machineNo.ToString(), operatorUserId, machineAssetId.ToString(), operatorAssetId.ToString());

                Log(LoggingType.Warn, "LoginOperator Error: " + ex.Message, fullPath);
                LogError("LoginOperator", ex, ex.Message, fullPath);
            }
        }

        /// <summary>
        /// Logout an operator to a machine.  This will remove the operator from the machine.
        /// </summary>
        /// <param name="assetId"></param>
        /// <returns></returns>
        public async Task LogoutOperator(Guid assetId)
        {
            await _client.ChangeOperator(assetId, Guid.Empty);
        }
        public async Task LogoutOperator(string machineNo)
        {
            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await GetMachineAssets();

            Guid machineAssetId = Guid.NewGuid();
            machineAssetId = machines.Find(i => i.AssetTag == machineNo).Id;

            // ---------------------------------------------------------------------------------------------

            await _client.ChangeOperator(machineAssetId, Guid.Empty);
        }

        #endregion

        // ----------------------------------------------------------------------------------

        #region Work order creation API calls

        //Work order creation is as follows:
        //    SaveProductStandard
        //        Add any machine overrides necessary for that product standard.
        //    SaveWorkOrder
        //    SaveWorkOrderLineItems
        //    CreateMultiOpStepsFromWorkOrder
        //        SaveOpSteps can be called indendently to update specific information against the opstep in case fiels like the OpStepName differs from the default OpstepName provided which is usually the product standard name

        /// <summary>
        ///Generates a 1 to 1 relationship between product standard and product profile
        ///End result is a product profile that is associated with a product standard with the given product profile name.
        /// </summary>
        /// <param name="productStandardModel"></param>
        /// <param name="productProfileName"></param>
        /// <param name="productProfileDescription"></param>
        /// <returns></returns>
        public async Task SaveProductStandard(ProductStandardModel productStandardModel, string productProfileName, string productProfileDescription)
        {
            await _client.SaveProductStandard(productStandardModel, productProfileName, productProfileDescription);
        }

        /// <summary>
        /// Updating the product standard and product profile
        /// Updates the 1 to 1 relationship between the product profile and product association.
        /// </summary>
        /// <param name="productStandardModel"></param>
        /// <param name="productProfileName"></param>
        /// <param name="productProfileDescription"></param>
        /// <returns></returns>
        public async Task UpdateProductStandard(ProductStandardModel productStandardModel, string productProfileName, string productProfileDescription)
        {
            await _client.UpdateProductStandard(productStandardModel, productProfileName, productProfileDescription);
        }

        /// <summary>
        /// Recall the product standard only
        /// </summary>
        /// <param name="prodStdName"></param>
        /// <returns></returns>
        public async Task<ProductStandardModel> GetProductStandardByName(string prodStdName)
        {
            return await _client.GetProductStandardByName(prodStdName);
        }

        /// <summary>
        /// Recall the product profile only
        /// </summary>
        /// <param name="prodProfName"></param>
        /// <returns></returns>
        public async Task<ProductTemplateModel> GetProductProfileByName(string prodProfName)
        {
            return await _client.GetProductProfileByName(prodProfName);
        }

        /// <summary>
        /// Save the work order
        /// </summary>
        /// <param name="workOrderModel"></param>
        /// <returns></returns>
        public async Task<WorkOrderModel> SaveWorkOrder(WorkOrderModel workOrderModel)
        {
            return await _client.SaveWorkOrder(workOrderModel);
        }

        /// <summary>
        /// Get the work order by work order name
        /// </summary>
        /// <param name="workOrderName"></param>
        /// <returns></returns>
        public async Task<WorkOrderModel> GetWorkOrderByWOName(String workOrderName)
        {
            return await _client.GetWorkOrder(workOrderName);
        }

        /// <summary>
        /// Add a work order line items to the work order
        /// </summary>
        /// <param name="workOrderLineItemsModels"></param>
        /// <returns></returns>
        public async Task SaveWorkOrderLineItems(IList<WorkOrderLineItemsModel> workOrderLineItemsModels)
        {
            await _client.SaveWorkOrderLineItems(workOrderLineItemsModels);
        }

        /// <summary>
        /// This will create the opsteps that are related to the work order line items.
        ///     Recall the work order line items for the work order
        ///     Note: call the work order and the work order line items will be populated.
        /// </summary>
        /// <param name="workOrderId"></param>
        /// <returns></returns>
        public async Task CreateMultiOpStepsFromWorkOrder(Guid workOrderId)
        {
            await _client.CreateMultiOpStepsFromWorkOrder(workOrderId);
        }

        /// <summary>
        /// Get the opstep by work order name and opstep name
        /// </summary>
        /// <param name="workOrderName"></param>
        /// <param name="opStepName"></param>
        /// <returns></returns>
        public async Task<PendingOpStepsModel> GetPendingOpStepByWorkOrderNameAndOpstepName(string workOrderName, string opStepName)
        {
            return await _client.GetPendingOpStep(workOrderName, opStepName);
        }

        /// <summary>
        /// Get the opsteps by work order name and work order line item name
        /// </summary>
        /// <param name="workOrderName"></param>
        /// <param name="workOrderLineItemName"></param>
        /// <returns></returns>
        public async Task<List<PendingOpStepsModel>> GetOpStepsForWorkOrderAndLineItem(string workOrderName, string opStepName)
        {
            return await _client.GetOpStepsForWorkOrderAndLineItem(workOrderName, opStepName);
        }

        /// <summary>
        /// Get the opsteps by work order line item Id
        /// </summary>
        /// <param name="workOrderLineItemId"></param>
        /// <returns></returns>
        public async Task GetOpStepsForWorkOrderLineItemById(Guid workOrderLineItemId)
        {

            await _client.GetOpStepsForWorkOrderLineItemById(workOrderLineItemId);
        }

        /// <summary>
        /// Update a list of opsteps
        /// </summary>
        /// <param name="opsteps"></param>
        /// <returns></returns>
        public async Task SaveOpSteps(List<PendingOpStepsModel> opsteps)
        {
            await _client.SaveOpSteps(opsteps);
        }

        /// <summary>
        /// Sets an opstep for the current machine
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="opstepId"></param>
        /// <returns></returns>
        public async Task RunOpstepOnMachineAssetNow(Guid assetId, Guid opstepId)
        {
            await _client.JobRunNow(opstepId, assetId);
        }

        #endregion

        // ----------------------------------------------------------------------------------

        #region Logs the opstep api calls

        /// <summary>
        ///Logs the opstep that is currently assigned to the machine asset id.
        /// Note: jobState is an enumerated value where
        ///        Pending     = 0     Opstep is available for all machines.Product Standard dictate what machines this is capable on.
        ///        Queued      = 1     Opstep has been assigned to a specific machine.  Once it has been set to "Queued", other machines that are capable of running the Opstep can't see the opstep.
        ///        Running     = 2     This is the current running Opstep for the given machine asset.
        ///        Completed   = 3     The Opstep is considered to be completed.
        /// Note: queuedDate is the date where you would expect the Opstep to get rerun again.  
        ///       The queuedDate should always be set to a valid value if JobState is either Pending or Queued.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="jobState"></param>
        /// <param name="queuedDate"></param>
        /// <returns></returns>
        public async Task LogoutOpStep(Guid assetId, int jobState, DateTime queuedDate)
        {
            await _client.LogoutOpStep(assetId, jobState, queuedDate);
        }
        public async Task LogoutOpStep(int machineNo, int jobState, DateTime queuedDate)
        {
            var machines = await GetMachineAssets();
            var machineAssetId = machines.Find(i => i.AssetTag == machineNo.ToString()).Id;

            Log(LoggingType.Info, "LogoutOpStep", string.Format("machineNo={0}|jobState={1}|queuedDate={2}|machineAssetId={3}", machineNo.ToString(), jobState.ToString(), queuedDate.ToShortTimeString(), machineAssetId.ToString()));

            await _client.LogoutOpStep(machineAssetId, jobState, queuedDate);
        }
        
        #endregion

        // ----------------------------------------------------------------------------------

        #region Metric related api calls

        //Metric related api calls
        //        get metric types
        //        get metric groups
        //        get metric for asset for a group for a duration of time
        //Note: this should be the final always.  No sense in muddying the waters by providing the other version.

        /// <summary>
        /// Provides a list of the available Metric Types within Tempus
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> GetMetricTypes()
        {
            return await _client.GetMetricTypes();
        }

        /// <summary>
        /// Provides a list of the metric groups to aggregate the data against a specific metric type.
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> GetMetricGroups()
        {
            return await _client.GetMetricGroups();
        }

        /// <summary>
        /// assetId = Machine Id
        /// Returns the accumulated data for the metric type, metric grouping, asset within the date range provided.
        /// </summary>
        public async Task<List<MetricPartModel>> GetFinalMetricsForRange(Guid assetId, string metricType, string metricGroup, DateTime startDate, DateTime endDate)
        {
            try
            {
                //http://jep-merlin.jadeplastics.local/api/metrics/final/getrange/8addcec6-597a-456e-ac97-5c25220d4c20/MachineStateAndPart/MesOpStepChange/1698638400000/1698854340000

                var metricsForOpstepEvents = await _client.GetFinalMetricsForRange(assetId, metricType, metricGroup, startDate, endDate);
                return metricsForOpstepEvents;
            }
            catch (Exception ex)
            {
                return await GetFinalMetricsForRangeDirect(assetId, metricType, metricGroup, startDate, endDate);
            }
        }
        public async Task<List<MetricPartModel>> GetFinalMetricsForRangeDirect(Guid assetId, string metricType, string metricGroup, DateTime startDate, DateTime endDate)
        {
            HttpClient _httpClient = GetHttpClient();

            string path = "api/metrics/final/getrange/{0}/{1}/{2}/{3}/{4}";
            Int64 fromMillis = startDate.ToUnixTimestampInMilliseconds();   // 1698638400000;
            Int64 toMillis = endDate.ToUnixTimestampInMilliseconds();       // 1698854340000;
            string fullPath = string.Format(path, assetId.ToString(), metricType, metricGroup, fromMillis, toMillis);

            ////http://jep-merlin.jadeplastics.local/api/metrics/final/getrange/8addcec6-597a-456e-ac97-5c25220d4c20/TotalPartCount/OperatorChange/1698638400000/1698854340000
            //fullPath = "api/metrics/final/getrange/8addcec6-597a-456e-ac97-5c25220d4c20/TotalPartCount/OperatorChange/1698638400000/1698854340000";

            HttpResponseMessage response = await _httpClient.GetAsync(fullPath);

            List<MetricPartModel> list = new List<Merlin.Platform.Standard.Data.Models.MetricPartModel>();
            if (response.IsSuccessStatusCode)
            {
                //list = await response.Content.ReadAsAsync<List<MetricPartModel>>();
                var data = await response.Content.ReadAsStringAsync();
                //var json = JsonConvert.SerializeObject(data);

                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(data);
                var jData = jsonObj["Data"];
                var jValues = jData["$values"];
                var jDataValues = jsonObj["Data"]["$values"];

                JObject obj = JObject.Parse(data);
                var _data = obj["Data"];
                var _items = _data["$values"];

                int c = _items.Count();
                foreach (var item in _items)
                {
                    //item.ToList()

                    //list.Add(new MetricPartModel()
                    //{
                    //    FarmId = new Guid(item.Value<string>("FarmId")),
                    //    SliceId = item.Value<DateTime>("Start"),
                    //    LastUpdated = item.Value<DateTime>("LastUpdated"),
                    //    MetricType = item.Value<string>("MetricType"),
                    //    MetricGroup = item.Value<string>("MetricGroup"),
                    //    EventType = item.Value<string>("EventType"),
                    //    EventGroup = item.Value<string>("EventGroup"),
                    //    AssetId = new Guid(item.Value<string>("AssetId")),
                    //    EventId = new Guid(item.Value<string>("EventId")),
                    //    Completed = item.Value<bool>("Completed"),
                    //    End = item.Value<DateTime>("End"),
                    //    Recalc = item.Value<bool>("Recalc"),
                    //    TimeSpan = item.Value<int>("TimeSpan"),
                    //    EventIds = null,
                    //    ExtraData = null,
                    //    GenericValue = item.Value<int>("MetricValue")
                    //});

                    MetricPartModel result = new MerlinClientApi.Models.MetricPartModelExt();
                    result.FarmId = new Guid(item.Value<string>("FarmId"));
                    //[JsonProperty("Start")]
                    result.SliceId = item.Value<DateTime>("Start");
                    result.LastUpdated = item.Value<DateTime>("LastUpdated");
                    result.MetricType = item.Value<string>("MetricType");
                    result.MetricGroup = item.Value<string>("MetricGroup");
                    result.EventType = item.Value<string>("EventType");
                    result.EventGroup = item.Value<string>("EventGroup");
                    result.AssetId = new Guid(item.Value<string>("AssetId"));
                    result.EventId = new Guid(item.Value<string>("EventId"));
                    result.Completed = item.Value<bool>("Completed");
                    result.End = item.Value<DateTime>("End");
                    result.Recalc = item.Value<bool>("Recalc");
                    result.TimeSpan = item.Value<int>("TimeSpan");

                    result.EventIds = null;
                    result.ExtraData = null;
                    //[JsonIgnore]
                    result.GenericValue = item.Value<int>("MetricValue");
                    list.Add(result);
                }
            }
            return list;
        }

        /// <summary>
        ///  Returns the accumulated data for the requestData provided.  This provides data for more than one asset.
        /// </summary>
        /// <param name="requestData"></param>
        /// <returns></returns>
        public async Task<List<Merlin.Platform.Standard.Data.Models.MetricPartModel>> GetFinalMetricsForRangeForAssets(MetricsRequest requestData)
        {
            return await _client.GetFinalMetricsForRangeForAssets(requestData);
        }

        /// <summary>
        /// Rebuilds the metrics for an asset for a particular metric type within the given Time Range.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="metricType"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task RebuildRangeByAssetByMetric(Guid assetId, string metricType, DateTime startDate, DateTime endDate)
        {
            await _client.RebuildRangeByAssetByMetric(assetId, metricType, startDate, endDate);
        }

        /// <summary>
        /// Rebuilds all the metrics for an asset within the given Time Range.
        /// </summary>
        /// <param name="assetId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task RebuildRangeByAsset(Guid assetId, DateTime startDate, DateTime endDate)
        {
            await _client.RebuildRangeByAsset(assetId, startDate, endDate);
        }

        public async Task<int> GetMachineCountTEST(string machineNo)
        {
            HttpClient _httpClient = GetHttpClient();

            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await GetMachineAssets();

            Guid machineAssetId = Guid.NewGuid();
            machineAssetId = machines.Find(i => i.AssetTag == machineNo).Id;

            // ---------------------------------------------------------------------------------------------

            string path = "api/Assets/{0}/Metrics/GoodPartCount/MesOpStepChange";

            string fullPath = string.Format(path, machineAssetId.ToString());

            HttpResponseMessage response = await _httpClient.GetAsync(fullPath);

            int count = 0;

            if (response.IsSuccessStatusCode)
            {
                //list = await response.Content.ReadAsAsync<List<MetricPartModel>>();
                var data = await response.Content.ReadAsStringAsync();
                //var json = JsonConvert.SerializeObject(data);

                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(data);
                var jData = jsonObj["Data"];
                var jValues = jData["$values"];
                var jDataValues = jsonObj["Data"]["$values"];

                JObject obj = JObject.Parse(data);
                var _data = obj["Data"];
                var _items = _data["$values"];

                int c = _items.Count();
                foreach (var item in _items)
                {
                    //MetricPartModel result = new MerlinClientApi.Models.MetricPartModelExt();
                    //result.FarmId = new Guid(item.Value<string>("FarmId"));
                    ////[JsonProperty("Start")]
                    //result.SliceId = item.Value<DateTime>("Start");
                    //result.LastUpdated = item.Value<DateTime>("LastUpdated");
                    //result.MetricType = item.Value<string>("MetricType");
                    //result.MetricGroup = item.Value<string>("MetricGroup");
                    //result.EventType = item.Value<string>("EventType");
                    //result.EventGroup = item.Value<string>("EventGroup");
                    //result.AssetId = new Guid(item.Value<string>("AssetId"));
                    //result.EventId = new Guid(item.Value<string>("EventId"));
                    //result.Completed = item.Value<bool>("Completed");
                    //result.End = item.Value<DateTime>("End");
                    //result.Recalc = item.Value<bool>("Recalc");
                    //result.TimeSpan = item.Value<int>("TimeSpan");

                    //result.EventIds = null;
                    //result.ExtraData = null;
                    ////[JsonIgnore]
                    //result.GenericValue = item.Value<int>("MetricValue");
                    //list.Add(result);
                }
            }
            return count;
        }

        #endregion

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

        #region Api Test Routine Function

        public async Task ApiTestRoutine()
        {
            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await GetMachineAssets();
            var operators = await GetOperatorAssets();

            // ---------------------------------------------------------------------------------------------
            // Log IN an Operator to a machine
            // 1. Determine what the Machine Id you want to log the Operator to log into
            // 2. Determine the Operator Id of the specific operator
            // 3. Request to login the operator to the machine
            // Note: Changing the operator uses the same steps.
            if (machines.Count != 0 && operators.Count != 0)
            {
                var machineId = machines[0].Id;
                var operatorId = operators[0].Id;
                await LoginOperator(machineId, operatorId);
            }

            //// Log OUT an Operator to a machine
            //// 1. Determine what the Machine Id you want to log the Operator to log into
            //// 2. Request to logout the operator to the machine
            if (machines.Count != 0)
            {
                var machineId = machines[0].Id;
                await LogoutOperator(machineId);
            }
            //// ---------------------------------------------------------------------------------------------
            //// Generate a Part Count against a Machine Asset.
            if (machines.Count != 0 && operators.Count != 0)
            {
                // generate a positive good part count against an asset
                var machineId = machines[0].Id; // Set to a specific machine id
                var partCount = 1;
                await RequestPartCount(machineId, partCount, false, null);

                // generate a negative good part count against an asset
                machineId = machines[0].Id; // Set to a specific machine id
                partCount = -1;
                await RequestPartCount(machineId, partCount, false, null);

                var rejectReasons = GetRejectStateAssociationsForAsset(machineId);
                if (rejectReasons != null)
                {
                    // Note: If the ReasonId is left blank or is an empty guid, then the reject will not be entered into Merlin.  ReasonId needs to be valid.

                    // generate a positive reject part count against an asset
                    // adds a reject part count to a reason 
                    machineId = machines[0].Id; // Set to a specific machine id
                    partCount = 1;
                    var rejectReasonId = rejectReasons.Result[0].RejectDefinitionId; // Select an appropriate reason code as indicated int he reject reasons that has been assoicated to the machine
                    await RequestPartCount(machineId, partCount, true, rejectReasonId);

                    // generate a negative good part count against an asset
                    // removes a reject part count to a reason 
                    machineId = machines[0].Id; // Set to a specific machine id
                    partCount = -1;
                    rejectReasonId = rejectReasons.Result[0].RejectDefinitionId; // Select an appropriate reason code as indicated int he reject reasons that has been assoicated to the machine
                    await RequestPartCount(machineId, partCount, true, rejectReasonId);
                }
            }
            //// ---------------------------------------------------------------------------------------------
            // Change Machine state for a Machine Asset 
            if (machines.Count != 0)
            {
                var machineId = machines[0].Id; // select the appropriate Machine's Id
                var assocStates = await GetMachineStateAssociationsForAsset(machineId); // this call is meant to show what associated states are available.
                if (assocStates.Count != 0)
                {
                    var assocState = assocStates[0];
                    await SetMachineStateForAsset(machineId, assocState.StateDefinition.Id); // this will handle the modal vs non modal condition                     
                }

                // return the machine state
                // the call will use the appropriate call depending on the current machine state.
                await ReturnMachineControlForAsset(machineId);

            }
            //// --------------------------------------------------------------------------------------------- 
            //// Create an Opstep
            ////  The creation of an opstep requires the following steps:
            ////      1. SaveProductStandard
            ////          a. Add any machine overrides necessary for that product standard.
            ////      2. SaveProductTemplate
            ////      3. Associate ProductTemplate to Product Standard
            ////      4. SaveWorkOrder
            ////      5. SaveWorkOrderLineItems
            ////      6. CreateMultiOpStepsFromWorkOrder
            ////          a. SaveOpSteps can be called indendently to update specific information against the opstep in case fiels like the OpStepName differs from the default OpstepName provided which is usually the product standard name
            ////  The steps will be outlined below with the corresponding API calls.

            ////      1. SaveProductStandard
            var prodStd = new ProductStandardModel()
            {
                FarmId = Guid.Empty,
                Id = Guid.NewGuid(),
                AcceptedCycleTime = TimeSpan.FromMinutes(1),
                AnyMachine = true,
                CyclePerParts = 1,
                CycleTime = TimeSpan.FromMinutes(1),
                MachineCutTime = TimeSpan.FromMinutes(1),
                MaterialConveyanceTime = TimeSpan.FromMinutes(1),
                PartsPerCycle = 1,
                ProductId = "Test Product",
                ProductName = "Test Product",
                ProductDescription = "Test",
                Category = "ProductStandard",
                SetUpTime = TimeSpan.FromMinutes(1),
                ProductStandardMachineOverrides = new List<ProductStandardMachineOverridesModel>()
            };

            if (machines.Count != 0)
            {
                // add the specific machines that this product standard is allowed to run on.
                // for this example, we will use the first machine as the asset to add.
                // The machine override should start off as a copy of the original product standard.
                var prodStdMachOverridForAsset = new ProductStandardMachineOverridesModel()
                {
                    FarmId = Guid.Empty,
                    Id = Guid.NewGuid(),
                    AssetId = machines[0].Id,
                    AcceptedCycleTime = prodStd.AcceptedCycleTime,
                    CyclePerParts = prodStd.CyclePerParts,
                    CycleTime = prodStd.CycleTime,
                    MachineCutTime = prodStd.MachineCutTime,
                    MaterialConveyanceTime = prodStd.MaterialConveyanceTime,
                    PartsPerCycle = prodStd.PartsPerCycle,
                    ProductId = prodStd.ProductId,
                    ProductName = prodStd.ProductName,
                    ProductDescription = prodStd.ProductDescription,
                    Category = prodStd.Category,
                    SetUpTime = prodStd.SetUpTime,
                    ProductStandardId = prodStd.Id

                };
                prodStd.ProductStandardMachineOverrides.Add(prodStdMachOverridForAsset);
            }
            //

            await SaveProductStandard(prodStd, "", "");

            // recall the product standard
            var productStandardByName = await _client.GetProductStandardByName(prodStd.ProductName);

            // Note Check to see if i can add the machine overrides as well to the product standard using this call.

            //      2. SaveProductTemplate
            var productProfile = new ProductTemplateModel()
            {
                FarmId = Guid.Empty,
                Id = Guid.NewGuid(),
                ProductId = "Test Product Template",
                ProductName = "Test Product Template",
                ProductDescription = "Test Template",
                Category = "ProductTemplate",
            };
            await _client.SaveProductProfile(productProfile);

            // recall the product profile by name
            var productProfileByName = await _client.GetProductProfileByName(productProfile.ProductName);

            //      3. Associate ProductTemplate to Product Standard
            // associate the product profile with the product standard
            await _client.AssociateProductStandardToProductProfile(prodStd, productProfile, out var modified);
            await _client.SaveProductProfile(productProfile);

            //      4. SaveWorkOrder
            var workOrder = new WorkOrderModel()
            {
                FarmId = Guid.Empty,
                Id = Guid.NewGuid(),
                WOName = "Test Work Order",
                SalesOrder = "Test",
                OrderDate = DateTime.Now,
                DeliveryDate = DateTime.Now,
                State = WorkOrderState.Released,
                Priority = 1,
                Committed = true
            };
            var testWO = await SaveWorkOrder(workOrder);

            // get the work order by WOName
            var wo = await _client.GetWorkOrder(testWO.WOName);

            //      5. SaveWorkOrderLineItems
            var woLineItem = new WorkOrderLineItemsModel()
            {
                FarmId = Guid.Empty,
                Id = Guid.NewGuid(),
                Name = "Test Line Item",
                NumberofParts = 10,
                Location = "Test",
                StartDate = DateTime.Now,
                RequiredDate = DateTime.Now,
                CustomerPartNumber = "TEST",
                WorkOrderId = testWO.Id,
                ProductTemplateId = productProfile.Id
            };
            await _client.SaveWorkOrderLineItems(new List<WorkOrderLineItemsModel>() { woLineItem });

            //      6. CreateMultiOpStepsFromWorkOrder
            // create the opsteps for the created work order mdoel.
            var multiOpStepsFromWorkOrder = await _client.CreateMultiOpStepsFromWorkOrder(testWO.Id);

            // get the opstep list agains the WOName and woLineItem.Name
            var opStepList = await _client.GetOpStepsForWorkOrderAndLineItem(testWO.WOName, woLineItem.Name);

            // get the opstep list agains the woLineItem.Id
            var opStepList2 = await _client.GetOpStepsForWorkOrderLineItemById(woLineItem.Id);

            // get the opstep list agains the WOName and opstepName
            var opStepList3 = await _client.GetPendingOpStep(testWO.WOName, opStepList.First().OpStepName);

            //// ---------------------------------------------------------------------------------------------

            //// Load an Opstep from a Work Order into the machine asset. 
            //// 1. Recall the opstep from the work order that you want to assign to the Machine
            //// 2. Determine the machne Id that you want to assign the opstep to
            //// 3. Request to assign the opstep to the machine
            var woName = opStepList3 != null ? opStepList3.WorkOrderModel.WOName : "";
            var opstepName = opStepList3 != null ? opStepList3.OpStepName : "";
            var opstep = await GetPendingOpStepByWorkOrderNameAndOpstepName(woName, opstepName);
            if (opstep != null)
            {
                var machineId = machines[0].Id; // insert one of the valid machine asset Ids
                var opstepId = opstep.Id;
                await RunOpstepOnMachineAssetNow(machineId, opstepId);
            }

            //// Logout or Remove the Opstep that is running on a Machine Asset
            //// Determine the machine that you want to logout the Opstep from.
            if (machines.Count != 0 && operators.Count != 0)
            {
                // Note:    Jobstate and QueuedDate is required as valid values if there is an Opstep that is currently running on the Machine.
                //          For the previous opstep, Jobstate must be Pending, Queued, or Completed.

                //      Opstep exists?  |   JobState    |   QueuedDate  |   Comment
                //      No              |                                   there isn't an opstep that is currently assigned to the machine.  JobState and Queued Date will be ignored.
                //      Yes             |   Pending(0)  |   No          |   Current Opstep will be set to a Jobstate of PENDING.  QueuedDate does not need to be valid.  Set to current date and time.
                //      Yes             |   Queued (1)  |   Yes         |   Current Opstep will be set to a Jobstate of QUEUED.  Asset will remaing the same.  Next run time for the Opstep will be set according to QueuedDate
                //      Yes             |   Running(2)  |   No          |   This is an INVALID condition.  QueuedDate does not need to be valid.  Set to current date and time.
                //      Yes             |   Completed(3)|   No          |   Current Opstep will be set to a Jobstate of COMPLETED.  QueuedDate does not need to be valid.  Set to current date and time.

                var machineId = machines[0].Id;
                var jobState = 0;
                var queuedDate = DateTime.Now;
                var lastOpstep = await _client.GetLatestOpStepChange(machineId);

                //if (lastOpstep != null && lastOpstep.Id != Guid.Empty)
                //{
                //    // Set Current Running Opstep to Pending
                //    machineId = machines[0].Id; // Set to a specific machine id
                //    jobState = 0;
                //    queuedDate = DateTime.Now;
                //    await LogoutOpStep(machineId, jobState, queuedDate);
                //}

                //lastOpstep = await _client.GetLatestOpStepChange(machineId);

                //if (lastOpstep != null && lastOpstep.Id != Guid.Empty)
                //{
                //    // Set Current Running Opstep to Queued
                //    machineId = machines[0].Id;     // Set to a specific machine id
                //    jobState = 1;
                //    queuedDate = DateTime.Now;  // This can be set to the previous run date for the opstep or can be set to sometime in the future.  If unknown, then it is best to set the value to previous run date.
                //    await LogoutOpStep(machineId, jobState, queuedDate);
                //}

                lastOpstep = await _client.GetLatestOpStepChange(machineId);

                if (lastOpstep != null && lastOpstep.Id != Guid.Empty)
                {
                    // Set Current Running Opstep to Completed
                    machineId = machines[0].Id; // Set to a specific machine id
                    jobState = 3;
                    queuedDate = DateTime.Now;  // This can be set to the previous run date for the opstep or can be set to sometime in the future.  If unknown, then it is best to set the value to previous run date.
                    await LogoutOpStep(machineId, jobState, queuedDate);
                }
            }
            // ---------------------------------------------------------------------------------------------
            // To determine the cycletime for an opstep
            //      Need to determine what the start and end time is for the specific opstep
            //      Use the metrics to calculate how much time was associated to a particular state and how many parts were generated for a specific opstep.
            /*
            
                Currently there isn't a function call within Merlin that calculates the cycle time for an opstep. 
                The definition for Cycletime can vary between implementations due to what the customer expects the formula to be.
                In this example, we will assume that the Cycletime formula is as follows:
                    Cycletime = (Uptime over the given duration) / (Total # of Parts created over the given duration)
                        Note: in Tempus, uptime is categorized against the state type called "uptime".  Any machine state associated with uptime will then be used for the metric type "Uptime"
                        Note: The customer can declare what machine states are considered to be "uptime" which can be different that what is configured in Tempus.  If this is the case, then the metric type to use will be "MachineStateAndPart".  This metric will show all the various machine states and any parts that have been generated.
           
                This information can be retrieved using the Metrics API.
                The Metrics is able to group certain pieces of data using different grouping criterias.
                    The Metric Types are as follows: 
                        
                    The Metric Groups are as follows:

                    Each metric information is related to an event and a duration.
                    
                    If the metric group for an opstep is called, then each opstep change will introduce a different entry.  Aggregration of the data will need to happen and the source of the information will need to be validated wrt event, opstep, and interval.

                We can call a metric type against a grouping criteria over any given interval of time.  Note: The metric database does not store the calculated metric types through all of history.  
                    The metric database holds the most recent metric information.  If an interval was to be called and not found in the metric database, then the information will need to be calculated and placed in the metric database before the API can provide the data.
                    If a long duration was to be called, more than 6 month for example, the api call will take some time to generate the information.  The Api may timeout in some instances if the duration is very large.
                    In order to alleviate some of these issues, the customer would need to determine what is the most important data and at what interval.
                For example, 
                    does the customer want the cycle time for a given opstep    
                        - for the past day? 
                        - for the past shift? 
                        - for the duration that the last opstep was running for? 
                        - for the entire duration the opstep had ran for?

                    As the duration grows, the amount of time it takes to retrieve data will grow as well.  Especially if the data has to be calculated.
                
                    Because the data for cycletime is derived and not stored in the merlin database, there is concern for how the data is polled because of performance issues.
                    
                    
                    If we consider using a trigger point to collect the data, 
                        - end of shift
                            > the customer could store the result of the cycletime in their database and keep a running tally of the results.  The cycletime could then be updated when the shift ends for that osptep.  This would reduce any performance issues generated from long interval metric calls.
                        - opstep is removed from machine
                            > When an opstep is removed from the machine, if a running tally of the cycletime was being stored in a different database, then an update could be made for th recent events only.  Otherwise the metric call would need to be called for the entire duration.

                The basic steps are:
                    Determine the duration
                    Determine the opsteps that fall in that duration via eventing APIs
                    Call the Metric Types: MachineStateAndPart against Metric Group: OpstepChange for the duration
                        Aggregate the information according to the information above.
                            If Incycle is the uptime, then add all the uptime values that correspond to the duration of time that the opstep change event occurred at.
                            Add all the part count values that correspond to the duration of time that the opstep change event occurred at.
                            The Current Uptime and Current Total Parts could be stored in a customer Database.
                        Once the final trigger happens, such as opstep being removed from the machine, then the total accumulation of Uptime and total parts could be used to generate the cycle time data.
                    
                The heaviest process in this will be calculating the metric information for a given duration if the duration is a very long period of time and does not exist in the database.
             */

            if (machines.Count != 0)
            {
                //  1. If we choose the duration period for 24 hours
                //  2. Select the Machine(s) to poll
                //      Note: In the following example, the machine asset: "Gantry Mill", will be used to represent the calculations.
                //  3. Retrieve all the opstep change events that occurred within the time given start and end times.
                //  4. Retrieve the metrics (metric Type: "MachineStateAndPart", metric Group: "MesOpStepChange|") for the Machine Asset: "Gantry Mill"
                //  5. Accumulate the amount of time for "UpTime"
                //  6. Accumulate the amount of parts created within the time period
                //      Note: At this point the accumulated CycleTime and part counts can be stored.  SOme sort of timestamp with a reference to the opstep should be stored with the data for further use.
                //  7. Calculate the interim cycletime per part.
                //      Note: It is up to the customer to determine what is the best way to represent cycletime and how propagate that data their respective system.

                //1.If we choose the duration period for 24 hours
                var currentDateTime = DateTime.Now;
                var start = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day, 0, 0, 0);
                currentDateTime = currentDateTime.AddDays(1);
                var end = new DateTime(currentDateTime.Year, currentDateTime.Month, currentDateTime.Day, 0, 0, 0);

                //2.Select the Machine(s) to poll
                var machine = machines.Find(el => el.Name == "Gantry Mill");

                if (machine != null)
                {

                    //3.Retrieve all the opstep change events that occurred within the time given start and end times.
                    var eventType = "MesOpStepChange";
                    var eventGroup = "";
                    var opstepEvents = await _client.GetEventsForAssetOfTypeForDateRange(machine.Id, eventType, eventGroup, start, end);

                    //4.Retrieve the metrics(metric Type: "MachineStateAndPart", metric Group: "MesOpStepChange|") for the Machine Asset: "Gantry Mill"
                    var metricType = "MachineStateAndPart";
                    var metricGroup = "MesOpStepChange|";
                    try
                    {
                        var metricsForOpstepEvents = await _client.GetFinalMetricsForRange(machine.Id, metricType, metricGroup, start, end);
                        // wrap in a try catch in case of null being returned.

                        if (opstepEvents != null && opstepEvents.Count != 0 && metricsForOpstepEvents != null && metricsForOpstepEvents.Count != 0)
                        {
                            //5.Accumulate the amount of time for "UpTime"
                            foreach (var opstepEvent in opstepEvents)
                            {
                                var opstepEventData = (MesOpStepChangeEventModel)opstepEvent.EventData;
                                if (opstepEventData != null && opstepEventData.OpStepId != Guid.Empty)
                                {
                                    var eventId = opstepEvent.Id;
                                    var opstepId = opstepEventData.OpStepId;
                                    var accumulatedPartsForOpstep = (Int64)0;
                                    var accumulatedCycleTimeForOpstep = (Int64)0;

                                    // retrieve the last known model for the opstep.  This should contain the total parts produced information.
                                    var currentOpstep = await _client.GetPendingOpStep(opstepEventData.OpStep.WorkOrderModel.WOName, opstepEventData.OpStep.OpStepName);

                                    // determine what the opstep that you want to accumulate the data for.
                                    foreach (var metricPartModel in metricsForOpstepEvents)
                                    {
                                        if (metricPartModel.EventIds.Find(el => el == eventId) != Guid.Empty)
                                        {
                                            // it is part of the correct shift.

                                            var containerDictionary = (RootContainerDictionary)metricPartModel.GenericValue;
                                            if (containerDictionary != null && containerDictionary.Count != 0)
                                            {
                                                //  5. Accumulate the amount of time for "UpTime"
                                                if (containerDictionary.TryGetValue("States", out var statesForOpstep))
                                                {
                                                    foreach (var stateInfoDictionary in statesForOpstep.Values)
                                                    {
                                                        if (stateInfoDictionary.TryGetValue("Name", out var stateNameValue))
                                                        {
                                                            if (stateNameValue.ToString().ToLower() == "InCycle".ToLower() && stateInfoDictionary.TryGetValue("Value", out var stateTimeValue))
                                                            {
                                                                var stateTimeBase = (StateInfoValueDictionary)stateTimeValue;
                                                                if (stateTimeBase.TryGetValue("Time", out var stateTimeInMilliseconds))
                                                                {
                                                                    if (stateTimeInMilliseconds != null && stateTimeInMilliseconds is Int64)
                                                                    {
                                                                        accumulatedCycleTimeForOpstep += (Int64)stateTimeInMilliseconds;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }

                                                //  6. Accumulate the amount of parts created within the time period
                                                if (containerDictionary.TryGetValue("Parts", out var partsForOpstep))
                                                {
                                                    foreach (var partInfo in partsForOpstep.Values)
                                                    {
                                                        if (partInfo.TryGetValue("Value", out var partCountValue))
                                                        {
                                                            if (partCountValue != null && partCountValue is Int64)
                                                            {
                                                                accumulatedPartsForOpstep += (Int64)partCountValue;
                                                            }
                                                        }
                                                    }
                                                }


                                            }
                                        }
                                    }

                                    //  7. Calculate the interim cycletime per part.
                                    //      Note: this is the cycletime based off of the current opstep running for the last 24 hours.
                                    var cycletimePast24Hours = accumulatedCycleTimeForOpstep / (accumulatedPartsForOpstep != 0 ? accumulatedPartsForOpstep : 1);
                                }
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        var iiii = 0;
                    }

                    // This request allows for more than one asset to be called for the same metric type, group and range
                    try
                    {
                        var metricRequest = new MetricsRequest()
                        {
                            AssetIds = new List<Guid>() { machine.Id },
                            MetricGroup = metricGroup,
                            MetricType = metricType,
                            StartDate = start.ToUnixTimestampInMilliseconds(),
                            EndDate = end.ToUnixTimestampInMilliseconds()

                        };
                        var metricsForOpstepEventsViaMetricRequest = await _client.GetFinalMetricsForRangeForAssets(metricRequest);
                        // note: if the metrics do not exist for this request, an eception will be thrown.  wrap in a try catch
                    }
                    catch (Exception ex)
                    {
                        var iiii = 0;
                    }
                }

                // helper functions
                // To get a list of all metric types:
                var metricTypes = await GetMetricTypes();

                // To get a list of all metric Groups: 
                var metricGroups = await GetMetricGroups();

                // To rebuild the metrics for an asset over an interval
                await _client.RebuildRangeByAsset(machine.Id, start, end);

                // To rebuild the metrics for an asset over an interval
                var metricTypeToRebuild = "MachineStateAndPart";
                await _client.RebuildRangeByAssetByMetric(machine.Id, metricTypeToRebuild, start, end);
            }
            // ---------------------------------------------------------------------------------------------
        }

        #endregion

        // ----------------------------------------------------------------------------------
    }
}
