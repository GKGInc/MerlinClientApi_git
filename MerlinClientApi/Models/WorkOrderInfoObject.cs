using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public class WorkOrderInfoObject
    {
        public string WorkOrderName { get; set; }
        public string OpStepName { get; set; }
        public string WorkOrderLineItemName { get; set; }
    }
}
