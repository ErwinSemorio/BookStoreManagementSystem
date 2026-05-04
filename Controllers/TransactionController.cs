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

        private int? GetUserId() => HttpContext.Session.GetInt32("UserId");

        // Updates wallet pill in navbar after every purchase
        private void RefreshWalletSession(decimal newBalance)
        {
            HttpContext.Session.SetString("WalletBalance", newBalance.ToString("N2"));
        }

        // ── GET: /Transaction/Confirm?id=1&quantity=1 ─────────────────────────────
        [HttpGet]
        public IActionResult Confirm(int id, int quantity = 1)
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var book = _context.Books.FirstOrDefault(b => b.Id == id);
            if (book == null) return NotFound();

            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user == null) return RedirectToAction("Login", "Account");

            // Clamp quantity to valid range
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

        // ── POST: /Transaction/PlaceOrder ─────────────────────────────────────────
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

            // Validate quantity
            if (quantity < 1 || quantity > book.Stock)
            {
                TempData["Error"] = "Invalid quantity selected.";
                return RedirectToAction("Confirm", new { id = bookId, quantity });
            }

            decimal totalAmount = book.Price * quantity;
            decimal walletBalance = user.WalletBalance ?? 0m;

            // ── Wallet check ──────────────────────────────────────────────────────
            if (walletBalance < totalAmount)
            {
                TempData["Error"] = $"Insufficient balance. You need ₱{totalAmount:N2} but only have ₱{walletBalance:N2}. Please reload your wallet.";
                return RedirectToAction("Confirm", new { id = bookId, quantity });
            }

            // ── Stock check ───────────────────────────────────────────────────────
            if (book.Stock < quantity)
            {
                TempData["Error"] = "Not enough stock available.";
                return RedirectToAction("Confirm", new { id = bookId, quantity });
            }

            // ── Process transaction ───────────────────────────────────────────────
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

                    // Update wallet pill in navbar
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

        // ── GET: /Transaction/History ─────────────────────────────────────────────
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
            ViewBag.WalletBalance = user.WalletBalance ?? 0m;
            ViewBag.TotalUnits = history.Sum(t => t.Quantity);
            ViewBag.LastDate = history.Any()
                                    ? history.First().TransactionDate.ToString("MMM dd, yyyy")
                                    : "—";

            return View(history);
        }
    }
}