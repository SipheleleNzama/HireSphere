using Microsoft.AspNetCore.Mvc;
using HireSphere.Services;
using HireSphere.Models;
using System.Threading.Tasks;
using HireSphere.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HireSphere.Controllers
{
    public class AnalysisController : Controller
    {
        private readonly AIService _aiService;
        private readonly HireSphereDbContext _context;

        public AnalysisController(AIService aiService, HireSphereDbContext context)
        {
            _aiService = aiService;
            _context = context;
        }

        // GET: Analysis/Upload
        public IActionResult Upload()
        {
            return View();
        }

        // POST: Analysis/Upload
        [HttpPost]
        public async Task<IActionResult> Upload(string jobDescription, IFormFile resumeFile)
        {
            if (resumeFile == null || resumeFile.Length == 0)
            {
                ModelState.AddModelError("", "Please upload a resume file");
                return View();
            }

            // Read the resume text
            using var reader = new StreamReader(resumeFile.OpenReadStream());
            var resumeText = await reader.ReadToEndAsync();

            // Analyze with your AI service
            var result = await _aiService.AnalyzeJobMatch(jobDescription, resumeText);

            // Save to database (example - adjust to your model)
            var analysis = new CandidateAnalysis
            {
                Name = resumeFile.FileName,
                UploadDate = DateTime.Now,
                MatchPercentage = result.MatchPercentage,
                Skills = string.Join(", ", result.MatchingSkills)
            };

            _context.CandidateAnalyses.Add(analysis);
            await _context.SaveChangesAsync();

            return RedirectToAction("Results");
        }

        // GET: Analysis/Results
        public IActionResult Results()
        {
            var topCandidates = _context.CandidateAnalyses
                .OrderByDescending(c => c.MatchPercentage)
                .Take(10)
                .ToList();

            return View(topCandidates);
        }
    }
}