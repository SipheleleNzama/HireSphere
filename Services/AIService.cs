using Azure.AI.TextAnalytics;
using Azure;

public class AIService
{
    private readonly TextAnalyticsClient _textAnalyticsClient;
    private readonly ILogger<AIService> _logger;

    public AIService(IConfiguration configuration, ILogger<AIService> logger)
    {
        var endpoint = configuration["AzureAI:Endpoint"];
        var key = configuration["AzureAI:Key"];
        _textAnalyticsClient = new TextAnalyticsClient(new Uri(endpoint), new AzureKeyCredential(key));
        _logger = logger;
    }

    public async Task<Dictionary<string, double>> ExtractKeyPhrasesAsync(string text)
    {
        try
        {
            var response = await _textAnalyticsClient.ExtractKeyPhrasesAsync(text);
            var phrases = response.Value;

            // Simple scoring - more frequent phrases get higher scores
            var phraseScores = phrases
                .GroupBy(p => p)
                .ToDictionary(g => g.Key, g => (double)g.Count() / phrases.Count());

            return phraseScores;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting key phrases");
            return new Dictionary<string, double>();
        }
    }

    public async Task<double> CalculateMatchScore(string jobDescription, string resumeText)
    {
        var jobPhrases = await ExtractKeyPhrasesAsync(jobDescription);
        var resumePhrases = await ExtractKeyPhrasesAsync(resumeText);

        double totalScore = 0;

        foreach (var phrase in jobPhrases)
        {
            if (resumePhrases.TryGetValue(phrase.Key, out var resumeScore))
            {
                totalScore += phrase.Value * resumeScore;
            }
        }

        // Normalize score to 0-100 range
        double maxPossibleScore = jobPhrases.Sum(p => p.Value);
        double normalizedScore = maxPossibleScore > 0 ? (totalScore / maxPossibleScore) * 100 : 0;

        return Math.Round(normalizedScore, 2);
    }
}