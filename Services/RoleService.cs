using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace HireSphere.Services
{
    public class RoleService
    {
        private readonly RoleManager<IdentityRole> _roleManager;

        public RoleService(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }

        public async Task InitializeRoles()
        {
            string[] roleNames = {
                "Administrator",
                "Recruiter",
                "HiringManager",
                "Candidate",
                "DataAnalyst"
            };

            foreach (var roleName in roleNames)
            {
                var roleExist = await _roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await _roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }
    }
}
