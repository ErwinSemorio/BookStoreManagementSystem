namespace BookStoreApp.Models
{
    public class DashboardViewModel
    {
        public decimal WalletBalance { get; set; }
        public int TotalOrders { get; set; }
        public List<Book> Books { get; set; } = new();
    }
}