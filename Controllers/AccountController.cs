using Microsoft.AspNetCore.Mvc;
using BookStoreApp.Data;
using BookStoreApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.IO;

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

        private bool IsLoggedIn() => HttpContext.Session.GetInt32("UserId") != null;

        // ── Saves all session values including WalletBalance for the navbar pill ──
        private void SetSession(Users user)
        {
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name ?? "User");
            HttpContext.Session.SetString("UserRole", user.Role ?? "User");
            HttpContext.Session.SetString("WalletBalance", (user.WalletBalance ?? 0m).ToString("N2"));
        }

        // ── Register GET ──────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Register()
        {
            if (IsLoggedIn()) return RedirectToAction("Index", "Books");
            return View();
        }

        // ── Register POST ─────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Register(string name, string email, string password, IFormFile? profileImage)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "All fields are required.";
                return View();
            }

            if (_db.Users.Any(u => u.Email == email.Trim().ToLower()))
            {
                ViewBag.Error = "An account with that email already exists.";
                return View();
            }

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
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                using var stream = new FileStream(savePath, FileMode.Create);
                await profileImage.CopyToAsync(stream);
            }

            var user = new Users
            {
                Name = name.Trim(),
                Email = email.Trim().ToLower(),
                Password = password,
                ProfileImage = fileName,
                Role = "User",
                WalletBalance = 1000.00m
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Account created! You start with ₱1,000.00 wallet balance. Please sign in.";
            return RedirectToAction("Login");
        }

        // ── Login GET ─────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Login()
        {
            if (IsLoggedIn()) return RedirectToAction("Index", "Books");
            return View();
        }

        // ── Login POST ────────────────────────────────────────────────────────────
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

            SetSession(user); // saves WalletBalance to session → shows in navbar
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

        // ── Profile ───────────────────────────────────────────────────────────────
        public IActionResult Profile()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = _db.Users.Find(userId.Value);
            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login");
            }

            return View(user);
        }

        // ── Edit GET ──────────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Edit()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = _db.Users.Find(userId.Value);
            if (user == null) return RedirectToAction("Login");

            return View(user);
        }

        // ── Edit POST ─────────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Edit(string name, string email,
                                              string? newPassword, string? confirmPassword,
                                              IFormFile? profileImage)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null) return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Name and email cannot be empty.";
                return View(user);
            }

            if (_db.Users.Any(u => u.Email == email.Trim().ToLower() && u.Id != userId))
            {
                ViewBag.Error = "That email is already used by another account.";
                return View(user);
            }

            // Password change with confirm check
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (newPassword != confirmPassword)
                {
                    ViewBag.Error = "New password and confirm password do not match.";
                    return View(user);
                }
                user.Password = newPassword;
            }

            // Profile image upload
            if (profileImage != null && profileImage.Length > 0)
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(profileImage.FileName).ToLower();
                if (!allowed.Contains(ext))
                {
                    ViewBag.Error = "Profile image must be jpg, png, gif, or webp.";
                    return View(user);
                }

                // Delete old image file
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, "images", user.ProfileImage);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                var fileName = Guid.NewGuid().ToString() + ext;
                var savePath = Path.Combine(_env.WebRootPath, "images", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                using var stream = new FileStream(savePath, FileMode.Create);
                await profileImage.CopyToAsync(stream);
                user.ProfileImage = fileName;
            }

            user.Name = name.Trim();
            user.Email = email.Trim().ToLower();

            await _db.SaveChangesAsync();

            SetSession(user); // refreshes wallet balance in navbar after edit
            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }

        // ── Reload Wallet GET ─────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult ReloadWallet()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = _db.Users.Find(userId.Value);
            if (user == null) return RedirectToAction("Login");

            ViewBag.CurrentBalance = user.WalletBalance ?? 0m;
            return View();
        }

        // ── Reload Wallet POST ────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> EditProfile(string name, string email,
                                      string? newPassword, string? confirmPassword,
                                      IFormFile? ProfileImage)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null) return RedirectToAction("Login");

            // VALIDATION
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Name and email cannot be empty.";
                return View(user);
            }

            if (_db.Users.Any(u => u.Email == email.Trim().ToLower() && u.Id != userId))
            {
                ViewBag.Error = "Email already used.";
                return View(user);
            }

            // PASSWORD
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (newPassword != confirmPassword)
                {
                    ViewBag.Error = "Passwords do not match.";
                    return View(user);
                }

                user.Password = newPassword;
            }

            // IMAGE UPLOAD
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(ProfileImage.FileName).ToLower();

                if (!allowed.Contains(ext))
                {
                    ViewBag.Error = "Invalid image format.";
                    return View(user);
                }

                // delete old image
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, "images", user.ProfileImage);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                // save new image
                var fileName = Guid.NewGuid().ToString() + ext;
                var savePath = Path.Combine(_env.WebRootPath, "images", fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(stream);
                }

                user.ProfileImage = fileName;
            }

            // UPDATE USER
            user.Name = name.Trim();
            user.Email = email.Trim().ToLower();

            await _db.SaveChangesAsync();

            // UPDATE SESSION
            SetSession(user);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }

        // ── Edit Profile POST ────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> EditP(string name, string email,
                                             string? newPassword, string? confirmPassword,
                                             IFormFile? ProfileImage)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null) return RedirectToAction("Login");

            // VALIDATION
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Name and email cannot be empty.";
                return View(user);
            }

            if (_db.Users.Any(u => u.Email == email.Trim().ToLower() && u.Id != userId))
            {
                ViewBag.Error = "Email already used.";
                return View(user);
            }

            // PASSWORD
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (newPassword != confirmPassword)
                {
                    ViewBag.Error = "Passwords do not match.";
                    return View(user);
                }

                user.Password = newPassword;
            }

            // IMAGE UPLOAD
            if (ProfileImage != null && ProfileImage.Length > 0)
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(ProfileImage.FileName).ToLower();

                if (!allowed.Contains(ext))
                {
                    ViewBag.Error = "Invalid image format.";
                    return View(user);
                }

                // delete old image
                if (!string.IsNullOrEmpty(user.ProfileImage))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, "images", user.ProfileImage);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                // save new image
                var fileName = Guid.NewGuid().ToString() + ext;
                var savePath = Path.Combine(_env.WebRootPath, "images", fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

                using (var stream = new FileStream(savePath, FileMode.Create))
                {
                    await ProfileImage.CopyToAsync(stream);
                }

                user.ProfileImage = fileName;
            }

            // UPDATE USER
            user.Name = name.Trim();
            user.Email = email.Trim().ToLower();

            await _db.SaveChangesAsync();

            // UPDATE SESSION
            SetSession(user);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }
    }
}