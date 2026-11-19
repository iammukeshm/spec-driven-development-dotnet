using Microsoft.EntityFrameworkCore;

namespace Catalog.Api;

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
}

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(product =>
        {
            product.Property(p => p.Name).HasMaxLength(200);
            product.Property(p => p.Price).HasPrecision(18, 2);
        });
    }
}

public record CreateProductRequest(string Name, decimal Price);

public record ProductResponse(int Id, string Name, decimal Price);

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products");

        group.MapGet("/", async (CatalogDbContext db, CancellationToken cancellationToken) =>
            await db.Products
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => new ProductResponse(p.Id, p.Name, p.Price))
                .ToListAsync(cancellationToken));

        group.MapPost("/", async (CreateProductRequest request, CatalogDbContext db, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Price < 0)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["product"] = ["Name is required and price cannot be negative."],
                });
            }

            var product = new Product { Name = request.Name.Trim(), Price = request.Price };
            db.Products.Add(product);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/products/{product.Id}", new ProductResponse(product.Id, product.Name, product.Price));
        });
    }
}
