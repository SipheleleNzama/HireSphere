using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using HireSphere.Models;
using Microsoft.AspNetCore.Authorization;
using HireSphere.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;

namespace HireSphere.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        [Authorize]
    public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return RedirectToAction("Login");
            }

            return View(new ProfileViewModel
            {
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName
            });
        }




        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string role = "Candidate")
        {
            if (ModelState.IsValid)
            {
                // Check if user already exists BEFORE creating to prevent duplicates
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError(string.Empty, "A user with this email address already exists.");
                    return View(model);
                }

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Check if role exists, if not create it
                    if (!await _roleManager.RoleExistsAsync(role))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(role));
                    }

                    await _userManager.AddToRoleAsync(user, role);

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToAction("Index", "Home");
                }

                AddErrors(result);
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string returnUrl = null)
        {
            // Clear the existing external cookie
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ViewData["ReturnUrl"] = returnUrl;
            var model = new LoginViewModel
            {
                ReturnUrl = returnUrl,
                ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
            };
            return View(model);
        }


        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            _logger.LogInformation($"Login attempt for {model.Email}");
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state");
                return View(model);
            }

            try
            {
                // Use FirstOrDefaultAsync to handle potential duplicates
                var users = _userManager.Users.Where(u => u.Email == model.Email);
                var userCount = await users.CountAsync();

                if (userCount == 0)
                {
                    _logger.LogWarning($"User {model.Email} not found");
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }

                if (userCount > 1)
                {
                    _logger.LogError($"Multiple users found with email {model.Email}. Count: {userCount}");
                    ModelState.AddModelError(string.Empty, "Account configuration error. Please contact support.");
                    return View(model);
                }

                var user = await users.FirstOrDefaultAsync();

                if (user == null)
                {
                    _logger.LogWarning($"User {model.Email} not found");
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false);

                _logger.LogInformation($"Login result: {result.Succeeded}");

                if (result.Succeeded)
                {
                    _logger.LogInformation($"User {user.Id} logged in");
                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return View(model);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, $"Error during login for {model.Email}");
                ModelState.AddModelError(string.Empty, "Login error. Please contact support.");
                return View(model);
            }
        }

        [AllowAnonymous]
        public async Task<IActionResult> CheckAdminUser()
        {
            var admin = await _userManager.FindByEmailAsync("anelenzama07@gmail.com");
            if (admin == null) return Content("Admin user not found");

            var passwordValid = await _userManager.CheckPasswordAsync(admin, "SecurePassword123!");
            return Content($"Admin exists. Password valid: {passwordValid}");
        }
        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        [AllowAnonymous]
        public async Task<IActionResult> InvestigateDuplicates()
        {
            var allUsers = _userManager.Users.ToList();
            var duplicateGroups = allUsers
                .GroupBy(u => u.Email.ToLower())
                .Where(g => g.Count() > 1)
                .ToList();

            var result = $"Total users in database: {allUsers.Count}\n";
            result += $"Email addresses with duplicates: {duplicateGroups.Count}\n\n";

            foreach (var group in duplicateGroups)
            {
                result += $"=== Email: {group.Key} ({group.Count()} duplicates) ===\n";

                foreach (var user in group.OrderBy(u => u.Id))
                {
                    result += $"  ID: {user.Id}\n";
                    result += $"  UserName: {user.UserName}\n";
                    result += $"  Email: {user.Email}\n";
                    result += $"  NormalizedEmail: {user.NormalizedEmail}\n";
                    result += $"  EmailConfirmed: {user.EmailConfirmed}\n";
                    result += $"  Created: {(user.LockoutEnd?.ToString() ?? "Unknown")}\n";
                    result += "  ---\n";
                }
                result += "\n";
            }

            return Content(result, "text/plain");
        }

        [AllowAnonymous]
        public async Task<IActionResult> CleanupDuplicates(bool actuallyDelete = false)
        {
            var allUsers = _userManager.Users.ToList();
            var duplicateGroups = allUsers
                .GroupBy(u => u.Email.ToLower())
                .Where(g => g.Count() > 1)
                .ToList();

            var result = $"=== DUPLICATE CLEANUP REPORT ===\n";
            result += $"Mode: {(actuallyDelete ? "ACTUAL DELETION" : "PREVIEW ONLY")}\n\n";

            int totalDeleted = 0;

            foreach (var group in duplicateGroups)
            {
                var users = group.OrderBy(u => u.Id).ToList(); // Keep the oldest (first created)
                var userToKeep = users.First();
                var usersToDelete = users.Skip(1).ToList();

                result += $"Email: {group.Key}\n";
                result += $"  KEEPING: ID={userToKeep.Id}, UserName={userToKeep.UserName}, Name={userToKeep.FirstName} {userToKeep.LastName}\n";

                foreach (var userToDelete in usersToDelete)
                {
                    result += $"  DELETING: ID={userToDelete.Id}, UserName={userToDelete.UserName}, Name={userToDelete.FirstName} {userToDelete.LastName}\n";

                    if (actuallyDelete)
                    {
                        try
                        {
                            var deleteResult = await _userManager.DeleteAsync(userToDelete);
                            if (deleteResult.Succeeded)
                            {
                                result += $"    ✓ Successfully deleted\n";
                                totalDeleted++;
                                _logger.LogInformation($"Deleted duplicate user: {userToDelete.Id} - {userToDelete.Email}");
                            }
                            else
                            {
                                result += $"    ✗ Failed to delete: {string.Join(", ", deleteResult.Errors.Select(e => e.Description))}\n";
                                _logger.LogError($"Failed to delete user {userToDelete.Id}: {string.Join(", ", deleteResult.Errors.Select(e => e.Description))}");
                            }
                        }
                        catch (Exception ex)
                        {
                            result += $"    ✗ Exception during deletion: {ex.Message}\n";
                            _logger.LogError(ex, $"Exception deleting user {userToDelete.Id}");
                        }
                    }
                }
                result += "\n";
            }

            result += $"\nSummary: {(actuallyDelete ? totalDeleted + " users deleted" : duplicateGroups.Sum(g => g.Count() - 1) + " users would be deleted")}\n";

            if (!actuallyDelete)
            {
                result += "\nTo actually perform the deletion, visit: /Account/CleanupDuplicates?actuallyDelete=true\n";
                result += "⚠️  WARNING: This will permanently delete duplicate user accounts!\n";
                result += "⚠️  Make sure to backup your database first!\n";
            }
            else
            {
                result += "\n✅ Cleanup completed!\n";
                result += "You can now try logging in again.\n";
            }

            return Content(result, "text/plain");
        }

        // Settings Page
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new SettingsViewModel
            {
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                CurrentPassword = string.Empty,
                NewPassword = string.Empty
            };

            return View(model);
        }
    }
}