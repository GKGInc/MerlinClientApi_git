using Merlin.Mes.Model.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public class WorkOrderExtObject
    {
        public WorkOrderModel WorkOrder { get; set; }
        public ProductStandardModel ProductStandard { get; set; }
        public ProductTemplateModel ProductProfile { get; set; }
        public List<PendingOpStepsModel> OpStepList { get; set; }
        public PendingOpStepsModel OpStep { get; set; }
    }
}
