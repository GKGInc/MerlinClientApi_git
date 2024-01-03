using Memex.Merlin.Client;
using Merlin.Mes.Engine.Metrics;
using Merlin.Mes.Model.Models;
using Merlin.Mes.Model;
using Merlin.Platform.Common;
using Merlin.Platform.Standard.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Classes
{
    public class ApiTestRoutines
    {
        private MerlinClient _client;

        #region Api Test Routine Function

        // ----------------------------------------------------------------------------------
        public async Task ApiTestRoutine(MerlinClientApiSDK merlinClientApiSDK)
        {
            // ---------------------------------------------------------------------------------------------
            // Recall the list of Machines
            var machines = await merlinClientApiSDK.GetMachineAssets();
            var operators = await merlinClientApiSDK.GetOperatorAssets();

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
                await merlinClientApiSDK.LoginOperator(machineId, operatorId);
            }

            //// Log OUT an Operator to a machine
            //// 1. Determine what the Machine Id you want to log the Operator to log into
            //// 2. Request to logout the operator to the machine
            if (machines.Count != 0)
            {
                var machineId = machines[0].Id;
                await merlinClientApiSDK.LogoutOperator(machineId);
            }
            //// ---------------------------------------------------------------------------------------------
            //// Generate a Part Count against a Machine Asset.
            if (machines.Count != 0 && operators.Count != 0)
            {
                // generate a positive good part count against an asset
                var machineId = machines[0].Id; // Set to a specific machine id
                var partCount = 1;
                await merlinClientApiSDK.RequestPartCount(machineId, partCount, false, null);

                // generate a negative good part count against an asset
                machineId = machines[0].Id; // Set to a specific machine id
                partCount = -1;
                await merlinClientApiSDK.RequestPartCount(machineId, partCount, false, null);

                var rejectReasons = merlinClientApiSDK.GetRejectStateAssociationsForAsset(machineId);
                if (rejectReasons != null)
                {
                    // Note: If the ReasonId is left blank or is an empty guid, then the reject will not be entered into Merlin.  ReasonId needs to be valid.

                    // generate a positive reject part count against an asset
                    // adds a reject part count to a reason 
                    machineId = machines[0].Id; // Set to a specific machine id
                    partCount = 1;
                    var rejectReasonId = rejectReasons.Result[0].RejectDefinitionId; // Select an appropriate reason code as indicated int he reject reasons that has been assoicated to the machine
                    await merlinClientApiSDK.RequestPartCount(machineId, partCount, true, rejectReasonId);

                    // generate a negative good part count against an asset
                    // removes a reject part count to a reason 
                    machineId = machines[0].Id; // Set to a specific machine id
                    partCount = -1;
                    rejectReasonId = rejectReasons.Result[0].RejectDefinitionId; // Select an appropriate reason code as indicated int he reject reasons that has been assoicated to the machine
                    await merlinClientApiSDK.RequestPartCount(machineId, partCount, true, rejectReasonId);

                }
            }
            //// ---------------------------------------------------------------------------------------------
            // Change Machine state for a Machine Asset 
            if (machines.Count != 0)
            {
                var machineId = machines[0].Id; // select the appropriate Machine's Id
                var assocStates = await merlinClientApiSDK.GetMachineStateAssociationsForAsset(machineId); // this call is meant to show what associated states are available.
                if (assocStates.Count != 0)
                {
                    var assocState = assocStates[0];
                    await merlinClientApiSDK.SetMachineStateForAsset(machineId, assocState.StateDefinition.Id); // this will handle the modal vs non modal condition                     
                }

                // return the machine state
                // the call will use the appropriate call depending on the current machine state.
                await merlinClientApiSDK.ReturnMachineControlForAsset(machineId);
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

            await merlinClientApiSDK.SaveProductStandard(prodStd, "", "");

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
            var testWO = await _client.SaveWorkOrder(workOrder);


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
            var opstep = await merlinClientApiSDK.GetPendingOpStepByWorkOrderNameAndOpstepName(woName, opstepName);
            if (opstep != null)
            {
                var machineId = machines[0].Id; // insert one of the valid machine asset Ids
                var opstepId = opstep.Id;
                await merlinClientApiSDK.RunOpstepOnMachineAssetNow(machineId, opstepId);
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
                    await merlinClientApiSDK.LogoutOpStep(machineId, jobState, queuedDate);
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
                var metricTypes = await merlinClientApiSDK.GetMetricTypes();

                // To get a list of all metric Groups: 
                var metricGroups = await merlinClientApiSDK.GetMetricGroups();

                // To rebuild the metrics for an asset over an interval
                await _client.RebuildRangeByAsset(machine.Id, start, end);

                // To rebuild the metrics for an asset over an interval
                var metricTypeToRebuild = "MachineStateAndPart";
                await _client.RebuildRangeByAssetByMetric(machine.Id, metricTypeToRebuild, start, end);
            }
            // ---------------------------------------------------------------------------------------------
        }

        #endregion
    }
}
