using Microsoft.EntityFrameworkCore;

namespace Catalog.Api;

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public List<ProductTag> Tags { get; set; } = [];
}

public class ProductTag
{
    public int ProductId { get; set; }
    public required string Name { get; set; }
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
            product.HasMany(p => p.Tags).WithOne().HasForeignKey(t => t.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProductTag>(tag =>
        {
            tag.HasKey(t => new { t.ProductId, t.Name });
            tag.Property(t => t.Name).HasMaxLength(30);
        });
    }
}

public record CreateProductRequest(string Name, decimal Price);

public record AddTagRequest(string? Tag);

public record ProductResponse(int Id, string Name, decimal Price, IReadOnlyList<string> Tags);

public static class ProductEndpoints
{
    private const int MaxTagsPerProduct = 5;

    private static string NormalizeTag(string? tag) => (tag ?? string.Empty).Trim().ToLowerInvariant();

    private static bool IsValidTag(string tag) =>
        tag.Length is >= 1 and <= 30 && tag.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');

    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products");

        group.MapGet("/", async (string? tag, CatalogDbContext db, CancellationToken cancellationToken) =>
        {
            var query = db.Products.AsNoTracking();
            if (tag is not null)
            {
                var normalized = NormalizeTag(tag);
                query = query.Where(p => p.Tags.Any(t => t.Name == normalized));
            }

            var products = await query
                .OrderBy(p => p.Id)
                .Select(p => new { p.Id, p.Name, p.Price, Tags = p.Tags.Select(t => t.Name).ToList() })
                .ToListAsync(cancellationToken);

            return products.Select(p => new ProductResponse(p.Id, p.Name, p.Price, SortTags(p.Tags)));
        });

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
            return Results.Created($"/products/{product.Id}", new ProductResponse(product.Id, product.Name, product.Price, []));
        });

        group.MapPost("/{id:int}/tags", async (int id, AddTagRequest request, CatalogDbContext db, CancellationToken cancellationToken) =>
        {
            var product = await db.Products.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (product is null)
            {
                return Results.NotFound();
            }

            var tag = NormalizeTag(request.Tag);
            if (!IsValidTag(tag))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["tag"] = ["Tag must be 1-30 characters: lowercase letters, digits and hyphens only."],
                });
            }

            if (product.Tags.All(t => t.Name != tag))
            {
                if (product.Tags.Count >= MaxTagsPerProduct)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["tag"] = [$"A product can have at most {MaxTagsPerProduct} tags."],
                    });
                }

                product.Tags.Add(new ProductTag { ProductId = product.Id, Name = tag });
                await db.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(ToResponse(product));
        });

        group.MapDelete("/{id:int}/tags/{tag}", async (int id, string tag, CatalogDbContext db, CancellationToken cancellationToken) =>
        {
            var product = await db.Products.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            var normalized = NormalizeTag(tag);
            var existing = product?.Tags.FirstOrDefault(t => t.Name == normalized);
            if (product is null || existing is null)
            {
                return Results.NotFound();
            }

            product.Tags.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
    }

    private static List<string> SortTags(IEnumerable<string> tags) => tags.Order(StringComparer.Ordinal).ToList();

    private static ProductResponse ToResponse(Product p) =>
        new(p.Id, p.Name, p.Price, SortTags(p.Tags.Select(t => t.Name)));
}
