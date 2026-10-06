using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NebuLog.Server.Identity;

/// <summary>Identity store plus the API-key table.</summary>
public sealed class NebuLogDbContext : IdentityDbContext<NebuLogUser, IdentityRole, string>
{
    /// <summary>Creates the context.</summary>
    /// <param name="options">EF Core options.</param>
    public NebuLogDbContext(DbContextOptions<NebuLogDbContext> options)
        : base(options)
    {
    }

    /// <summary>The issued API keys.</summary>
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApiKey>(entity =>
        {
            entity.HasKey(key => key.Id);
            entity.Property(key => key.Name).HasMaxLength(128).IsRequired();
            entity.Property(key => key.Prefix).HasMaxLength(8).IsRequired();
            entity.Property(key => key.SecretHash).IsRequired();
            entity.Property(key => key.ServiceName).HasMaxLength(256);
            entity.Property(key => key.CreatedBy).HasMaxLength(256).IsRequired();

            // Authentication looks a key up by prefix on every request, so the index is the hot path.
            entity.HasIndex(key => key.Prefix).IsUnique();
        });
    }
}
