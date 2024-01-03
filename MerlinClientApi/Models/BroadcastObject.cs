using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public class BroadcastObject
    {
        public string sono { get; set; }
        public int opno { get; set; }
        public string sonoopno { get; set; }                    // sono+opno trimmmed
        public string department { get; set; }                  // data to add/update
        public string scheduled_status { get; set; }            // data to add/update
        public string workcenter { get; set; }                  // data to add/update
        public string previous_department { get; set; }         // data to remove
        public string previous_scheduled_status { get; set; }   // data to remove
        public string previous_workcenter { get; set; }         // data to remove
        public List<string> sonoopno_list { get; set; }         // batch sono+opno 
    }
}
