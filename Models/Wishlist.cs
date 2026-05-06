using System;
using System.ComponentModel.DataAnnotations;
namespace BookStoreApp.Models
{
    public class Wishlist
    {
        public int Id { get; set; }
        public int UserId { get; set; } = 0;
        public int BookId { get; set; } = 0;
        public DateTime AddedDate { get; set; } = DateTime.Now;
        public Users? User { get; set; }
        public Book? Book { get; set; }
    }
}
