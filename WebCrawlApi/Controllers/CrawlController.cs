using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WebCrawlApi.Data.Models;

namespace WebCrawlApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CrawlController : ControllerBase
    {
        private static Dictionary<int, Dictionary<string, string>> crawlResults = new Dictionary<int, Dictionary<string, string>>();
        private static int currentCrawlId = 1;
        private readonly ILogger<CrawlController> _logger;
        private readonly IMemoryCache _cache;
        HashSet<string> visitedUrls = new HashSet<string>();
        public CrawlController(ILogger<CrawlController> logger,IMemoryCache cache)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache?? throw new ArgumentNullException(nameof(cache));
        }
        [HttpPost]
        public async Task<ActionResult<int>> StartCrawl([FromBody] CrawlRequest request)
        {
            try
            {
                var result = new Dictionary<string, string>();
                // Validate request
                if (request == null || request.Urls == null || request.Urls.Count == 0 || request.MaxDepth <= 0)
                {
                    return BadRequest("Invalid request format");
                }
                foreach (var Url in request.Urls)
                {
                  await Crawl(Url, request.MaxDepth, result,visitedUrls);
                }
              
                //Store results in-memory cache
                _cache.Set(currentCrawlId, result);

                _logger.LogInformation($"Crawl completed for ID:{currentCrawlId}");
                return currentCrawlId++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing crawl request");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpGet("{crawlId}")]
        public ActionResult<Dictionary<string, string>> GetCrawlResult(int crawlId)
        {
            try
            {     
                if (_cache.TryGetValue(crawlId, out var result))
                {
                        return Ok(result);
                }
                else
                {
                    _logger.LogWarning($"Crawl result not found for id :{crawlId}");
                    return NotFound("Crawl result not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting crawl result for id {crawlId}");
                return StatusCode(500, "Internal Server Error");
            }

        }
        [HttpGet("pagerelations")]
        public async Task<ActionResult<Dictionary<string, string>>> GetPageRelations([FromQuery] string url, [FromQuery] int maxDepth)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                { return BadRequest("URL cannot be empty"); }

                _logger.LogInformation($"Fetching page relations for URL: {url}, Max Depth: {maxDepth}");

                var result = new Dictionary<string, string>();
                await Crawl(url, maxDepth, result,visitedUrls);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching page relations: {ex.Message}");
                return StatusCode(500, "Internal Server Error");
            }
        }

        private async Task Crawl(string url, int maxDepth, Dictionary<string, string> result,HashSet<string> visitedUrls, int currentDepth = 0)
        {
            if (currentDepth > maxDepth || visitedUrls.Contains(url))
            {
                return ; // Stop traversal if max depth is reached or if the URL has been visited.
            }
            visitedUrls.Add(url); // Mark the current URL as visited.
            try
            {
                var web = new HtmlWeb();
                var doc = web.Load(url);
                var anchorTags = doc.DocumentNode.SelectNodes("//a[@href]");
                if (anchorTags != null)
                {
                    foreach (var anchor in anchorTags)
                    {
                        var href = anchor.GetAttributeValue("href", string.Empty);
                        var title = anchor.InnerText.Trim();

                        if (!string.IsNullOrEmpty(href) && !result.ContainsKey(href))
                        {
                            if (href.Contains("https://") || href.Contains("http://"))
                            {
                                result.Add(href, title);
                                await Crawl(href, maxDepth, result, visitedUrls, currentDepth + 1);
                            }
                            
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (e.g., invalid URL, network issues, etc.)
                Console.WriteLine($"Error crawling {url}: {ex.Message}");
            }
        }
    }

    
}
