using System.Text.RegularExpressions;
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
    public required string Tag { get; set; }
}

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();

    // EnsureCreated skips databases that already have tables, so databases created before tags
    // existed never get the ProductTags table. This idempotent script (kept in sync with the model
    // below) is run on startup to upgrade them. It is a no-op on up-to-date databases.
    public const string EnsureTagsTableSql = """
        CREATE TABLE IF NOT EXISTS "ProductTags" (
            "ProductId" INTEGER NOT NULL,
            "Tag" TEXT NOT NULL,
            CONSTRAINT "PK_ProductTags" PRIMARY KEY ("ProductId", "Tag"),
            CONSTRAINT "FK_ProductTags_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS "IX_ProductTags_Tag" ON "ProductTags" ("Tag");
        """;

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
            tag.ToTable("ProductTags");
            tag.HasKey(t => new { t.ProductId, t.Tag });
            tag.Property(t => t.Tag).HasMaxLength(ProductTagRules.MaxLength);
            tag.HasIndex(t => t.Tag);
        });
    }
}

public record CreateProductRequest(string Name, decimal Price);

public record AddTagRequest(string? Tag);

public record ProductResponse(int Id, string Name, decimal Price, IReadOnlyList<string> Tags);

public static partial class ProductTagRules
{
    public const int MaxTags = 5;
    public const int MaxLength = 30;

    [GeneratedRegex("^[a-z0-9-]{1,30}$")]
    private static partial Regex ValidTag();

    public static string Normalize(string? tag) => (tag ?? "").Trim().ToLowerInvariant();

    public static bool IsValid(string normalized) => ValidTag().IsMatch(normalized);
}

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products");

        group.MapGet("/", async (string? tag, CatalogDbContext db, CancellationToken cancellationToken) =>
        {
            var query = db.Products.AsNoTracking();
            if (tag is not null)
            {
                var normalized = ProductTagRules.Normalize(tag);
                query = query.Where(p => p.Tags.Any(t => t.Tag == normalized));
            }

            var products = await query.OrderBy(p => p.Id).Include(p => p.Tags).ToListAsync(cancellationToken);
            return products.Select(ToResponse).ToList();
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
            return Results.Created($"/products/{product.Id}", ToResponse(product));
        });

        group.MapPost("/{id:int}/tags", async (int id, AddTagRequest request, CatalogDbContext db, CancellationToken cancellationToken) =>
        {
            var product = await db.Products.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (product is null)
            {
                return Results.NotFound();
            }

            var tag = ProductTagRules.Normalize(request.Tag);
            if (!ProductTagRules.IsValid(tag))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["tag"] = [$"A tag must be 1 to {ProductTagRules.MaxLength} characters: lowercase letters, digits and hyphens only."],
                });
            }

            if (product.Tags.All(t => t.Tag != tag))
            {
                if (product.Tags.Count >= ProductTagRules.MaxTags)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["tag"] = [$"A product can have at most {ProductTagRules.MaxTags} tags."],
                    });
                }

                product.Tags.Add(new ProductTag { ProductId = product.Id, Tag = tag });
                await db.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(ToResponse(product));
        });

        group.MapDelete("/{id:int}/tags/{tag}", async (int id, string tag, CatalogDbContext db, CancellationToken cancellationToken) =>
        {
            var normalized = ProductTagRules.Normalize(tag);
            var productTag = await db.ProductTags.FirstOrDefaultAsync(t => t.ProductId == id && t.Tag == normalized, cancellationToken);
            if (productTag is null)
            {
                return Results.NotFound();
            }

            db.ProductTags.Remove(productTag);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
    }

    private static ProductResponse ToResponse(Product p) =>
        new(p.Id, p.Name, p.Price, p.Tags.Select(t => t.Tag).Order(StringComparer.Ordinal).ToList());
}
