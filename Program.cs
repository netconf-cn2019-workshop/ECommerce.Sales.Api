using ECommerce.Services.Common.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Reflection;
using System.Xml;

namespace ECommerce.Sales.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
               .MinimumLevel.Debug()
               .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
               .MinimumLevel.Override("System", LogEventLevel.Warning)
               .Enrich.FromLogContext()
               .WriteTo.Console()
               .CreateLogger();

            try
            {
                Log.Information("正在启动网站");
                BuildWebHost(args).Run();
                return;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "网站意外停止");
                return;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                   .ConfigureAppConfiguration((context, builder) =>
                   {
                       var orchestrator = context.Configuration["ORCHESTRATOR"];
                       builder.SetBasePath(Directory.GetCurrentDirectory());
                       builder.AddJsonFile($"appsettings.json", optional: false);
                       builder.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: false);
                       builder.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.{orchestrator}.json", optional: true);
                       builder.AddEnvironmentVariables();
                       builder.AddCloud();
                   })
                   .UseStartup<Startup>()
                   .UseSerilog()
                   .Build();
    }
}
