using Microsoft.EntityFrameworkCore;

namespace Catalog.Api;

public class Product
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public List<ProductTag> Tags { get; set; } = [];
}

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductTag> ProductTags => Set<ProductTag>();

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
            tag.Property(t => t.Tag).HasMaxLength(TagRules.MaxTagLength);
            tag.HasIndex(t => t.Tag);
        });
    }
}

public record CreateProductRequest(string Name, decimal Price);

public record ProductResponse(int Id, string Name, decimal Price, IReadOnlyList<string> Tags)
{
    public static ProductResponse From(Product product) =>
        new(product.Id, product.Name, product.Price,
            product.Tags.Select(t => t.Tag).Order(StringComparer.Ordinal).ToList());
}

public static class ProductEndpoints
{
    private static Dictionary<string, string[]> InvalidTag() => new()
    {
        ["tag"] = [$"Tag must be 1 to {TagRules.MaxTagLength} characters of a-z, 0-9 or '-'."],
    };

    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products");

        group.MapGet("/", async (string? tag, CatalogDbContext db, CancellationToken cancellationToken) =>
        {
            var query = db.Products.AsNoTracking().Include(p => p.Tags).AsQueryable();

            if (tag is not null)
            {
                var normalized = TagRules.Normalize(tag);
                if (!TagRules.IsValid(normalized))
                {
                    return Results.ValidationProblem(InvalidTag());
                }

                query = query.Where(p => p.Tags.Any(t => t.Tag == normalized));
            }

            var products = await query.OrderBy(p => p.Id).ToListAsync(cancellationToken);
            return Results.Ok(products.Select(ProductResponse.From).ToList());
        });

        group.MapPost("/{id:int}/tags", async (int id, AddTagRequest request, CatalogDbContext db, CancellationToken cancellationToken) =>
        {
            var product = await db.Products.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (product is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found.");
            }

            var tag = TagRules.Normalize(request.Tag);
            if (!TagRules.IsValid(tag))
            {
                return Results.ValidationProblem(InvalidTag());
            }

            if (product.Tags.Any(t => t.Tag == tag))
            {
                return Results.Ok(ProductResponse.From(product));
            }

            if (product.Tags.Count >= TagRules.MaxTagsPerProduct)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["tag"] = [$"A product can have at most {TagRules.MaxTagsPerProduct} tags."],
                });
            }

            product.Tags.Add(new ProductTag { ProductId = id, Tag = tag });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent request inserted the same (ProductId, Tag): treat as already present.
                db.ChangeTracker.Clear();
                product = await db.Products.AsNoTracking().Include(p => p.Tags)
                    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
                if (product is null || !product.Tags.Any(t => t.Tag == tag))
                {
                    throw;
                }
            }

            return Results.Ok(ProductResponse.From(product));
        });

        group.MapDelete("/{id:int}/tags/{tag}", async (int id, string tag, CatalogDbContext db, CancellationToken cancellationToken) =>
        {
            var normalized = TagRules.Normalize(tag);
            var product = await db.Products.Include(p => p.Tags).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            var existing = TagRules.IsValid(normalized) ? product?.Tags.FirstOrDefault(t => t.Tag == normalized) : null;

            if (product is null || existing is null)
            {
                return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Product or tag not found.");
            }

            product.Tags.Remove(existing);
            db.ProductTags.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
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
            return Results.Created($"/products/{product.Id}", ProductResponse.From(product));
        });
    }
}
