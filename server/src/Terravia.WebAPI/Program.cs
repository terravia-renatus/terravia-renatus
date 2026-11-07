using Terravia.Domain.Catalog;
using Terravia.Domain.Oases;
using Terravia.Domain.Villages;
using Terravia.Infrastructure.Oases;
using Terravia.Infrastructure.Players;
using Terravia.Infrastructure.Villages;
using Terravia.WebAPI.Catalog;
using Terravia.WebAPI.Villages;

var builder = WebApplication.CreateBuilder(args);

var defaultConnectionString = builder.Configuration.GetConnectionString("Default") ?? string.Empty;

builder.Services.AddSingleton<IGameCatalog>(_ => FrozenGameCatalog.Default);
builder.Services.AddSingleton<IConstructionCalculator, ConstructionCalculator>();
builder.Services.AddSingleton<IBuildingRuleCatalog, BuildingRuleCatalog>();
builder.Services.AddSingleton<IResourceProductionCalculator, ResourceProductionCalculator>();
builder.Services.AddSingleton<IStarvationCalculator, StarvationCalculator>();
builder.Services.AddSingleton<VillageFieldTopologyCatalog>();
builder.Services.AddSingleton<OasisEcologyCatalog>();
builder.Services.AddSingleton<OasisAnnexationManager>();
builder.Services.AddSingleton<CelebrationManager>();
builder.Services.AddSingleton<VillageExpansionCatalog>();
builder.Services.AddSingleton<VillageExpansionManager>();
builder.Services.AddSingleton<SiegeAndConquestCalculator>();
builder.Services.AddSingleton<ITrainingTimeCalculator, TrainingTimeCalculator>();
builder.Services.AddSingleton<ICombatUpgradeCalculator, CombatUpgradeCalculator>();
builder.Services.AddSingleton<MilitaryTrainingManager>();
builder.Services.AddSingleton<TechnologyResearchManager>();
builder.Services.AddSingleton<HospitalHealingManager>();
builder.Services.AddSingleton<TrapperManager>();
builder.Services.AddSingleton(sp => new DapperVillageRepository(defaultConnectionString, sp.GetRequiredService<IGameCatalog>()));
builder.Services.AddSingleton(_ => new DapperOasisRepository(defaultConnectionString));
builder.Services.AddSingleton(_ => new DapperPlayerRepository(defaultConnectionString));

var app = builder.Build();

app.MapGameCatalogEndpoints();
app.MapVillageEconomyEndpoints();
app.MapVillageMilitaryEndpoints();

app.Run();

public partial class Program;
