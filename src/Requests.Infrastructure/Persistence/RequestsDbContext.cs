using Microsoft.EntityFrameworkCore;
using Requests.Domain.Entities;

namespace Requests.Infrastructure.Persistence;

public class RequestsDbContext : DbContext
{
    public RequestsDbContext(DbContextOptions<RequestsDbContext> options) : base(options)
    {
    }

    public DbSet<Request> Requests => Set<Request>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Request>(entity =>
        {
            entity.HasKey(r => r.Id);

            // Composite indexes covering the two halves of the authorization predicate together
            // with the default sort, so a regular user's page can be served from an index.
            entity.HasIndex(r => new { r.OwnerId, r.CreatedAt });
            entity.HasIndex(r => new { r.AssignedToUserId, r.CreatedAt });

            // Single-column indexes for the optional filters and the administrator default sort.
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.RequestType);
            entity.HasIndex(r => r.CreatedAt);

            // Non-unique on purpose: the seeder happens to generate distinct values, but nothing in
            // the model enforces uniqueness, so asserting it here would be incorrect.
            entity.HasIndex(r => r.RequestNumber);
        });

        // Note: the EF Core InMemory provider ignores relational indexes entirely. They are declared
        // here to document the intended production schema, which is what the read query above needs
        // in order to stay efficient at scale on a relational provider.
    }
}
