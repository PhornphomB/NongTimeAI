using Microsoft.AspNetCore.Mvc;

namespace NongTimeAI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly IConfiguration _configuration;

    public HealthController(ILogger<HealthController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0",
            service = "NongTimeAI"
        });
    }

    [HttpGet("ollama")]
    public async Task<IActionResult> CheckOllama()
    {
        try
        {
            var ollamaBaseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            var response = await httpClient.GetAsync($"{ollamaBaseUrl}/api/tags");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return Ok(new
                {
                    status = "healthy",
                    ollama_url = ollamaBaseUrl,
                    timestamp = DateTime.UtcNow,
                    message = "Ollama is reachable",
                    models = content
                });
            }

            return StatusCode(503, new
            {
                status = "unhealthy",
                ollama_url = ollamaBaseUrl,
                timestamp = DateTime.UtcNow,
                message = "Ollama returned non-success status",
                statusCode = (int)response.StatusCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Ollama");
            return StatusCode(503, new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow,
                message = "Cannot connect to Ollama",
                error = ex.Message
            });
        }
    }
}
