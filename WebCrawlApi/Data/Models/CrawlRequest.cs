namespace WebCrawlApi.Data.Models
{
    public class CrawlRequest
    {
        public List<string> Urls { get; set; }
        public int MaxDepth { get; set; }

    }
}
