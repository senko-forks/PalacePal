namespace Pal.Client.Scheduled
{
    internal class QueuedConfigUpdate : IQueueOnFrameworkThread
    {
        public void Run(Plugin plugin, ref bool recreateLayout, ref bool saveMarkers)
        {
            if (Service.Configuration.Mode == Configuration.EMode.Offline)
            {
                LocalState.UpdateAll();
                plugin.FloorMarkers.Clear();
                plugin.EphemeralMarkers.Clear();
                plugin.LastTerritory = 0;

                recreateLayout = true;
                saveMarkers = true;
            }
        }
    }
}
