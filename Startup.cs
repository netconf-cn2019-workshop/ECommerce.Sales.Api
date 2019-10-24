﻿using Autofac;
using Autofac.Core;
using Autofac.Extensions.DependencyInjection;
using CorrelationId;
using ECommerce.Common.Infrastructure.Messaging;
using ECommerce.Sales.Api.Configuration;
using ECommerce.Sales.Api.Model;
using ECommerce.Sales.Api.Modules;
using ECommerce.Sales.Api.Services;
using MassTransit;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace ECommerce.Sales.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IContainer Container { get; private set; }

        public IConfiguration Configuration { get; }

        public bool UseCloudServices => Configuration.GetValue<bool>("UseCloudServices");

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();

            var healthCheckBuilder = services.AddHealthChecks()
                .AddSqlServer(Configuration["ConnectionStrings:SalesDb"], tags: new[] { "db", "sql" });
                
            if (UseCloudServices)
            {
                healthCheckBuilder
                    .AddAzureServiceBusQueue(Configuration["Brokers:ServiceBus:Url"], "sales_fanout", name: "sales_fanout_queue", tags: new[] { "broker" })
                    .AddAzureServiceBusQueue(Configuration["Brokers:ServiceBus:Url"], "sales_submit_orders", name: "sales_submit_orders_queue", tags: new[] { "broker" });
            }
            else
            {
                healthCheckBuilder
                    .AddRabbitMQ(Configuration["Brokers:RabbitMQ:Url"], tags: new[] { "broker" });
            }

            services.AddEntityFrameworkSqlServer()
                    .AddDbContext<SalesContext>(options =>
                    {
                        options.UseSqlServer(Configuration["ConnectionStrings:SalesDb"],
                            sqlServerOptionsAction: sqlOptions =>
                            {
                                sqlOptions.EnableRetryOnFailure(maxRetryCount: 10, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
                            });
                    },
                        ServiceLifetime.Scoped  //Showing explicitly that the DbContext is shared across the HTTP request scope (graph of objects started in the HTTP request)
                    );

            services.AddTransient<CorrelationIdDelegatingHandler>();
            services.AddHttpClient("DefaultClient")
                .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();

            services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
            services.AddApplicationInsightsTelemetry(Configuration["ApplicationInsights:InstrumentationKey"]);
            services.AddCorrelationId();
            services.AddHostedService<SalesService>();

            var builder = new ContainerBuilder();

            builder.Populate(services);
            builder.RegisterModule(GetBusModule());
            builder.RegisterModule<ConsumerModule>();
            builder.RegisterType<DataService>().As<IDataService>().SingleInstance();

            Container = builder.Build();

            return new AutofacServiceProvider(Container);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime lifetime, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHealthChecks("/health/live", new HealthCheckOptions()
            {
                Predicate = p => p.Tags.Count == 0
            });
            app.UseHealthChecks("/health/ready", new HealthCheckOptions()
            {
                Predicate = p => p.Tags.Count > 0
            });

            app.UseCorrelationId(new CorrelationIdOptions
            {
                UpdateTraceIdentifier = false,
                UseGuidForCorrelationId = true
            });
            app.UseMvc();
        }

        private IModule GetBusModule()
        {
            return  UseCloudServices ?
                (IModule) new AzureBusModule() : new BusModule();
        }
    }
}
