using System.Linq;
using Dalamud.Interface.Windowing;

namespace Pal.Client.Extensions
{
    internal static class WindowSystemExtensions
    {
        public static T? GetWindow<T>(this WindowSystem windowSystem)
                where T : Window
        {
            return windowSystem.Windows.OfType<T>().FirstOrDefault();
        }
    }
}
