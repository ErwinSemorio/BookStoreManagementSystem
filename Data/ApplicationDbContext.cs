using Microsoft.EntityFrameworkCore;
using BookStoreApp.Models;

namespace BookStoreApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Users> Users { get; set; } = null!;
        public DbSet<Book> Books { get; set; } = null!;
        public DbSet<Transaction> Transactions { get; set; } = null!;
    }
}