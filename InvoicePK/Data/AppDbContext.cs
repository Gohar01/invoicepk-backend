using Microsoft.EntityFrameworkCore;
using InvoicePK.Models;

namespace InvoicePK.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // User
        mb.Entity<User>(e => {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Plan).HasDefaultValue("Trial");
        });

        // Client → User
        mb.Entity<Client>(e => {
            e.HasOne(c => c.User)
             .WithMany(u => u.Clients)
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Invoice → User
        mb.Entity<Invoice>(e => {
            e.HasOne(i => i.User)
             .WithMany(u => u.Invoices)
             .HasForeignKey(i => i.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // Invoice → Client (no cascade to avoid multiple paths)
            e.HasOne(i => i.Client)
             .WithMany(c => c.Invoices)
             .HasForeignKey(i => i.ClientId)
             .OnDelete(DeleteBehavior.Restrict);

            e.Property(i => i.Status).HasDefaultValue("Draft");
        });

        // InvoiceItem → Invoice
        mb.Entity<InvoiceItem>(e => {
            e.HasOne(ii => ii.Invoice)
             .WithMany(i => i.Items)
             .HasForeignKey(ii => ii.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);

            // SubTotal is computed in C#, not stored in DB
            e.Ignore(ii => ii.SubTotal);
        });

        // EmailLog → Invoice
        mb.Entity<EmailLog>(e => {
            e.HasOne(el => el.Invoice)
             .WithMany(i => i.EmailLogs)
             .HasForeignKey(el => el.InvoiceId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
