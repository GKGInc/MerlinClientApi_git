using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public class AssetDataObject
    {
        public string AssetId { get; set; }   // Guid

        public string OpstepId { get; set; }  // Guid
        public string OperatorId { get; set; }// Guid
        public string StateId { get; set; }   // Guid
    }
}
