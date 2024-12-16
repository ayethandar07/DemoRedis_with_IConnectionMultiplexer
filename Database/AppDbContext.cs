using DemoRedis.Model;
using Microsoft.EntityFrameworkCore;

namespace DemoRedis.Database;

public class AppDbContext : DbContext
{
    public DbSet<Employee> Employees { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=employees.db");
    }
}