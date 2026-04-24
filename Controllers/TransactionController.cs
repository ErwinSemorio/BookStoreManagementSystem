using Microsoft.AspNetCore.Mvc;
using BookStoreApp.Data;
using BookStoreApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApp.Controllers
{
    public class TransactionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransactionController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: /Transaction/Buy
        [HttpPost]
        public async Task<IActionResult> Buy(int bookId, int quantity)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                TempData["Error"] = "Book not found.";
                return RedirectToAction("Index", "Books");
            }

            if (book.Stock < quantity)
            {
                TempData["Error"] = $"Not enough stock. Only {book.Stock} left.";
                return RedirectToAction("Details", "Books", new { id = bookId });
            }

            book.Stock -= quantity;

            var transaction = new Transaction
            {
                UserId = userId.Value,
                BookId = bookId,
                Quantity = quantity,
                Date = DateTime.Now
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Successfully purchased {quantity} copy(ies) of \"{book.Title}\"!";
            return RedirectToAction("History");
        }

        // GET: /Transaction/History
        public async Task<IActionResult> History()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var transactions = await _context.Transactions
                .Include(t => t.Book)
                .Include(t => t.User)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            return View(transactions);
        }
    }
}
