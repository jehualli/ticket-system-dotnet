using Jehualli.TicketSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Jehualli.TicketSystem.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Comment> Comments => Set<Comment>();

    // SQL Server's datetime2 has no time zone, so values come back as DateTimeKind.Unspecified and
    // serialize without a "Z". Everything is stored in UTC, so mark it as such when reading.
    protected override void ConfigureConventions(ModelConfigurationBuilder conventions)
    {
        conventions.Properties<DateTime>().HaveConversion<UtcConverter>();
        conventions.Properties<DateTime?>().HaveConversion<UtcConverter>();
    }

    private class UtcConverter() : ValueConverter<DateTime, DateTime>(
        v => v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.Property(u => u.Name).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        model.Entity<Client>(e =>
        {
            e.ToTable("Clients");
            e.Property(c => c.CompanyName).HasMaxLength(150).IsRequired();
            e.Property(c => c.ContactName).HasMaxLength(100).IsRequired();
            e.Property(c => c.Email).HasMaxLength(150).IsRequired();
            e.HasIndex(c => c.Email).IsUnique();
            e.Property(c => c.Phone).HasMaxLength(30);
            e.HasOne(c => c.User).WithOne().HasForeignKey<Client>(c => c.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<Ticket>(e =>
        {
            e.ToTable("Tickets");
            e.Property(t => t.Title).HasMaxLength(255).IsRequired();
            e.Property(t => t.Description).IsRequired();
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(t => t.Priority).HasConversion<string>().HasMaxLength(10);
            e.Property(t => t.Category).HasConversion<string>().HasMaxLength(20);
            e.HasOne(t => t.Client).WithMany().HasForeignKey(t => t.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            // SQL Server rejects multiple cascade paths to Users, so these two do nothing on delete.
            e.HasOne(t => t.Assignee).WithMany().HasForeignKey(t => t.AssigneeId)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedById)
                .OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(t => t.Status);
            e.HasIndex(t => t.CreatedAt);
        });

        model.Entity<Comment>(e =>
        {
            e.ToTable("Comments");
            e.Property(c => c.Content).IsRequired();
            e.HasOne(c => c.Ticket).WithMany(t => t.Comments).HasForeignKey(c => c.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.Author).WithMany().HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    // Stamps CreatedAt / UpdatedAt / ResolvedAt so services never have to remember to.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                switch (entry.Entity)
                {
                    case User u: u.CreatedAt = now; break;
                    case Client c: c.CreatedAt = now; break;
                    case Comment c: c.CreatedAt = now; break;
                    case Ticket t: t.CreatedAt = now; t.UpdatedAt = now; break;
                }
            }
            else if (entry.State == EntityState.Modified && entry.Entity is Ticket modified)
            {
                modified.UpdatedAt = now;
            }

            if (entry.Entity is Ticket ticket
                && ticket.Status is TicketStatus.Resolved or TicketStatus.Closed
                && ticket.ResolvedAt is null)
            {
                ticket.ResolvedAt = now;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
