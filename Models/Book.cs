using System.ComponentModel.DataAnnotations;

namespace BookStoreApp.Models
{
    public class Book
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Author")]
        public string Author { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Price")]
        [Range(0.01, 9999.99)]
        public decimal Price { get; set; }

        [Display(Name = "Cover Image")]
        public string? CoverImage { get; set; }

        [Range(0, 999)]
        public int Stock { get; set; }

        public int CategoryId { get; set; }

        // NEW SAFE PROPERTIES
        public string? ISBN { get; set; }
        public string? Description { get; set; }
        public DateTime? PublishedDate { get; set; }
        public string? Publisher { get; set; }
        public string? CategoryName { get; set; }
    }
}