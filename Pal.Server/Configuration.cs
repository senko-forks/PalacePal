namespace Pal.Server
{
    internal class CustomConfigurationProvider : ConfigurationProvider
    {
        private readonly string _dataDirectory;

        public CustomConfigurationProvider(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
        }

        public override void Load()
        {
            var jwtKeyPath = Path.Join(_dataDirectory, "jwt.key");
            if (File.Exists(jwtKeyPath))
                Data["JWT:Key"] = File.ReadAllText(jwtKeyPath);
        }
    }

    internal class CustomConfigurationSource : IConfigurationSource
    {

        private readonly string dataDirectory;

        public CustomConfigurationSource(string dataDirectory)
        {
            this.dataDirectory = dataDirectory;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder) =>
            new CustomConfigurationProvider(dataDirectory);
    }

    internal static class ConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddCustomConfiguration(this IConfigurationBuilder builder)
        {
            var tempConfig = builder.Build();
            if (tempConfig["DataDirectory"] is string dataDirectory)
                return builder.Add(new CustomConfigurationSource(dataDirectory));
            else
                return builder;
        }
    }
}
