using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public class CreateOpstepObject
    {
        public int MachineNo { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public string ProductDescription { get; set; }
        public string Category { get; set; }
        public int CycleTime { get; set; }
        public string WoName { get; set; }
        public string SalesOrder { get; set; }
        public string LineItemName { get; set; }
        public int NumberofParts { get; set; }
        public string Location { get; set; }
        public string CustomerPartNumber { get; set; }
    }
}
