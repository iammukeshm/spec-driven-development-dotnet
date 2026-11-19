using System.Net;
using System.Net.Http.Json;
using Catalog.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Catalog.Tests;

public class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"catalog-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Catalog", $"Data Source={_dbPath}");
}

public class ProductTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Created_product_is_listed()
    {
        var ct = TestContext.Current.CancellationToken;
        var created = await _client.PostAsJsonAsync("/products", new CreateProductRequest("Mug", 12.50m), ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/products", ct);
        Assert.Contains(products!, p => p.Name == "Mug");
    }
}

public class ProductTagTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> CreateProductAsync()
    {
        var response = await _client.PostAsJsonAsync("/products", new CreateProductRequest("Item", 1m), Ct);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(Ct);
        return product!.Id;
    }

    private Task<HttpResponseMessage> AddTagAsync(int id, string tag) =>
        _client.PostAsJsonAsync($"/products/{id}/tags", new { tag }, Ct);

    private async Task<ProductResponse> GetProductAsync(int id)
    {
        var products = await _client.GetFromJsonAsync<List<ProductResponse>>("/products", Ct);
        return products!.Single(p => p.Id == id);
    }

    [Fact]
    public async Task Add_trims_and_lowercases_tag()
    {
        var id = await CreateProductAsync();
        var response = await AddTagAsync(id, " Kitchen ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(Ct);
        Assert.Equal(["kitchen"], product!.Tags);
    }

    [Fact]
    public async Task Adding_existing_tag_returns_200_and_changes_nothing()
    {
        var id = await CreateProductAsync();
        await AddTagAsync(id, "kitchen");
        var response = await AddTagAsync(id, "kitchen");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["kitchen"], (await GetProductAsync(id)).Tags);
    }

    [Fact]
    public async Task Sixth_tag_returns_400()
    {
        var id = await CreateProductAsync();
        foreach (var tag in new[] { "a", "b", "c", "d", "e" })
        {
            Assert.Equal(HttpStatusCode.OK, (await AddTagAsync(id, tag)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await AddTagAsync(id, "gift")).StatusCode);
        Assert.Equal(5, (await GetProductAsync(id)).Tags.Count);
    }

    [Fact]
    public async Task Existing_tag_on_full_product_still_returns_200()
    {
        var id = await CreateProductAsync();
        foreach (var tag in new[] { "a", "b", "c", "d", "e" })
        {
            await AddTagAsync(id, tag);
        }

        Assert.Equal(HttpStatusCode.OK, (await AddTagAsync(id, "e")).StatusCode);
    }

    [Theory]
    [InlineData("gift wrap")]
    [InlineData("gift_wrap")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("café")]
    public async Task Invalid_tag_returns_400(string tag)
    {
        var id = await CreateProductAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await AddTagAsync(id, tag)).StatusCode);
    }

    [Fact]
    public async Task Tag_length_boundaries()
    {
        var id = await CreateProductAsync();
        Assert.Equal(HttpStatusCode.OK, (await AddTagAsync(id, new string('a', 30))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await AddTagAsync(id, new string('a', 31))).StatusCode);
    }

    [Fact]
    public async Task Digits_and_hyphens_are_allowed()
    {
        var id = await CreateProductAsync();
        Assert.Equal(HttpStatusCode.OK, (await AddTagAsync(id, "gift-2025")).StatusCode);
    }

    [Fact]
    public async Task Add_tag_to_unknown_product_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await AddTagAsync(99999, "kitchen")).StatusCode);
    }

    [Fact]
    public async Task Delete_tag_returns_204_and_removes_it()
    {
        var id = await CreateProductAsync();
        await AddTagAsync(id, "kitchen");

        var response = await _client.DeleteAsync($"/products/{id}/tags/kitchen", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await GetProductAsync(id)).Tags);
    }

    [Fact]
    public async Task Delete_missing_tag_returns_404()
    {
        var id = await CreateProductAsync();
        await AddTagAsync(id, "gift");

        var response = await _client.DeleteAsync($"/products/{id}/tags/kitchen", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_tag_on_unknown_product_returns_404()
    {
        var response = await _client.DeleteAsync("/products/99999/tags/kitchen", Ct);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Filter_by_tag_returns_only_matching_products()
    {
        var tagged = await CreateProductAsync();
        var other = await CreateProductAsync();
        await AddTagAsync(tagged, "filter-me");
        await AddTagAsync(other, "something-else");

        var filtered = await _client.GetFromJsonAsync<List<ProductResponse>>("/products?tag=Filter-Me", Ct);
        Assert.Equal([tagged], filtered!.Select(p => p.Id));

        var all = await _client.GetFromJsonAsync<List<ProductResponse>>("/products", Ct);
        Assert.Contains(all!, p => p.Id == other);
        Assert.Contains(all!, p => p.Id == tagged);
    }

    [Fact]
    public async Task Listed_tags_are_sorted()
    {
        var id = await CreateProductAsync();
        foreach (var tag in new[] { "zebra", "apple", "mango" })
        {
            await AddTagAsync(id, tag);
        }

        Assert.Equal(["apple", "mango", "zebra"], (await GetProductAsync(id)).Tags);
    }
}
