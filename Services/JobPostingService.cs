using HireSphere.Data;
using HireSphere.Models;
using Microsoft.EntityFrameworkCore;

namespace HireSphere.Services
{
    public class JobPostingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<JobPostingService> _logger;

        public JobPostingService(ApplicationDbContext context, ILogger<JobPostingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task ExpireOldJobPostings()
        {
            try
            {
                var expiredJobs = await _context.JobPostings
                    .Where(j => j.IsActive && j.ExpiryDate < DateTime.Today)
                    .ToListAsync();

                foreach (var job in expiredJobs)
                {
                    job.IsActive = false;
                    _context.Update(job);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Expired {expiredJobs.Count} job postings");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring old job postings");
            }
        }
    }
}