namespace KaraokeApp.Models
{
    public class UploadResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? DownloadUrl { get; set; }
        public string? FileName { get; set; }
    }
}