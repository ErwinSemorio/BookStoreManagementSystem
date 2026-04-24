namespace BookStoreApp.Models
{
    public class Book
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Stock { get; set; } = 0;

        // Add this property if you want to store a book cover image
        public string? CoverImage { get; set; }
    }
}