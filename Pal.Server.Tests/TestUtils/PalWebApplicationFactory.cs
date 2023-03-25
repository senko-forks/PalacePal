extern alias PalServer;
using System;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PalServer::Pal.Server.Database;

namespace Pal.Server.Tests.TestUtils
{
    public sealed class PalWebApplicationFactory<TProgram>
        : WebApplicationFactory<TProgram>
        where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // ensure we open a connection by default, because each open/close by EF core would reset it
                services.Remove(services.Single(d => d.ServiceType == typeof(DbContextOptions<PalServerContext>)));
                services.AddSingleton<DbConnection>(_ =>
                {
                    var connection = new SqliteConnection("DataSource=:memory:");
                    connection.Open();

                    return connection;
                });
                services.AddDbContext<PalServerContext>((sp, o) =>
                {
                    o.UseSqlite(sp.GetRequiredService<DbConnection>());
                    o.AddInterceptors(new PalConnectionInterceptor());
                });

                // make sure we have an IP for each request
                services.AddSingleton<IStartupFilter, TestEnvironmentStartupFilter>();
            });

            builder.UseEnvironment("Development");
        }

        public sealed class TestEnvironmentStartupFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return app =>
                {
                    app.UseMiddleware<FakeRemoteIpMiddleware>();
                    next(app);
                };
            }
        }

        public sealed class FakeRemoteIpMiddleware
        {
            private readonly RequestDelegate _next;

            public FakeRemoteIpMiddleware(RequestDelegate next)
            {
                _next = next;
            }

            public async Task InvokeAsync(HttpContext httpContext)
            {
                httpContext.Connection.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
                await _next(httpContext);
            }
        }
    }
}
