using System;
using System.Text.Json.Serialization;

namespace Data.Analytics
{
    public class EncodingDto<T>
    {
        public RequestType RequestType { get; set; }
        
        public Guid TextId { get; set; }
        
        [JsonPropertyName("text")]
        public T Text { get; set; }

        public EncodingDto(RequestType requestType, T text)
        {
            RequestType = requestType;
            TextId = Guid.NewGuid();
            Text = text;
        }
    }
}