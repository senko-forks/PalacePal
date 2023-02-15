using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Dalamud.Logging;

namespace Pal.Client.Configuration
{
    public class AccountConfigurationV7 : IAccountConfiguration
    {
        [JsonConstructor]
        public AccountConfigurationV7()
        {
        }

        public AccountConfigurationV7(string server, Guid accountId)
        {
            Server = server;
            EncryptedId = EncryptAccountId(accountId);
        }

        [Obsolete("for V1 import")]
        public AccountConfigurationV7(string server, string accountId)
        {
            Server = server;

            if (accountId.StartsWith("s:"))
                EncryptedId = accountId;
            else if (Guid.TryParse(accountId, out Guid guid))
                EncryptedId = EncryptAccountId(guid);
            else
                throw new InvalidOperationException("invalid account id format");
        }

        [JsonPropertyName("Id")]
        [JsonInclude]
        public string EncryptedId { get; private set; } = null!;

        public string Server { get; init; } = null!;

        [JsonIgnore] public bool IsUsable => DecryptAccountId(EncryptedId) != null;

        [JsonIgnore] public Guid AccountId => DecryptAccountId(EncryptedId) ?? throw new InvalidOperationException();

        public List<string> CachedRoles { get; set; } = new();

        private Guid? DecryptAccountId(string id)
        {
            if (Guid.TryParse(id, out Guid guid) && guid != Guid.Empty)
                return guid;

            if (!id.StartsWith("s:"))
                throw new InvalidOperationException("invalid prefix");

            try
            {
                byte[] guidBytes = ProtectedData.Unprotect(Convert.FromBase64String(id.Substring(2)),
                    ConfigurationData.Entropy, DataProtectionScope.CurrentUser);
                return new Guid(guidBytes);
            }
            catch (Exception e)
            {
                PluginLog.Verbose(e, $"Could not load account id {id}");
                return null;
            }
        }

        private string EncryptAccountId(Guid g)
        {
            try
            {
                byte[] guidBytes = ProtectedData.Protect(g.ToByteArray(), ConfigurationData.Entropy,
                    DataProtectionScope.CurrentUser);
                return $"s:{Convert.ToBase64String(guidBytes)}";
            }
            catch (Exception)
            {
                return g.ToString();
            }
        }

        public bool EncryptIfNeeded()
        {
            if (Guid.TryParse(EncryptedId, out Guid g))
            {
                string oldId = EncryptedId;
                EncryptedId = EncryptAccountId(g);
                return oldId != EncryptedId;
            }
            return false;
        }
    }
}
