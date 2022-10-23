using Dalamud.Interface.Windowing;
using System.Linq;

namespace Pal.Client
{
    internal static class WindowSystemExtensions
    {
        public static T GetWindow<T>(this WindowSystem windowSystem)
                where T : Window
        {
            return windowSystem.Windows.Select(w => w as T).FirstOrDefault(w => w != null);
        }
    }
}
