using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Api.Extensions;
using Bot;
using Bot.Interfaces;
using Bot.Services;
using Core.Interfaces;
using CsStg;
using Data;
using Data.Analytics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Npgsql;
using StackExchange.Redis;
using Telegram.Bot;

namespace Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Any, 0));
            });

            services.AddStackExchangeRedisCache(opt =>
            {
                opt.Configuration = Configuration["REDIS_CONNECTION_STRING"];
            });
            
            services.AddDataProtection()
                .PersistKeysToStackExchangeRedis(ConnectionMultiplexer.Connect(Configuration["REDIS_CONNECTION_STRING"]),
                    "DataProtection-Keys");
            
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                    options.JsonSerializerOptions.PropertyNamingPolicy = new JsonSnakeCaseNamingPolicy();
                });
            
            // var builder = new NpgsqlConnectionStringBuilder(Configuration["DATABASE_CONNECTION_STRING"]);

            services.AddDbContext<Context>(options =>
            {
                options.UseNpgsql(Configuration["DATABASE_CONNECTION_STRING"]);
                options.EnableSensitiveDataLogging();
            });
            
            services.AddRabbit(Configuration);
            
            services.AddSingleton<ITelegramBotClient>(provider => 
                new TelegramBotClient(Configuration["TELEGRAM_TOKEN"]));

            services.AddSingleton<AbstractEncoder, Lsb>();
            services.AddSingleton<AbstractEncoder, Kutter>();
            
            services.AddHostedService<QueuedHostedService>();
            services.AddSingleton<IBackgroundTaskQueue>(ctx => {
                if (!int.TryParse(Configuration["QueueCapacity"], out var queueCapacity))
                    queueCapacity = 100;
                return new TelegramTaskQueue(queueCapacity);
            });

            services.AddTransient<StateManager>();

            services.AddHostedService<TelegramConsumer>();

            services.AddHealthChecks()
                .AddNpgSql(Configuration["DATABASE_CONNECTION_STRING"])
                .AddRedis(Configuration["REDIS_CONNECTION_STRING"]);
            
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
                
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "JWT Authorization header using the Bearer scheme."
                });
                
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference 
                            { 
                                Type = ReferenceType.SecurityScheme, 
                                Id = "Bearer" 
                            }
                        },
                        new string[] {}

                    }
                });
                
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // app.UseRabbitListener();

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
            });

            app.UseStatusCodePages();
            
            app.UseRouting();
            
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}