using Microsoft.AspNetCore.Mvc;
using BookStoreApp.Data;
using BookStoreApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims; // 👈 ADD THIS LINE

namespace BookStoreApp.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BooksController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 👇 ADD THIS HELPER METHOD
        private bool IsAdmin()
        {
            var role = HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
            return role == "Admin";
        }

        // GET: /Books/Index?search=xxx
        public async Task<IActionResult> Index(string? search)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var books = _context.Books.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                books = books.Where(b =>
                    b.Title.Contains(search) || b.Author.Contains(search));
                ViewBag.Search = search;
            }

            // 👇 Pass admin status to view
            ViewBag.IsAdmin = IsAdmin();
            return View(await books.ToListAsync());
        }

        // GET: /Books/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();

            return View(book);
        }

        // --- NEW: GET Create View ---
        public IActionResult Create()
        {
            // Security: Only Admins can see this page
            if (!IsAdmin()) return Forbid();
            return View();
        }

        // --- NEW: POST Create Action ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Book book)
        {
            // Security: Only Admins can submit
            if (!IsAdmin()) return Forbid();

            if (ModelState.IsValid)
            {
                _context.Books.Add(book);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(book);
        }
    }
}