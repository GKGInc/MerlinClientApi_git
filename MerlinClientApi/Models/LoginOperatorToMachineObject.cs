using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public class LoginOperatorToMachineObject
    {
        public int MachineNo { get; set; }
        public string OperatorUserId { get; set; }
        public bool Login { get; set; }
    }
}
