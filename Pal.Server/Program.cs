using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pal.Server.Services;

namespace Pal.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddCustomConfiguration();
            builder.Services.AddGrpc(o => o.EnableDetailedErrors = true);
            builder.Services.AddDbContext<PalContext>(o =>
            {
                if (builder.Configuration["DataDirectory"] is string dbPath)
                {
                    dbPath += "/palace-pal.db";
                }
                else
                {
#if DEBUG
                    dbPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "pal.db");
#else
                    dbPath = "palace-pal.db";
#endif
                }
                o.UseSqlite($"Data Source={dbPath}");
            });
            builder.Services.AddHostedService<RemoveIpHashService>();
            builder.Services.AddSingleton<PalaceLocationCache>();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration.GetOrThrow("JWT:Issuer"),
                    ValidAudience = builder.Configuration.GetOrThrow("JWT:Audience"),
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(builder.Configuration.GetOrThrow("JWT:Key"))),
                };
            });
            builder.Services.AddAuthorization();

            if (builder.Configuration["DataDirectory"] is string dataDirectory)
            {
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(dataDirectory));
            }

            builder.Host.UseSystemd();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGrpcService<AccountService>();
            app.MapGrpcService<PalaceService>();
            app.MapGrpcService<ExportService>();

            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PalContext>();
                dbContext.Database.Migrate();
            }

            await app.RunAsync();
        }
    }

    internal class CustomConfigurationProvider : ConfigurationProvider
    {
        private readonly string dataDirectory;

        public CustomConfigurationProvider(string dataDirectory)
        {
            this.dataDirectory = dataDirectory;
        }

        public override void Load()
        {
            var jwtKeyPath = Path.Join(dataDirectory, "jwt.key");
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
            var dataDirectory = tempConfig["DataDirectory"] as string;
            if (dataDirectory != null)
                return builder.Add(new CustomConfigurationSource(dataDirectory));
            else
                return builder;
        }
    }
}