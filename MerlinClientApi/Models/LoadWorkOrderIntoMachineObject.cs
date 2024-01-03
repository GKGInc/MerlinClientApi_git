using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public class LoadWorkOrderIntoMachineObject
    {
        public string woName { get; set; }
        public int opstep { get; set; }
        //public string opstepName { get; set; }
        public int machineNo { get; set; }
    }
}
