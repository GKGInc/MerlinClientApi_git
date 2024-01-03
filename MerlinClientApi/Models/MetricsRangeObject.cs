using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public class MetricsRangeObject
    {
        public string AssetId { get; set; } // Guid
        public string MetricType { get; set; }
        public string MetricGroup { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        //public string StartDate { get; set; }
        //public string EndDate { get; set; }
    }
}
