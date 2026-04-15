using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NongTimeAI.Data;

namespace NongTimeAI.Infrastructure;

public class TimesheetDbContextFactory : IDesignTimeDbContextFactory<TimesheetDbContext>
{
    public TimesheetDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TimesheetDbContext>();

        // ใช้ connection string สำหรับ migration
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=nongtimeai;Username=postgres;Password=postgres");

        return new TimesheetDbContext(optionsBuilder.Options);
    }
}
