using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HireSphere.Models.AI;

namespace HireSphere.Services
{
    public class AIService
    {
        private readonly ILogger<AIService> _logger;
        private readonly double _minimumSkillScore;

        public AIService(ILogger<AIService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _minimumSkillScore = configuration.GetValue<double>("AI:MinimumSkillScore", 0.1);
        }

        public async Task<Dictionary<string, double>> ExtractKeyPhrasesAsync(string text)
        {
            // Mock implementation - in production, replace with actual AI service
            if (string.IsNullOrWhiteSpace(text))
                return new Dictionary<string, double>();

            var words = text.Split(new[] { ' ', '.', ',', ';', '!', '?' },
                                StringSplitOptions.RemoveEmptyEntries)
                          .Where(w => w.Length > 3)
                          .Select(w => w.ToLower());

            var totalWords = words.Count();
            return words
                .GroupBy(w => w)
                .ToDictionary(g => g.Key, g => (double)g.Count() / totalWords)
                .Where(p => p.Value >= _minimumSkillScore)
                .ToDictionary(p => p.Key, p => p.Value);
        }

        public async Task<MatchAnalysisResult> AnalyzeJobMatch(string jobDescription, string resumeText)
        {
            try
            {
                // Extract skills from both documents
                var jobSkillsTask = ExtractKeyPhrasesAsync(jobDescription);
                var resumeSkillsTask = ExtractKeyPhrasesAsync(resumeText);
                await Task.WhenAll(jobSkillsTask, resumeSkillsTask);

                var jobSkills = await jobSkillsTask;
                var resumeSkills = await resumeSkillsTask;

                // Calculate match percentage
                var matchingSkills = jobSkills.Keys
                    .Intersect(resumeSkills.Keys)
                    .ToList();

                var matchPercentage = jobSkills.Count > 0
                    ? (double)matchingSkills.Count / jobSkills.Count * 100
                    : 0;

                return new MatchAnalysisResult
                {
                    MatchPercentage = Math.Round(matchPercentage, 2),
                    MatchingSkills = matchingSkills,
                    MissingSkills = jobSkills.Keys.Except(resumeSkills.Keys).ToList(),
                    AdditionalSkills = resumeSkills.Keys.Except(jobSkills.Keys).ToList()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing job match");
                throw;
            }
        }
    }
}