using System;

namespace Pal.Client.Configuration
{
    internal static class ConfigurationData
    {
        [Obsolete("for V1 import")]
        internal static readonly byte[] FixedV1Entropy = { 0x22, 0x4b, 0xe7, 0x21, 0x44, 0x83, 0x69, 0x55, 0x80, 0x38 };
    }
}
