using Microsoft.EntityFrameworkCore;

namespace C2C.Web.Data;

public class QuoteRequest
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string ServicesRequired { get; set; } = "";
    public string? StartTiming { get; set; }
    public bool Handled { get; set; }
}

public class JobApplication
{
    public int Id { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Company { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Field { get; set; } = "";
    public string Profile { get; set; } = "";
    public string OriginalFileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public bool Handled { get; set; }
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<QuoteRequest> Quotes => Set<QuoteRequest>();
    public DbSet<JobApplication> Applications => Set<JobApplication>();
}
