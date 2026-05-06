using Microsoft.AspNetCore.Mvc;
using BookStoreApp.Data;
using BookStoreApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStoreApp.Controllers
{
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _db;

        public WishlistController(ApplicationDbContext db)
        {
            _db = db;
        }

        private int? GetUserId() => HttpContext.Session.GetInt32("UserId");

        // ── POST: /Wishlist/Toggle ────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Toggle(int bookId)
        {
            var userId = GetUserId();
            if (userId == null)
                return Json(new { success = false, message = "Not logged in" });

            var existing = await _db.Wishlists
                .FirstOrDefaultAsync(w => w.UserId == userId && w.BookId == bookId);

            if (existing != null)
            {
                // Already in wishlist — remove it
                _db.Wishlists.Remove(existing);
                await _db.SaveChangesAsync();
                return Json(new { success = true, added = false, message = "Removed from wishlist" });
            }
            else
            {
                // Not in wishlist — add it
                _db.Wishlists.Add(new Wishlist
                {
                    UserId = userId.Value,
                    BookId = bookId,
                    AddedDate = DateTime.Now
                });
                await _db.SaveChangesAsync();
                return Json(new { success = true, added = true, message = "Added to wishlist" });
            }
        }

        // ── POST: /Wishlist/Remove ────────────────────────────────────────────
        [HttpPost]
        public async Task<IActionResult> Remove(int bookId)
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var item = await _db.Wishlists
                .FirstOrDefaultAsync(w => w.UserId == userId && w.BookId == bookId);

            if (item != null)
            {
                _db.Wishlists.Remove(item);
                await _db.SaveChangesAsync();
                TempData["Success"] = "Book removed from wishlist.";
            }

            return RedirectToAction("Profile", "Account");
        }
    }
}