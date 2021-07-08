using System;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AngleSharp.Io;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.HealthCheck
{
    public class BotHealthCheckOptions : HealthCheckOptions
    {
        public BotHealthCheckOptions()
        {
            ResponseWriter = Writer;
        }

        private static Task Writer(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = MediaTypeNames.Application.Json;
            
            var options = new JsonWriterOptions
            {
                Indented = true
            };

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, options))
            {
                writer.WriteStartObject();
                writer.WriteString(nameof(report.Status), report.Status.ToString());
                    
                writer.WriteStartObject(nameof(report.Entries));
                
                foreach (var (k, entry) in report.Entries)
                {
                    writer.WriteStartObject(k);
                    writer.WriteString(nameof(report.Status), entry.Status.ToString());
                    writer.WriteString(nameof(entry.Description), entry.Description);
                    writer.WriteStartObject(nameof(entry.Data));
                    
                    foreach (var (key, value) in entry.Data)
                    {
                        writer.WritePropertyName(key);
                        JsonSerializer.Serialize(
                            writer, value, value.GetType());
                    }
                    writer.WriteEndObject();
                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            var json = Encoding.UTF8.GetString(stream.ToArray());
            return context.Response.WriteAsync(json);
        }
    }
}