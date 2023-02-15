using Dalamud.Logging;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace Pal.Client.Configuration
{
    internal static class ConfigurationData
    {
        [Obsolete("for V1 import")]
        internal static readonly byte[] FixedV1Entropy = { 0x22, 0x4b, 0xe7, 0x21, 0x44, 0x83, 0x69, 0x55, 0x80, 0x38 };

        private static bool? _supportsDpapi = null;
        public static bool SupportsDpapi
        {
            get
            {
                if (_supportsDpapi == null)
                {
                    try
                    {
                        byte[] input = RandomNumberGenerator.GetBytes(32);
                        byte[] entropy = RandomNumberGenerator.GetBytes(16);
                        byte[] temp = ProtectedData.Protect(input, entropy, DataProtectionScope.CurrentUser);
                        byte[] output = ProtectedData.Unprotect(temp, entropy, DataProtectionScope.CurrentUser);
                        _supportsDpapi = input.SequenceEqual(output);
                    }
                    catch (Exception)
                    {
                        _supportsDpapi = false;
                    }

                    PluginLog.Verbose($"DPAPI support: {_supportsDpapi}");
                }
                return _supportsDpapi.Value;
            }
        }
    }
}
