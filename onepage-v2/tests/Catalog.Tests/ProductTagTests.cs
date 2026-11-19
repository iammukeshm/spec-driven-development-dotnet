using System.Net;
using System.Net.Http.Json;
using Catalog.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Catalog.Tests;

public class ProductTagTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> CreateProduct(string name = "Mug")
    {
        var response = await _client.PostAsJsonAsync("/products", new CreateProductRequest(name, 10m), Ct);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(Ct))!.Id;
    }

    private Task<HttpResponseMessage> AddTag(int id, string tag) =>
        _client.PostAsJsonAsync($"/products/{id}/tags", new AddTagRequest(tag), Ct);

    private static async Task<string[]> TagsOf(HttpResponseMessage response) =>
        [.. (await response.Content.ReadFromJsonAsync<ProductResponse>(Ct))!.Tags];

    [Fact]
    public async Task Tag_is_trimmed_and_lowercased()
    {
        var id = await CreateProduct();
        var response = await AddTag(id, " Kitchen ");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["kitchen"], await TagsOf(response));
    }

    [Fact]
    public async Task Adding_existing_tag_returns_200_and_changes_nothing()
    {
        var id = await CreateProduct();
        await AddTag(id, "kitchen");
        var response = await AddTag(id, "kitchen");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["kitchen"], await TagsOf(response));
    }

    [Fact]
    public async Task Adding_existing_tag_with_different_casing_is_a_duplicate()
    {
        var id = await CreateProduct();
        await AddTag(id, "kitchen");
        var response = await AddTag(id, "KITCHEN");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["kitchen"], await TagsOf(response));
    }

    [Fact]
    public async Task Sixth_tag_returns_400()
    {
        var id = await CreateProduct();
        foreach (var t in new[] { "a", "b", "c", "d", "e" })
        {
            Assert.Equal(HttpStatusCode.OK, (await AddTag(id, t)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await AddTag(id, "gift")).StatusCode);

        // An existing tag is still a no-op at the limit.
        var again = await AddTag(id, "a");
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(["a", "b", "c", "d", "e"], await TagsOf(again));
    }

    [Theory]
    [InlineData("gift wrap")]
    [InlineData("gift_wrap")]
    [InlineData("gift!")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this-tag-is-way-too-long-to-be-valid")]
    public async Task Invalid_tag_returns_400(string tag)
    {
        var id = await CreateProduct();
        Assert.Equal(HttpStatusCode.BadRequest, (await AddTag(id, tag)).StatusCode);
    }

    [Fact]
    public async Task Missing_tag_returns_400()
    {
        var id = await CreateProduct();
        var response = await _client.PostAsJsonAsync($"/products/{id}/tags", new AddTagRequest(null), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Tag_of_30_chars_is_valid_and_31_is_not()
    {
        var id = await CreateProduct();
        Assert.Equal(HttpStatusCode.OK, (await AddTag(id, new string('a', 30))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AddTag(id, new string('a', 31))).StatusCode);
    }

    [Fact]
    public async Task Digits_and_hyphens_are_valid()
    {
        var id = await CreateProduct();
        var response = await AddTag(id, "gift-2024");
        Assert.Equal(["gift-2024"], await TagsOf(response));
    }

    [Fact]
    public async Task Delete_removes_tag_and_returns_204()
    {
        var id = await CreateProduct();
        await AddTag(id, "kitchen");
        var response = await _client.DeleteAsync($"/products/{id}/tags/kitchen", Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/products", Ct);
        Assert.Empty(products!.Single(p => p.Id == id).Tags);
    }

    [Fact]
    public async Task Delete_missing_tag_returns_404()
    {
        var id = await CreateProduct();
        await AddTag(id, "gift");
        var response = await _client.DeleteAsync($"/products/{id}/tags/kitchen", Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_product_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await AddTag(999_999, "kitchen")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/products/999999/tags/kitchen", Ct)).StatusCode);
    }

    [Fact]
    public async Task List_includes_sorted_tags()
    {
        var id = await CreateProduct();
        await AddTag(id, "zebra");
        await AddTag(id, "apple");
        await AddTag(id, "mango");
        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/products", Ct);
        Assert.Equal(["apple", "mango", "zebra"], products!.Single(p => p.Id == id).Tags);
    }

    [Fact]
    public async Task Filter_by_tag_returns_only_matching_products()
    {
        var tagged = await CreateProduct("Tagged");
        var other = await CreateProduct("Other");
        await AddTag(tagged, "filter-me");
        await AddTag(tagged, "extra");
        await AddTag(other, "different");

        var filtered = await _client.GetFromJsonAsync<List<ProductResponse>>("/products?tag=Filter-Me", Ct);
        var match = Assert.Single(filtered!);
        Assert.Equal(tagged, match.Id);
        Assert.Equal(["extra", "filter-me"], match.Tags);

        var all = await _client.GetFromJsonAsync<List<ProductResponse>>("/products", Ct);
        Assert.Contains(all!, p => p.Id == other);
        Assert.Contains(all!, p => p.Id == tagged);
    }

    [Fact]
    public async Task Filter_by_unknown_tag_returns_empty_list()
    {
        await CreateProduct();
        var filtered = await _client.GetFromJsonAsync<List<ProductResponse>>("/products?tag=nothing-has-this", Ct);
        Assert.Empty(filtered!);
    }
}

public class LegacyDatabaseTests
{
    [Fact]
    public async Task Database_created_by_previous_version_upgrades_and_keeps_data()
    {
        var ct = TestContext.Current.CancellationToken;
        var dbPath = Path.Combine(Path.GetTempPath(), $"catalog-legacy-{Guid.NewGuid():N}.db");

        // Schema exactly as the previous version's EnsureCreated produced it (no tags table).
        await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE "Products" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Products" PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT NOT NULL,
                    "Price" TEXT NOT NULL
                );
                INSERT INTO "Products" ("Name", "Price") VALUES ('Old mug', '9.50');
                """;
            await command.ExecuteNonQueryAsync(ct);
        }

        try
        {
            await using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Catalog", $"Data Source={dbPath}"));
            using var client = factory.CreateClient();

            var products = await client.GetFromJsonAsync<List<ProductResponse>>("/products", ct);
            var old = Assert.Single(products!);
            Assert.Equal("Old mug", old.Name);
            Assert.Equal(9.50m, old.Price);
            Assert.Empty(old.Tags);

            var response = await client.PostAsJsonAsync($"/products/{old.Id}/tags", new AddTagRequest("kitchen"), ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(dbPath);
        }
    }
}
