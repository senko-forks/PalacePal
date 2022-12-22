using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pal.Client.Scheduled
{
    internal interface IQueueOnFrameworkThread
    {
        void Run(Plugin plugin, ref bool recreateLayout, ref bool saveMarkers);
    }
}
