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

        private void SetSession(Users user)
        {
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("UserName", user.Name ?? "User");
            HttpContext.Session.SetString("UserRole", user.Role ?? "User");
            HttpContext.Session.SetString("WalletBalance", (user.WalletBalance ?? 0m).ToString("N2"));
        }

        // ── Register GET ──────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Register()
        {
            if (IsLoggedIn()) return RedirectToAction("Index", "Books");
            return View();
        }

        // ── Register POST ─────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Register(
            string name,
            string email,
            string password,
            string? confirmPassword,
            IFormFile? profileImage)
        {
            // ── 1. Empty field check ──────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(name))
            {
                ViewBag.Error = "Full name is required.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Email address is required.";
                return View();
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Password is required.";
                return View();
            }

            // ── 2. Password minimum length ────────────────────────────────────
            if (password.Length < 6)
            {
                ViewBag.Error = "Password must be at least 6 characters.";
                return View();
            }

            // ── 3. Password match check ───────────────────────────────────────
            if (password != confirmPassword)
            {
                ViewBag.Error = "Passwords do not match. Please try again.";
                return View();
            }

            // ── 4. Duplicate email check ──────────────────────────────────────
            bool emailExists = _db.Users.Any(u =>
                u.Email == email.Trim().ToLower());

            if (emailExists)
            {
                ViewBag.Error = "An account with that email already exists. Please sign in instead.";
                return View();
            }

            // ── 5. Profile image upload ───────────────────────────────────────
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

                if (profileImage.Length > 5 * 1024 * 1024)
                {
                    ViewBag.Error = "Profile image must be smaller than 5MB.";
                    return View();
                }

                fileName = Guid.NewGuid().ToString() + ext;
                var savePath = Path.Combine(_env.WebRootPath, "images", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                using var stream = new FileStream(savePath, FileMode.Create);
                await profileImage.CopyToAsync(stream);
            }

            // ── 6. Create user ────────────────────────────────────────────────
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

            TempData["Success"] = $"Welcome, {user.Name}! Your account has been created with ₱1,000.00 starting balance.";
            return RedirectToAction("Login");
        }

        // ── Login GET ─────────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult Login()
        {
            if (IsLoggedIn()) return RedirectToAction("Index", "Books");
            return View();
        }

        // ── Login POST ────────────────────────────────────────────────────────
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
                ViewBag.Error = "Incorrect email or password. Please try again.";
                return View();
            }

            SetSession(user);
            TempData["Success"] = $"Welcome back, {user.Name}! 👋";
            return RedirectToAction("Index", "Books");
        }

        // ── Logout ────────────────────────────────────────────────────────────
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Login");
        }

        // ── Profile ───────────────────────────────────────────────────────────
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

            var transactions = _db.Transactions
                .Include(t => t.Book)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ToList();

            var wishlist = _db.Wishlists
                .Include(w => w.Book)
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.AddedDate)
                .ToList();

            ViewBag.Transactions = transactions;
            ViewBag.Wishlist = wishlist;
            ViewBag.BooksOwned = transactions.Sum(t => t.Quantity);

            return View(user);
        }

        // ── Edit GET ──────────────────────────────────────────────────────────
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

        // ── Edit POST ─────────────────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Edit(
            string name,
            string email,
            string? newPassword,
            string? confirmPassword,
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

            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                if (newPassword.Length < 6)
                {
                    ViewBag.Error = "New password must be at least 6 characters.";
                    return View(user);
                }

                if (newPassword != confirmPassword)
                {
                    ViewBag.Error = "New password and confirm password do not match.";
                    return View(user);
                }

                user.Password = newPassword;
            }

            if (profileImage != null && profileImage.Length > 0)
            {
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(profileImage.FileName).ToLower();

                if (!allowed.Contains(ext))
                {
                    ViewBag.Error = "Profile image must be jpg, png, gif, or webp.";
                    return View(user);
                }

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
            SetSession(user);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Profile");
        }

        // ── Reload Wallet GET ─────────────────────────────────────────────────
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

        // ── Reload Wallet POST ────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReloadWallet(decimal amount)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login");

            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null) return RedirectToAction("Login");

            if (amount < 100m)
            {
                ViewBag.Error = "Minimum reload amount is ₱100.";
                ViewBag.CurrentBalance = user.WalletBalance ?? 0m;
                return View();
            }

            if (amount > 50000m)
            {
                ViewBag.Error = "Maximum reload amount is ₱50,000.";
                ViewBag.CurrentBalance = user.WalletBalance ?? 0m;
                return View();
            }

            user.WalletBalance = (user.WalletBalance ?? 0m) + amount;
            await _db.SaveChangesAsync();
            SetSession(user);

            TempData["Success"] = $"₱{amount:N2} added! New balance: ₱{user.WalletBalance:N2}";
            return RedirectToAction("Profile");
        }
    }
}