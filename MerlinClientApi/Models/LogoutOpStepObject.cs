using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public class LogoutOpStepObject
    {
        ///Logs the opstep that is currently assigned to the machine asset id.
        /// Note: jobState is an enumerated value where
        ///        Pending     = 0     Opstep is available for all machines.Product Standard dictate what machines this is capable on.
        ///        Queued      = 1     Opstep has been assigned to a specific machine.  Once it has been set to "Queued", other machines that are capable of running the Opstep can't see the opstep.
        ///        Running     = 2     This is the current running Opstep for the given machine asset.
        ///        Completed   = 3     The Opstep is considered to be completed.
        /// Note: queuedDate is the date where you would expect the Opstep to get rerun again.  The queuedDate should always be set to a valid value if JobState is either Pending or Queued.
        
        public int MachineNo { get; set; }
        public string AssetId { get; set; } //Guid
        public int JobState { get; set; }
        public DateTime QueuedDate { get; set; }
    }
}
