using HireSphere.Data;
using HireSphere.Models;
using HireSphere.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireSphere.Controllers
{
    
    public class CandidatesController : Controller
    {
        private readonly HireSphereDbContext _context;
        private readonly FileUploadService _fileUploadService;
        private readonly AIService _aiService;
       

        public CandidatesController(
            HireSphereDbContext context,
            FileUploadService fileUploadService,
            AIService aiService)
          
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _aiService = aiService;
            
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int jobId, IFormFile resume, Candidate candidate)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Save candidate
                    _context.Candidates.Add(candidate);
                    await _context.SaveChangesAsync();

                    // Upload resume
                    var resumePath = await _fileUploadService.UploadResumeAsync(resume, candidate.Id);
                    candidate.ResumePath = resumePath;
                    _context.Update(candidate);

                    // Extract skills from resume
                    var resumeText = await _fileUploadService.ExtractTextFromResumeAsync(resumePath);
                    candidate.Skills = string.Join(", ", (await _aiService.ExtractKeyPhrasesAsync(resumeText)).Keys);

                    // Create application
                    var job = await _context.JobPostings.FindAsync(jobId);
                    if (job == null)
                    {
                        return NotFound();
                    }

                    var application = new Application
                    {
                        JobPostingId = jobId,
                        CandidateId = candidate.Id,
                        ApplicationDate = DateTime.Now,
                        Status = ApplicationStatus.Submitted,
                        MatchScore = (decimal)await _aiService.CalculateMatchScore(job.Description + " " + job.Requirements, resumeText)
                    };

                    _context.Applications.Add(application);
                    await _context.SaveChangesAsync();

                    return RedirectToAction("ApplicationSuccess", new { applicationId = application.Id });
                }

                return RedirectToAction("Details", "JobPostings", new { id = jobId });
            }
            catch (Exception ex)
            {
                
                ModelState.AddModelError("", "There was an error processing your application. Please try again.");
                return RedirectToAction("Details", "JobPostings", new { id = jobId });
            }
        }

        public async Task<IActionResult> View(int id)
        {
            var candidate = await _context.Candidates
                .Include(c => c.Applications)
                .ThenInclude(a => a.JobPosting)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (candidate == null)
            {
                return NotFound();
            }

            return View(candidate);
        }
    }
}
