using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace HireSphere.Models
{
    public class CandidateAnalysis
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }

        public DateTime UploadDate { get; set; }

        public double MatchPercentage { get; set; }

        public string? Skills { get; set; }
    }
}