using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Project.Models;
using Project.ViewModels;

namespace Project.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            await EnsureRolesExistAsync();

            var model = new RegisterViewModel
            {
                SelectedRole = SD.Role_Customer
            };

            await PopulateRoleListAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            await EnsureRolesExistAsync();
            await PopulateRoleListAsync(model);

            if (!ModelState.IsValid)
                return View(model);

            if (string.IsNullOrWhiteSpace(model.SelectedRole) ||
                !await _roleManager.RoleExistsAsync(model.SelectedRole))
            {
                ModelState.AddModelError(
                    nameof(model.SelectedRole),
                    "Please select a valid role.");

                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                DisplayName = model.DisplayName
            };

            var result = await _userManager.CreateAsync(
                user,
                model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);

                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(
                user,
                model.SelectedRole);

            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);

                await _userManager.DeleteAsync(user);
                return View(model);
            }

            await _signInManager.SignInAsync(
                user,
                isPersistent: false);

            TempData["success"] = "Registration successful.";

            // Both roles return to the homepage after registration.
            return RedirectToAction(
                "Index",
                "Home",
                new { area = "" });
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid login attempt.");

                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Invalid login attempt.");

                return View(model);
            }

            // Both Admin and Customer return to the homepage.
            if (await _userManager.IsInRoleAsync(user, SD.Role_Admin) ||
                await _userManager.IsInRoleAsync(user, SD.Role_Customer))
            {
                return RedirectToAction(
                    "Index",
                    "Home",
                    new { area = "" });
            }

            await _signInManager.SignOutAsync();

            ModelState.AddModelError(
                string.Empty,
                "Your account does not have a supported role.");

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            // Clear temporary Session state, including the navigation cart count.
            // The database shopping-cart rows are intentionally preserved.
            HttpContext.Session.Clear();

            TempData["success"] = "Logged out successfully.";

            return RedirectToAction(
                "Index",
                "Home",
                new { area = "" });
        }

        private async Task EnsureRolesExistAsync()
        {
            string[] roles =
            {
                SD.Role_Customer,
                SD.Role_Admin
            };

            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    var result = await _roleManager.CreateAsync(
                        new IdentityRole(role));

                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create role '{role}': " +
                            string.Join(
                                "; ",
                                result.Errors.Select(
                                    error => error.Description)));
                    }
                }
            }
        }

        private Task PopulateRoleListAsync(RegisterViewModel model)
        {
            model.RoleList = _roleManager.Roles
                .Where(role => role.Name != null)
                .OrderBy(role => role.Name)
                .Select(role => new SelectListItem
                {
                    Text = role.Name!,
                    Value = role.Name!
                })
                .ToList();

            return Task.CompletedTask;
        }
    }
}