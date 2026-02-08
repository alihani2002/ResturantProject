using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Resturant.Infrastructure.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer("Server=db40463.public.databaseasp.net; Database=db40463; User Id=db40463; Password=5y%EQ7e=o@3J; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;  ");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
