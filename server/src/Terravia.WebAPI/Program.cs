using Terravia.Domain.Catalog;
using Terravia.WebAPI.Catalog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGameCatalog>(_ => FrozenGameCatalog.Default);
builder.Services.AddSingleton<IConstructionCalculator, ConstructionCalculator>();
builder.Services.AddSingleton<IBuildingRuleCatalog, BuildingRuleCatalog>();

var app = builder.Build();

app.MapGameCatalogEndpoints();

app.Run();

public partial class Program;
