using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using BookStoreApp.Models;
using BookStoreApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace BookStoreApp.Controllers
{
    public class TransactionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransactionController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Confirm(int id, int quantity = 1)
        {
            var book = _context.Books.FirstOrDefault(b => b.Id == id);
            if (book == null) return NotFound();

            var userId = HttpContext.Session.GetInt32("UserId"); // NO SPACE
            if (userId == null) return RedirectToAction("Login", "Account"); // NO SPACE

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return RedirectToAction("Login", "Account");

            ViewBag.User = user;
            ViewBag.Quantity = quantity;

            return View(book);
        }

        [HttpPost]
        public IActionResult PlaceOrder(int bookId, int quantity)
        {
            var userId = HttpContext.Session.GetInt32("UserId"); // NO SPACE
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId);
            var book = _context.Books.Find(bookId);

            if (user == null || book == null) return BadRequest();

            decimal totalAmount = book.Price * quantity;
            decimal wallet = user.WalletBalance ?? 0m;

            if (wallet < totalAmount)
            {
                TempData["Error"] = "Insufficient funds.";
                return RedirectToAction("Confirm", new { id = bookId, quantity = quantity });
            }

            using (var dbTransaction = _context.Database.BeginTransaction())
            {
                try
                {
                    user.WalletBalance = wallet - totalAmount;
                    book.Stock -= quantity;

                    var transaction = new Transaction
                    {
                        UserId = user.Id,
                        BookId = book.Id,
                        Quantity = quantity,
                        TotalAmount = totalAmount,
                        TransactionDate = DateTime.Now,
                        Status = "Completed"
                    };

                    _context.Transactions.Add(transaction);
                    _context.SaveChanges();

                    dbTransaction.Commit();
                    TempData["Success"] = "Purchase successful!";
                    return RedirectToAction("History");
                }
                catch
                {
                    dbTransaction.Rollback();
                    TempData["Error"] = "Transaction failed.";
                    return RedirectToAction("Confirm", new { id = bookId, quantity = quantity });
                }
            }
        }

        [HttpGet]
        public IActionResult History()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Account");

            var history = _context.Transactions
                .Include(t => t.Book)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ToList();

            return View(history);
        }
    }
}