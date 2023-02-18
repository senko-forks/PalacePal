using ECommons.Schedulers;
using Pal.Client.Windows;

namespace Pal.Client.Commands
{
    internal sealed class PalTestConnectionCommand
    {
        private readonly ConfigWindow _configWindow;

        public PalTestConnectionCommand(ConfigWindow configWindow)
        {
            _configWindow = configWindow;
        }

        public void Execute()
        {
            var _ = new TickScheduler(() => _configWindow.TestConnection());
        }
    }
}
