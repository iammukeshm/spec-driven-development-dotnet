// Written by hand from docs/specs/product-tags.md, before reading either implementation.
// The same file runs against the one-page build and the Spec Kit build.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Catalog.Tests;

public class SpecAcceptanceTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> NewProduct()
    {
        var response = await _client.PostAsJsonAsync("/products", new { name = "Item", price = 1m }, Ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return doc.RootElement.GetProperty("id").GetInt32();
    }

    private Task<HttpResponseMessage> Add(int id, string tag) =>
        _client.PostAsJsonAsync($"/products/{id}/tags", new { tag }, Ct);

    private static async Task<string[]> TagsOf(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return [.. doc.RootElement.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!)];
    }

    private async Task<string[]> ListedTags(int id)
    {
        using var doc = JsonDocument.Parse(await _client.GetStringAsync("/products", Ct));
        var product = doc.RootElement.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == id);
        return [.. product.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!)];
    }

    [Fact] // Example 1
    public async Task Add_trims_and_lowercases()
    {
        var id = await NewProduct();
        var response = await Add(id, " Kitchen ");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["kitchen"], await TagsOf(response));
    }

    [Fact] // Example 2, rule 3
    public async Task Adding_existing_tag_is_200_and_changes_nothing()
    {
        var id = await NewProduct();
        await Add(id, "kitchen");
        var response = await Add(id, "kitchen");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["kitchen"], await ListedTags(id));
    }

    [Fact] // Example 3, rule 1
    public async Task Sixth_tag_is_400()
    {
        var id = await NewProduct();
        foreach (var t in new[] { "a", "b", "c", "d", "e" }) await Add(id, t);
        Assert.Equal(HttpStatusCode.BadRequest, (await Add(id, "gift")).StatusCode);
    }

    [Fact] // Rule 3 wins over rule 1: an existing tag on a full product changes nothing
    public async Task Existing_tag_on_full_product_is_200()
    {
        var id = await NewProduct();
        foreach (var t in new[] { "a", "b", "c", "d", "e" }) await Add(id, t);
        Assert.Equal(HttpStatusCode.OK, (await Add(id, "e")).StatusCode);
    }

    [Theory] // Example 4, rule 2
    [InlineData("gift wrap")]
    [InlineData("gift_wrap")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")] // 31 chars
    public async Task Invalid_tag_is_400(string tag)
    {
        var id = await NewProduct();
        Assert.Equal(HttpStatusCode.BadRequest, (await Add(id, tag)).StatusCode);
    }

    [Fact] // Rule 2 boundary
    public async Task Thirty_chars_with_digits_and_hyphens_is_ok()
    {
        var id = await NewProduct();
        Assert.Equal(HttpStatusCode.OK, (await Add(id, "gift-2026-" + new string('x', 20))).StatusCode);
    }

    [Fact] // Example 5, rule 4
    public async Task Removing_missing_tag_is_404()
    {
        var id = await NewProduct();
        await Add(id, "gift");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/products/{id}/tags/kitchen", Ct)).StatusCode);
    }

    [Fact] // Endpoint: DELETE -> 204
    public async Task Removing_tag_is_204()
    {
        var id = await NewProduct();
        await Add(id, "gift");
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/products/{id}/tags/gift", Ct)).StatusCode);
        Assert.Empty(await ListedTags(id));
    }

    [Fact] // Rule 5
    public async Task Unknown_product_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Add(987654, "gift")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/products/987654/tags/gift", Ct)).StatusCode);
    }

    [Fact] // Endpoint: GET ?tag= and sorted tags array
    public async Task Filter_by_tag_and_sorted_tags()
    {
        var tagged = await NewProduct();
        var other = await NewProduct();
        foreach (var t in new[] { "zebra", "apple", "only-here" }) await Add(tagged, t);
        await Add(other, "apple");

        using var doc = JsonDocument.Parse(await _client.GetStringAsync("/products?tag=only-here", Ct));
        Assert.Equal([tagged], doc.RootElement.EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
        Assert.Equal(["apple", "only-here", "zebra"], await ListedTags(tagged));
    }

    [Fact] // Without tag: all products, each with a tags array
    public async Task Untagged_product_lists_empty_tags()
    {
        var id = await NewProduct();
        Assert.Empty(await ListedTags(id));
    }
}
