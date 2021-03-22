using System;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Data.Analytics
{
    public class JsonSnakeCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            return string.Join('_', Regex.Split(name, @"(?<!^)(?=[A-Z])")).ToLower();
        } 
    }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RequestType
    {
        Encode,
        Decode
    }
    
    public class AnalyticsRequest
    {
        public Guid TextId { get; set; }

        public RequestType RequestType { get; set; }
        
        public AnaliticsResult AnaliticsResult { get; set; }
    }

    public class AnaliticsResult
    {
        public HttpStatusCode Code { get; set; }
    }
}