using System.ComponentModel.DataAnnotations.Schema;

namespace BookStoreApp.Models
{
    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }   // ✅ FIXED

        public int Stock { get; set; } = 0;
        public string? CoverImage { get; set; }
    }
}