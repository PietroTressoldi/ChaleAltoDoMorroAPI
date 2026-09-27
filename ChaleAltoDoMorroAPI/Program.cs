using System.Text.Json;
using System.Text.Json.Serialization;
using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Global;
using ChaleAltoDoMorroAPI.Interfaces;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

const string SiteCorsPolicy = "Site";

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
        .UseSnakeCaseNamingConvention());

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IClock, BrazilClock>();

// CORS: só o site pode chamar a API a partir do navegador. As origens vêm da configuração
// (dev: appsettings.Development.json; produção: variável de ambiente Cors__AllowedOrigins__0).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(SiteCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var clock = scope.ServiceProvider.GetRequiredService<IClock>();

    DevSeeder seeder = new(db, app.Logger, clock);
    await seeder.SeedAsync();
}

app.UseHttpsRedirection();

// Antes do UseAuthorization e do MapControllers: o preflight (OPTIONS) precisa ser respondido aqui.
app.UseCors(SiteCorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
