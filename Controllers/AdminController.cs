using Microsoft.AspNetCore.Mvc;
using BookStoreApp.Data;
using BookStoreApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApp.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AdminController(ApplicationDbContext db)
        {
            _db = db;
        }

        // ── Guard helper ──────────────────────────────────────────────────────────
        private bool IsAdmin()
        {
            return HttpContext.Session.GetString("UserRole") == "Admin";
        }

        private IActionResult AdminOnly()
        {
            TempData["Error"] = "Access denied. Admins only.";
            return RedirectToAction("Index", "Books");
        }

        // ── Dashboard ─────────────────────────────────────────────────────────────
        public IActionResult Index()
        {
            if (!IsAdmin()) return AdminOnly();

            ViewBag.TotalBooks = _db.Books.Count();
            ViewBag.TotalUsers = _db.Users.Count();
            ViewBag.TotalOrders = _db.Transactions.Count();
            ViewBag.LowStock = _db.Books.Count(b => b.Stock <= 5);

            return View();
        }

        // ── Users ─────────────────────────────────────────────────────────────────
        public IActionResult Users()
        {
            if (!IsAdmin()) return AdminOnly();

            var users = _db.Users.ToList();
            return View(users);
        }

        // ── Books list ────────────────────────────────────────────────────────────
        public IActionResult Books(string? search)
        {
            if (!IsAdmin()) return AdminOnly();

            var books = _db.Books.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim().ToLower();
                books = books.Where(b => b.Title.ToLower().Contains(q) ||
                                         b.Author.ToLower().Contains(q));
            }

            ViewBag.Search = search;
            return View(books.OrderBy(b => b.Title).ToList());
        }

        // ── Create Book (GET) ─────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult CreateBook()
        {
            if (!IsAdmin()) return AdminOnly();
            return View(new Book());
        }

        // ── Create Book (POST) ────────────────────────────────────────────────────
        [HttpPost]
        public IActionResult CreateBook(Book book)
        {
            if (!IsAdmin()) return AdminOnly();

            if (!ModelState.IsValid)
                return View(book);

            _db.Books.Add(book);
            _db.SaveChanges();

            TempData["Success"] = $"Book \"{book.Title}\" added successfully.";
            return RedirectToAction("Books");
        }

        // ── Edit Book (GET) ───────────────────────────────────────────────────────
        [HttpGet]
        public IActionResult EditBook(int id)
        {
            if (!IsAdmin()) return AdminOnly();

            var book = _db.Books.Find(id);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Books");
            }

            return View(book);
        }

        // ── Edit Book (POST) ──────────────────────────────────────────────────────
        [HttpPost]
        public IActionResult EditBook(int id, Book updated)
        {
            if (!IsAdmin()) return AdminOnly();

            var book = _db.Books.Find(id);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Books");
            }

            if (!ModelState.IsValid)
                return View(updated);

            book.Title = updated.Title;
            book.Author = updated.Author;
            book.Price = updated.Price;
            book.Stock = updated.Stock;

            _db.SaveChanges();

            TempData["Success"] = $"Book \"{book.Title}\" updated.";
            return RedirectToAction("Books");
        }

        // ── Delete Book (POST) ────────────────────────────────────────────────────
        [HttpPost]
        public IActionResult DeleteBook(int id)
        {
            if (!IsAdmin()) return AdminOnly();

            var book = _db.Books.Find(id);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Books");
            }

            _db.Books.Remove(book);
            _db.SaveChanges();

            TempData["Success"] = $"Book \"{book.Title}\" deleted.";
            return RedirectToAction("Books");
        }
    }
}