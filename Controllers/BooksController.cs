using Microsoft.AspNetCore.Mvc;
using BookStoreApp.Data;
using BookStoreApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BookStoreApp.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BooksController(ApplicationDbContext context)
        {
            _context = context;
            SeedBooks();
        }

        private void SeedBooks()
        {
            if (_context.Books.Any()) return;

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

        // FIXED: Merged duplicate Index() methods into one
        public async Task<IActionResult> Index(string? searchString)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            ViewData["CurrentFilter"] = searchString;

            var user = await _context.Users.FindAsync(userId);
            var totalOrders = await _context.Transactions.CountAsync(t => t.UserId == userId);

            var booksQuery = from b in _context.Books select b;

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                booksQuery = booksQuery.Where(b => b.Title.Contains(searchString) || b.Author.Contains(searchString));
            }

            var model = new DashboardViewModel
            {
                WalletBalance = user?.WalletBalance ?? 0,
                TotalOrders = totalOrders,
                Books = await booksQuery.ToListAsync()
            };

            return View(model);
        }

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