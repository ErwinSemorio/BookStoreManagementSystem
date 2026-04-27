using Microsoft.AspNetCore.Mvc;
using BookStoreApp.Data;
using BookStoreApp.Models;

namespace BookStoreApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public AccountController(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private bool IsLoggedIn() => HttpContext.Session.GetInt32("UserId") != null;

        private void SetSession(Users user)
        {
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name);
            HttpContext.Session.SetString("UserRole", user.Role);
        }

        // ── Register (GET) ────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Register()
        {
            if (IsLoggedIn()) return RedirectToAction("Index", "Books");
            return View();
        }

        // ── Register (POST) ───────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Register(string name, string email,
                                                   string password, IFormFile? profileImage)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "All fields are required.";
                return View();
            }

            // Check duplicate email
            if (_db.Users.Any(u => u.Email == email))
            {
                ViewBag.Error = "An account with that email already exists.";
                return View();
            }

            // Handle profile image upload
            string? fileName = null;
            if (profileImage != null && profileImage.Length > 0)
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(profileImage.FileName).ToLower();
                if (!allowed.Contains(ext))
                {
                    ViewBag.Error = "Profile image must be jpg, png, gif, or webp.";
                    return View();
                }

                fileName = Guid.NewGuid().ToString() + ext;
                var savePath = Path.Combine(_env.WebRootPath, "images", fileName);
                using var stream = new FileStream(savePath, FileMode.Create);
                await profileImage.CopyToAsync(stream);
            }

            // Save user
            var user = new Users
            {
                Name = name.Trim(),
                Email = email.Trim().ToLower(),
                Password = password,        // TODO: hash with BCrypt in production
                ProfileImage = fileName,
                Role = "User"
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Account created! Please sign in.";
            return RedirectToAction("Login");
        }

        // ── Login (GET) ───────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Login()
        {
            if (IsLoggedIn()) return RedirectToAction("Index", "Books");
            return View();
        }

        // ── Login (POST) ──────────────────────────────────────────────────────────
        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Email and password are required.";
                return View();
            }

            var user = _db.Users.FirstOrDefault(
                u => u.Email == email.Trim().ToLower() && u.Password == password);

            if (user == null)
            {
                ViewBag.Error = "Incorrect email or password.";
                return View();
            }

            SetSession(user);

            TempData["Success"] = $"Welcome back, {user.Name}!";
            return RedirectToAction("Index", "Books");
        }

        // ── Logout ────────────────────────────────────────────────────────────────
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["Success"] = "You have been logged out.";
            return RedirectToAction("Login");
        }

        // ── Profile (GET) ─────────────────────────────────────────────────────────
        public IActionResult Profile()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var user = _db.Users.Find(userId);

            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }

            return View(user);
        }

        // ── Edit Profile (GET) ────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Edit()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var user = _db.Users.Find(userId);

            if (user == null) return RedirectToAction("Login");

            return View(user);
        }

        // ── Edit Profile (POST) ───────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Edit(string name, string email, string? newPassword, IFormFile? profileImage)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return RedirectToAction("Login");

            // Validate name & email
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Name and email cannot be empty.";
                return View(user);
            }

            // Check email not taken by someone else
            bool emailTaken = _db.Users.Any(u => u.Email == email.Trim().ToLower()
                                              && u.Id != userId);
            if (emailTaken)
            {
                ViewBag.Error = "That email is already used by another account.";
                return View(user);
            }

            // Handle new profile image
            if (profileImage != null && profileImage.Length > 0)
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(profileImage.FileName).ToLower();
                if (!allowed.Contains(ext))
                {
                    ViewBag.Error = "Profile image must be jpg, png, gif, or webp.";
                    return View(user);
                }

                // Delete old image if it exists
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, "images", user.ProfileImage);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                var fileName = Guid.NewGuid().ToString() + ext;
                var savePath = Path.Combine(_env.WebRootPath, "images", fileName);
                using var stream = new FileStream(savePath, FileMode.Create);
                await profileImage.CopyToAsync(stream);
                user.ProfileImage = fileName;
            }

            // Apply changes
            user.Name = name.Trim();
            user.Email = email.Trim().ToLower();

            if (!string.IsNullOrWhiteSpace(newPassword))
                user.Password = newPassword;   // TODO: hash in production

            await _db.SaveChangesAsync();

            // Refresh session name
            HttpContext.Session.SetString("UserName", user.Name);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }
    }
}