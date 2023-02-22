using Dalamud.Interface.Windowing;
using Pal.Client.Configuration;
using Pal.Client.Windows;

namespace Pal.Client.Commands
{
    internal class PalConfigCommand
    {
        private readonly IPalacePalConfiguration _configuration;
        private readonly AgreementWindow _agreementWindow;
        private readonly ConfigWindow _configWindow;

        public PalConfigCommand(
            IPalacePalConfiguration configuration,
            AgreementWindow agreementWindow,
            ConfigWindow configWindow)
        {
            _configuration = configuration;
            _agreementWindow = agreementWindow;
            _configWindow = configWindow;
        }

        public void Execute()
        {
            if (_configuration.FirstUse)
                _agreementWindow.IsOpen = true;
            else
                _configWindow.Toggle();
        }
    }
}
