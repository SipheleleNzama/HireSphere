using HireSphere.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore; // Add this
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Identity.Client;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace HireSphere.Data
{
    public class HireSphereDbContext : IdentityDbContext<ApplicationUser> // Modified this line
    {
        public HireSphereDbContext(DbContextOptions<HireSphereDbContext> options)
            : base(options)
        {
        }

        // Your existing DbSets
        public DbSet<JobPosting> JobPostings { get; set; }
        public DbSet<Candidate> Candidates { get; set; }
        public DbSet<Application> Applications { get; set; }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Add this line first

            // Your existing relationships
            modelBuilder.Entity<Application>()
                .HasOne(a => a.JobPosting)
                .WithMany(j => j.Applications)
                .HasForeignKey(a => a.JobPostingId);

            modelBuilder.Entity<Application>()
                .HasOne(a => a.Candidate)
                .WithMany(c => c.Applications)
                .HasForeignKey(a => a.CandidateId);

            // Add precision for decimal properties to fix the warnings
            modelBuilder.Entity<Application>()
                .Property(a => a.MatchScore)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Candidate>()
                .Property(c => c.ExpectedSalary)
                .HasPrecision(18, 2);

            modelBuilder.Entity<JobPosting>()
                .Property(j => j.SalaryRangeMin)
                .HasPrecision(18, 2);

            modelBuilder.Entity<JobPosting>()
                .Property(j => j.SalaryRangeMax)
                .HasPrecision(18, 2);
        }
    }
}