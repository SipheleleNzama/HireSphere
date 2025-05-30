using Microsoft.AspNetCore.Mvc;
using HireSphere.Services;
using HireSphere.Models.AI;

namespace HireSphere.Views.Shared.Components.MatchAnalysis
{
    public class MatchAnalysisViewComponent : ViewComponent
    {
        public IViewComponentResult Invoke(MatchAnalysisResult model)
        {
            return View(model);
        }
    }
}