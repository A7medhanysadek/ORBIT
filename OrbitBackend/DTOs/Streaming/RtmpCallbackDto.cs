using Microsoft.AspNetCore.Mvc;

namespace OrbitBackend.DTOs.Streaming
{
    
    
    
    
    
    public class RtmpCallbackDto
    {
        
        
        
        [FromForm(Name = "app")]
        public string? App { get; set; }

        
        
        
        [FromForm(Name = "name")]
        public string? Name { get; set; }

        
        
        
        [FromForm(Name = "tcurl")]
        public string? TcUrl { get; set; }

        
        
        
        [FromForm(Name = "addr")]
        public string? Addr { get; set; }

        
        
        
        [FromQuery(Name = "secret")]
        public string? Secret { get; set; }

        /// <summary>
        /// Sent by nginx on_record_done — the full file path of the recorded stream.
        /// </summary>
        [FromForm(Name = "path")]
        public string? Path { get; set; }
    }

    public class MediaServerMergeResponse
    {
        public bool Success { get; set; }
        public string? MergedFileName { get; set; }
        public string? MergedUrl { get; set; }
        public long Size { get; set; }
        public int FileCount { get; set; }
        public string? Error { get; set; }
    }
}
