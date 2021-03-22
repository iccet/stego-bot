using System;
using System.Text.Json.Serialization;

namespace Data.Analytics
{
    public class AnalyticsDto<T>
    {
        public RequestType RequestType { get; set; }
        
        public Guid TextId { get; set; }
        
        [JsonPropertyName("text")]
        public T Text { get; set; }

        public AnalyticsDto(RequestType requestType, T text)
        {
            RequestType = requestType;
            TextId = Guid.NewGuid();
            Text = text;
        }
    }
}