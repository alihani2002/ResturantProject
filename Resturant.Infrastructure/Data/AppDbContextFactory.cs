using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Resturant.Infrastructure.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseSqlServer("Server=db52974.public.databaseasp.net; Database=db52974; User Id=db52974; Password=d@3R+9EomS=7; Encrypt=True; TrustServerCertificate=True; MultipleActiveResultSets=True;");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
