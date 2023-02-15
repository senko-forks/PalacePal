using System;

namespace Pal.Client.DependencyInjection
{
    internal class DebugState
    {
        public string? DebugMessage { get; set; }

        public void SetFromException(Exception e)
            => DebugMessage = $"{DateTime.Now}\n{e}";

        public void Reset()
            => DebugMessage = null;
    }
}
