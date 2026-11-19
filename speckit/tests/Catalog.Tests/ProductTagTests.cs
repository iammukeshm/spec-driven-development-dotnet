using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Catalog.Api;

namespace Catalog.Tests;

public class ProductTagTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> CreateProductAsync(string name = "Mug")
    {
        var response = await _client.PostAsJsonAsync("/products", new CreateProductRequest(name, 10m), Ct);
        response.EnsureSuccessStatusCode();
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(Ct);
        return product!.Id;
    }

    private Task<HttpResponseMessage> AddTagAsync(int productId, string? tag) =>
        _client.PostAsJsonAsync($"/products/{productId}/tags", new AddTagRequest(tag), Ct);

    private Task<HttpResponseMessage> RemoveTagAsync(int productId, string tag) =>
        _client.DeleteAsync($"/products/{productId}/tags/{Uri.EscapeDataString(tag)}", Ct);

    private async Task<List<ProductResponse>> GetProductsAsync(string? tag = null)
    {
        var url = tag is null ? "/products" : $"/products?tag={Uri.EscapeDataString(tag)}";
        return (await _client.GetFromJsonAsync<List<ProductResponse>>(url, Ct))!;
    }

    private async Task<ProductResponse> GetProductAsync(int id) =>
        (await GetProductsAsync()).Single(p => p.Id == id);

    private static async Task AssertTagErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("tag", out _));
    }

    private async Task AddTagsAsync(int productId, params string[] tags)
    {
        foreach (var tag in tags)
        {
            Assert.Equal(HttpStatusCode.OK, (await AddTagAsync(productId, tag)).StatusCode);
        }
    }

    // ---------- US1: add a tag ----------

    [Fact]
    public async Task Add_tag_returns_product_with_tag()
    {
        var id = await CreateProductAsync();
        var response = await AddTagAsync(id, "kitchen");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<ProductResponse>(Ct);
        Assert.Equal(["kitchen"], product!.Tags);
    }

    [Fact]
    public async Task Add_tag_is_trimmed_and_lowercased()
    {
        var id = await CreateProductAsync();
        await AddTagAsync(id, "  Gift  ");

        Assert.Equal(["gift"], (await GetProductAsync(id)).Tags);
    }

    [Fact]
    public async Task Adding_existing_tag_is_idempotent_even_in_different_case()
    {
        var id = await CreateProductAsync();
        await AddTagAsync(id, "gift");
        var again = await AddTagAsync(id, "GIFT");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(["gift"], (await GetProductAsync(id)).Tags);
    }

    [Fact]
    public async Task Adding_existing_tag_at_limit_returns_ok_unchanged()
    {
        var id = await CreateProductAsync();
        await AddTagsAsync(id, "a", "b", "c", "d", "e");

        var again = await AddTagAsync(id, "c");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(["a", "b", "c", "d", "e"], (await GetProductAsync(id)).Tags);
    }

    [Fact]
    public async Task Sixth_distinct_tag_is_rejected()
    {
        var id = await CreateProductAsync();
        await AddTagsAsync(id, "a", "b", "c", "d", "e");

        await AssertTagErrorAsync(await AddTagAsync(id, "f"));
        Assert.Equal(5, (await GetProductAsync(id)).Tags.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("gift set")]
    [InlineData("under_score")]
    [InlineData("café")]
    [InlineData("Bad!")]
    public async Task Invalid_tag_is_rejected_and_product_unchanged(string tag)
    {
        var id = await CreateProductAsync();

        await AssertTagErrorAsync(await AddTagAsync(id, tag));
        Assert.Empty((await GetProductAsync(id)).Tags);
    }

    [Fact]
    public async Task Missing_tag_value_is_rejected()
    {
        var id = await CreateProductAsync();
        await AssertTagErrorAsync(await AddTagAsync(id, null));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("123456789012345678901234567890")]
    [InlineData("-")]
    public async Task Boundary_tags_are_accepted(string tag)
    {
        var id = await CreateProductAsync();

        Assert.Equal(HttpStatusCode.OK, (await AddTagAsync(id, tag)).StatusCode);
        Assert.Equal([tag], (await GetProductAsync(id)).Tags);
    }

    [Fact]
    public async Task Add_tag_to_unknown_product_returns_404()
    {
        var response = await AddTagAsync(999_999, "kitchen");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_product_wins_over_bad_tag()
    {
        var response = await AddTagAsync(999_999, "Bad!");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Tags_persist_and_are_scoped_per_product()
    {
        var first = await CreateProductAsync("One");
        var second = await CreateProductAsync("Two");

        await AddTagAsync(first, "shared");
        Assert.Equal(HttpStatusCode.OK, (await AddTagAsync(second, "shared")).StatusCode);

        Assert.Equal(["shared"], (await GetProductAsync(first)).Tags);
        Assert.Equal(["shared"], (await GetProductAsync(second)).Tags);
    }

    // ---------- US2: filter by tag ----------

    [Fact]
    public async Task Filter_returns_only_tagged_products_ordered_by_id()
    {
        var a = await CreateProductAsync("A");
        var b = await CreateProductAsync("B");
        var c = await CreateProductAsync("C");
        await AddTagAsync(b, "filter-gift");
        await AddTagAsync(a, "filter-gift");

        var result = await GetProductsAsync("filter-gift");

        Assert.Equal([a, b], result.Select(p => p.Id).ToArray());
        Assert.DoesNotContain(result, p => p.Id == c);
    }

    [Fact]
    public async Task Filter_by_unused_tag_returns_empty_list()
    {
        await CreateProductAsync();

        Assert.Empty(await GetProductsAsync("never-used"));
    }

    [Fact]
    public async Task Filter_is_normalized()
    {
        var id = await CreateProductAsync();
        await AddTagAsync(id, "norm-gift");

        var result = await GetProductsAsync("  NORM-GIFT ");

        Assert.Equal([id], result.Select(p => p.Id).ToArray());
    }

    [Fact]
    public async Task No_filter_returns_all_products()
    {
        var tagged = await CreateProductAsync();
        var untagged = await CreateProductAsync();
        await AddTagAsync(tagged, "some-tag");

        var all = await GetProductsAsync();

        Assert.Contains(all, p => p.Id == tagged);
        Assert.Contains(all, p => p.Id == untagged);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456789012345678901")]
    [InlineData("bad tag")]
    [InlineData("Bad!")]
    public async Task Invalid_filter_returns_400(string tag)
    {
        var response = await _client.GetAsync($"/products?tag={Uri.EscapeDataString(tag)}", Ct);

        await AssertTagErrorAsync(response);
    }

    // ---------- US3: tags in the list ----------

    [Fact]
    public async Task Tags_are_sorted_ordinally()
    {
        var id = await CreateProductAsync();
        await AddTagsAsync(id, "zebra", "apple", "mango");
        Assert.Equal(["apple", "mango", "zebra"], (await GetProductAsync(id)).Tags);

        var digits = await CreateProductAsync();
        await AddTagsAsync(digits, "a1", "a-b", "1-a");
        Assert.Equal(["1-a", "a-b", "a1"], (await GetProductAsync(digits)).Tags);
    }

    [Fact]
    public async Task Untagged_product_shows_empty_tags()
    {
        var id = await CreateProductAsync();

        Assert.Empty((await GetProductAsync(id)).Tags);
    }

    [Fact]
    public async Task Filtered_result_shows_all_tags_of_product()
    {
        var id = await CreateProductAsync();
        await AddTagsAsync(id, "kitchen", "show-all");

        var result = await GetProductsAsync("show-all");

        Assert.Equal(["kitchen", "show-all"], result.Single(p => p.Id == id).Tags);
    }

    // ---------- US4: remove a tag ----------

    [Fact]
    public async Task Remove_tag_returns_204_and_tag_is_gone()
    {
        var id = await CreateProductAsync();
        await AddTagAsync(id, "rm-kitchen");

        var response = await RemoveTagAsync(id, "rm-kitchen");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await GetProductAsync(id)).Tags);
        Assert.DoesNotContain(await GetProductsAsync("rm-kitchen"), p => p.Id == id);
    }

    [Fact]
    public async Task Remove_tag_is_normalized()
    {
        var id = await CreateProductAsync();
        await AddTagAsync(id, "rm-norm");

        var response = await RemoveTagAsync(id, "  RM-NORM ");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await GetProductAsync(id)).Tags);
    }

    [Fact]
    public async Task Remove_absent_tag_returns_404_and_product_unchanged()
    {
        var id = await CreateProductAsync();
        await AddTagAsync(id, "keep");

        var response = await RemoveTagAsync(id, "other");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(["keep"], (await GetProductAsync(id)).Tags);
    }

    [Fact]
    public async Task Remove_tag_from_unknown_product_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await RemoveTagAsync(999_999, "kitchen")).StatusCode);
    }

    [Fact]
    public async Task Remove_malformed_tag_returns_404()
    {
        var id = await CreateProductAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await RemoveTagAsync(id, "Bad Tag!")).StatusCode);
    }

    [Fact]
    public async Task Removing_a_tag_at_limit_frees_a_slot()
    {
        var id = await CreateProductAsync();
        await AddTagsAsync(id, "a", "b", "c", "d", "e");

        Assert.Equal(HttpStatusCode.NoContent, (await RemoveTagAsync(id, "a")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await AddTagAsync(id, "f")).StatusCode);
        Assert.Equal(["b", "c", "d", "e", "f"], (await GetProductAsync(id)).Tags);
    }
}
