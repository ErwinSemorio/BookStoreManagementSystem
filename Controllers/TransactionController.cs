using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using BookStoreApp.Models;
using BookStoreApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Text;

namespace BookStoreApp.Controllers
{
    public class TransactionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TransactionController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? GetUserId() => HttpContext.Session.GetInt32("UserId");

        private void RefreshWalletSession(decimal newBalance)
        {
            HttpContext.Session.SetString("WalletBalance", newBalance.ToString("N2"));
        }

        // ── GET: /Transaction/Confirm?id=1&quantity=1 ─────────────────────────
        [HttpGet]
        public IActionResult Confirm(int id, int quantity = 1)
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var book = _context.Books.FirstOrDefault(b => b.Id == id);
            if (book == null) return NotFound();

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return RedirectToAction("Login", "Account");

            if (quantity < 1) quantity = 1;
            if (quantity > book.Stock) quantity = book.Stock;

            decimal totalCost = book.Price * quantity;
            decimal walletBalance = user.WalletBalance ?? 0m;

            ViewBag.User = user;
            ViewBag.Quantity = quantity;
            ViewBag.TotalCost = totalCost;
            ViewBag.WalletBalance = walletBalance;
            ViewBag.Enough = walletBalance >= totalCost;
            ViewBag.Shortfall = totalCost - walletBalance;

            return View(book);
        }

        // ── POST: /Transaction/PlaceOrder ─────────────────────────────────────
        [HttpPost]
        public IActionResult PlaceOrder(int bookId, int quantity)
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId);
            var book = _context.Books.Find(bookId);

            if (user == null || book == null)
            {
                TempData["Error"] = "Something went wrong. Please try again.";
                return RedirectToAction("Index", "Books");
            }

            if (quantity < 1 || quantity > book.Stock)
            {
                TempData["Error"] = "Invalid quantity selected.";
                return RedirectToAction("Confirm", new { id = bookId, quantity });
            }

            decimal totalAmount = book.Price * quantity;
            decimal walletBalance = user.WalletBalance ?? 0m;

            if (walletBalance < totalAmount)
            {
                TempData["Error"] = $"Insufficient balance. You need ₱{totalAmount:N2} but only have ₱{walletBalance:N2}.";
                return RedirectToAction("Confirm", new { id = bookId, quantity });
            }

            if (book.Stock < quantity)
            {
                TempData["Error"] = "Not enough stock available.";
                return RedirectToAction("Confirm", new { id = bookId, quantity });
            }

            using (var dbTransaction = _context.Database.BeginTransaction())
            {
                try
                {
                    user.WalletBalance = walletBalance - totalAmount;
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

                    RefreshWalletSession(user.WalletBalance ?? 0m);

                    TempData["Success"] = $"Purchase successful! You bought {quantity}x \"{book.Title}\" for ₱{totalAmount:N2}. Remaining balance: ₱{user.WalletBalance:N2}.";
                    return RedirectToAction("History");
                }
                catch
                {
                    dbTransaction.Rollback();
                    TempData["Error"] = "Transaction failed. Please try again.";
                    return RedirectToAction("Confirm", new { id = bookId, quantity });
                }
            }
        }

        // ── GET: /Transaction/History ─────────────────────────────────────────
        [HttpGet]
        public IActionResult History()
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId.Value);
            if (user == null) return RedirectToAction("Login", "Account");

            var history = _context.Transactions
                .Include(t => t.Book)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ToList();

            ViewBag.User = user;
            ViewBag.TotalSpent = history.Sum(t => t.TotalAmount);
            ViewBag.TotalUnits = history.Sum(t => t.Quantity);
            ViewBag.LastDate = history.Any()
                                    ? history.First().TransactionDate.ToString("MMM dd, yyyy")
                                    : "—";

            return View(history);
        }

        // ── GET: /Transaction/ExportCsv ───────────────────────────────────────
        [HttpGet]
        public IActionResult ExportCsv()
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var user = _context.Users.Find(userId.Value);
            if (user == null) return RedirectToAction("Login", "Account");

            var history = _context.Transactions
                .Include(t => t.Book)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TransactionDate)
                .ToList();

            var sb = new StringBuilder();

            // Header row
            sb.AppendLine("Transaction ID,Book Title,Author,Quantity,Amount (PHP),Date,Status");

            // Data rows
            foreach (var t in history)
            {
                var title = $"\"{t.Book?.Title?.Replace("\"", "'")}\"";
                var author = $"\"{t.Book?.Author?.Replace("\"", "'")}\"";
                sb.AppendLine($"{t.Id},{title},{author},{t.Quantity},{t.TotalAmount:N2},{t.TransactionDate:yyyy-MM-dd HH:mm},{t.Status}");
            }

            // Summary rows
            sb.AppendLine();
            sb.AppendLine($",,,,,,");
            sb.AppendLine($"Exported by,{user.Name},,,,{DateTime.Now:yyyy-MM-dd HH:mm},");
            sb.AppendLine($"Total Transactions,{history.Count},,,,");
            sb.AppendLine($"Total Spent,\"{history.Sum(t => t.TotalAmount):N2}\",,,,");
            sb.AppendLine($"Current Balance,\"{user.WalletBalance:N2}\",,,,");

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            var fileName = $"BookStore_Transactions_{user.Name?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.csv";

            return File(bytes, "text/csv", fileName);
        }
    }
}