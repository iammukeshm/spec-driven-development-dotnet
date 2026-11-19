using Catalog.Api;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Catalog") ?? "Data Source=catalog.db"));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    db.Database.EnsureCreated();
    db.Database.ExecuteSqlRaw(CatalogDbContext.EnsureTagsTableSql);
}

app.MapOpenApi();
app.MapScalarApiReference();

app.MapProductEndpoints();

app.Run();

public partial class Program;
