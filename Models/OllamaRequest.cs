namespace NongTimeAI.Models;

public class OllamaRequest
{
    public string Model { get; set; } = "llama3.2";
    public string Prompt { get; set; } = string.Empty;
    public bool Stream { get; set; } = false;
    public OllamaOptions? Options { get; set; }
}

public class OllamaOptions
{
    public double Temperature { get; set; } = 0.1;
    public double TopP { get; set; } = 0.1;
}
