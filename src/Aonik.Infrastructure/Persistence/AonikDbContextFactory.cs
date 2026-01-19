using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aonik.Infrastructure.Persistence;

public class AonikDbContextFactory : IDesignTimeDbContextFactory<AonikDbContext>
{
    public AonikDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AonikDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=AonikDb;Trusted_Connection=True;TrustServerCertificate=True;");

        return new AonikDbContext(optionsBuilder.Options);
    }
}
