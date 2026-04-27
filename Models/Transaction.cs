using System;

namespace BookStoreApp.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int BookId { get; set; }
        public int Quantity { get; set; } = 1;
        public DateTime Date { get; set; } = DateTime.Now;
        public string Status { get; set; } = "Completed";

        // Navigation properties
        public Users? User { get; set; }
        public Book? Book { get; set; }
    }
}