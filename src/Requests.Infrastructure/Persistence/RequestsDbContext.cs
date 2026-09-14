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

            // Composites cover the two halves of the authorization predicate plus the default sort.
            entity.HasIndex(r => new { r.OwnerId, r.CreatedAt });
            entity.HasIndex(r => new { r.AssignedToUserId, r.CreatedAt });

            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.RequestType);
            entity.HasIndex(r => r.CreatedAt);

            // Non-unique: nothing in the model enforces uniqueness of the request number.
            entity.HasIndex(r => r.RequestNumber);
        });

        // The InMemory provider ignores relational indexes; these document the intended production
        // schema the search query depends on at scale.
    }
}
