using Microsoft.AspNetCore.Mvc;
using BookStoreApp.Data;
using BookStoreApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApp.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BooksController(ApplicationDbContext context)
        {
            _context = context;
            SeedBooks(); // Auto-seed dummy books if table is empty
        }

        // ── Seed dummy books once ─────────────────────────────────────────────────
        private void SeedBooks()
        {
            if (_context.Books.Any()) return;  // ← back to this


            // Insert fresh dummy books
            var dummyBooks = new List<Book>
    {
        new Book { Title = "Harry Potter and the Sorcerer's Stone", Author = "J.K. Rowling",       Price = 350, Stock = 10, CoverImage = "harry.jpg" },
        new Book { Title = "The Alchemist",                         Author = "Paulo Coelho",        Price = 280, Stock = 5,  CoverImage = "alchemist.jpg" },
        new Book { Title = "Rich Dad Poor Dad",                     Author = "Robert Kiyosaki",     Price = 320, Stock = 8,  CoverImage = "richdad.jpg" },
        new Book { Title = "To Kill a Mockingbird",                 Author = "Harper Lee",          Price = 299, Stock = 6,  CoverImage = "mockingbird.jpg" },
        new Book { Title = "The Great Gatsby",                      Author = "F. Scott Fitzgerald", Price = 250, Stock = 4,  CoverImage = "gatsby.jpg" },
        new Book { Title = "1984",                                  Author = "George Orwell",       Price = 275, Stock = 12, CoverImage = "1984.jpg" },
        new Book { Title = "Atomic Habits",                         Author = "James Clear",         Price = 399, Stock = 15, CoverImage = "atomic.jpg" },
        new Book { Title = "The 48 Laws of Power",                  Author = "Robert Greene",       Price = 450, Stock = 3,  CoverImage = "48laws.jpg" },
        new Book { Title = "Pride and Prejudice",                   Author = "Jane Austen",         Price = 220, Stock = 7,  CoverImage = "pride.jpg" },
        new Book { Title = "The Hunger Games",                      Author = "Suzanne Collins",     Price = 310, Stock = 9,  CoverImage = "hunger.jpg" },
    };

            _context.Books.AddRange(dummyBooks);
            _context.SaveChanges();
        }
        
        // ── GET: /Books/Index?search=xxx ──────────────────────────────────────────
        public async Task<IActionResult> Index(string? search)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var books = _context.Books.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                books = books.Where(b =>
                    b.Title.Contains(search) || b.Author.Contains(search));
            }

            ViewBag.Search = search;
            return View(await books.ToListAsync());
        }

        // ── GET: /Books/Details/5 ─────────────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();

            return View(book);
        }
    }
}