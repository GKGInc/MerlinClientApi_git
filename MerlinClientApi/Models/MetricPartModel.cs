using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerlinClientApi.Models
{
    public partial class MetricPartModelExt : Merlin.Platform.Standard.Data.Models.MetricPartModel
    {
        //public decimal MetricValue { get; set; }

        //public override AddExtraData(object otherExtraData);
        public override object AddValue(object other, object extraData, Merlin.Platform.Standard.Data.IStringBuilder logBuilder, bool realtime = false)
        {
            return null;
        }

    }
}
