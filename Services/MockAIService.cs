using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace HireSphere.Services
{
    public class MockAIService 
    {
        private readonly ILogger<MockAIService> _logger;
        private readonly double _minimumPhraseScore;

        public MockAIService(IConfiguration configuration, ILogger<MockAIService> logger)
        {
            _logger = logger;
            _minimumPhraseScore = configuration.GetValue<double>("AzureAI:MinimumPhraseScore", 0.05);
        }

        public async Task<Dictionary<string, double>> ExtractKeyPhrasesAsync(string text)
        {
            await Task.Delay(100); // Simulate processing time

            if (string.IsNullOrWhiteSpace(text))
            {
                return new Dictionary<string, double>();
            }

            // Mock implementation - extract words as "key phrases"
            var words = text.Split(new[] { ' ', '.', ',', ';', '!', '?' },
                                 StringSplitOptions.RemoveEmptyEntries)
                           .Where(w => w.Length > 3)
                           .Select(w => w.ToLower());

            var totalWords = words.Count();
            var phraseScores = words
                .GroupBy(w => w)
                .ToDictionary(
                    g => g.Key,
                    g => (double)g.Count() / totalWords)
                .Where(p => p.Value >= _minimumPhraseScore)
                .ToDictionary(p => p.Key, p => p.Value);

            return phraseScores;
        }

        public async Task<double> CalculateMatchScore(string jobDescription, string resumeText)
        {
            await Task.Delay(200); // Simulate processing time

            // Simple mock implementation - count matching words
            var jobWords = (await ExtractKeyPhrasesAsync(jobDescription)).Keys;
            var resumeWords = (await ExtractKeyPhrasesAsync(resumeText)).Keys;

            if (!jobWords.Any()) return 0;

            var matchingWords = jobWords.Intersect(resumeWords).Count();
            var score = (double)matchingWords / jobWords.Count() * 100;

            return Math.Round(Math.Min(score, 100), 2); // Cap at 100
        }

        public async Task<Dictionary<string, string>> AnalyzeResume(string resumeText)
        {
            await Task.Delay(300); // Simulate processing time

            var keyPhrases = await ExtractKeyPhrasesAsync(resumeText);
            var topSkills = keyPhrases.OrderByDescending(kp => kp.Value)
                                     .Take(5)
                                     .Select(kp => kp.Key);

            return new Dictionary<string, string>
            {
                ["Key Skills"] = string.Join(", ", topSkills),
                ["Language"] = "English (Confidence: 95%)",
                ["Sentiment"] = "Positive (Confidence: 80%)",
                ["Detected Skills"] = string.Join(", ", topSkills.Take(3))
            };
        }
    }
}