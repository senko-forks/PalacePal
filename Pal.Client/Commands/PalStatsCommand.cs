using System;
using Pal.Client.DependencyInjection;

namespace Pal.Client.Commands
{
    internal sealed class PalStatsCommand
    {
        private readonly StatisticsService _statisticsService;

        public PalStatsCommand(StatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
        }

        public void Execute()
            => _statisticsService.ShowGlobalStatistics();
    }
}
