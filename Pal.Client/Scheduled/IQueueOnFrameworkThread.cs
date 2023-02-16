using Dalamud.Logging;

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
            protected abstract void Run(T queued, ref bool recreateLayout, ref bool saveMarkers);

            public void RunIfCompatible(IQueueOnFrameworkThread queued, ref bool recreateLayout, ref bool saveMarkers)
            {
                if (queued is T t)
                {
                    PluginLog.Information($"Handling {queued.GetType()} with handler {GetType()}");
                    Run(t, ref recreateLayout, ref saveMarkers);
                }
                else
                {
                    PluginLog.Error($"Could not use queue handler {GetType()} with type {queued.GetType()}");
                }
            }
        }
    }
}
