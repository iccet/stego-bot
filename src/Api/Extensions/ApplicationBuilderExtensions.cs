#pragma warning disable 618
using Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseRabbitListener(this IApplicationBuilder app)
        {
            var consumer = app.ApplicationServices.GetRequiredService<IAnalyticsConsumer>();
            var lifetime = app.ApplicationServices.GetRequiredService<IApplicationLifetime>();
            lifetime.ApplicationStarted.Register(() => consumer.Subscribe());
            lifetime.ApplicationStopping.Register(() => consumer.Unsubscribe());
            return app;
        }
    }
}