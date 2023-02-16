using System.Reflection.Metadata;
using Dalamud.Logging;
using Microsoft.Extensions.Logging;

namespace Pal.Client.Scheduled
{
    internal interface IQueueOnFrameworkThread
    {
        internal interface IHandler
        {
            void RunIfCompatible(IQueueOnFrameworkThread queued, ref bool recreateLayout, ref bool saveMarkers);
        }

        internal abstract class Handler<T> : IHandler
            where T : IQueueOnFrameworkThread
        {
            protected readonly ILogger<Handler<T>> _logger;

            protected Handler(ILogger<Handler<T>> logger)
            {
                _logger = logger;
            }

            protected abstract void Run(T queued, ref bool recreateLayout, ref bool saveMarkers);

            public void RunIfCompatible(IQueueOnFrameworkThread queued, ref bool recreateLayout, ref bool saveMarkers)
            {
                if (queued is T t)
                {
                    _logger.LogInformation("Handling {QueuedType}", queued.GetType());
                    Run(t, ref recreateLayout, ref saveMarkers);
                }
                else
                {
                    _logger.LogError("Could not use queue handler {QueuedType}", queued.GetType());
                }
            }
        }
    }
}
