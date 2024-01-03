using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public class PartCountObject
    {
        //public Guid AssetId { get; set; }
        public string AssetId { get; set; }
        public int Count { get; set; }
        public bool IsReject { get; set; }
        //public Guid? ReasonId { get; set; }
        public string ReasonId { get; set; }
    }
}
