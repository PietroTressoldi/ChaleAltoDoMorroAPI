using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Global;
using ChaleAltoDoMorroAPI.Interfaces;
using ChaleAltoDoMorroAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
        .UseSnakeCaseNamingConvention());

builder.Services.AddIdentityCore<IdentityUser>(options =>
{
    options.Password.RequiredLength = 10;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(3);
    options.Lockout.AllowedForNewUsers = true;
    options.User.RequireUniqueEmail = true;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Seção 'Jwt' não configurada.");

if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key ausente ou curta (mínimo 32 bytes). Configure no user-secrets.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Sem isso o handler renomeia "email"/"sub" para as URLs longas de ClaimTypes.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,

            ValidateAudience = true,
            ValidAudience = jwt.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });


builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddAuthorization();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // Declara o esquema Bearer: o Scalar mostra onde colar o token.
    options.AddDocumentTransformer((document, context, ct) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT"
        };
        return Task.CompletedTask;
    });

    // Só as rotas com [Authorize] pedem o token na documentação; as públicas do site continuam abertas.
    options.AddOperationTransformer((operation, context, ct) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
            });
        }
        return Task.CompletedTask;
    });
});

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IClock, BrazilClock>();
builder.Services.AddSingleton<SrvToken>();

// CORS: quais origens podem chamar a API a partir do NAVEGADOR. As origens vêm da configuração
// (dev: appsettings.Development.json; produção: Cors__AllowedOrigins__0 e Cors__PortalOrigins__0).
// Duas políticas (e não uma com as duas listas): o site só ganha o que ele usa (GET/POST), e o portal
// ganha PUT/PATCH/DELETE + Authorization só nas rotas dele ([EnableCors(CorsPolicies.Portal)]).
var siteOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var portalOrigins = builder.Configuration.GetSection("Cors:PortalOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicies.Site, policy =>
        policy.WithOrigins(siteOrigins)
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type"));

    // Sem AllowCredentials: o token vai no header Authorization, não em cookie.
    // SetPreflightMaxAge: o navegador guarda a resposta do OPTIONS por 10 min (menos idas e vindas no celular).
    options.AddPolicy(CorsPolicies.Portal, policy =>
        policy.WithOrigins(portalOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Content-Type", "Authorization")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10)));
});

builder.Services.AddScoped<SrvReservation>();
builder.Services.AddScoped<SrvAvailability>();
builder.Services.AddScoped<SrvPricing>();
builder.Services.AddScoped<SrvPackage>();
builder.Services.AddScoped<SrvKit>();
builder.Services.AddScoped<SrvBlockedDate>();
builder.Services.AddScoped<SrvAdminReservation>();
builder.Services.AddScoped<SrvCashCategory>();
builder.Services.AddScoped<SrvRecurringExpense>();
builder.Services.AddScoped<SrvCashBook>();
builder.Services.AddScoped<SrvCashTransaction>();
builder.Services.AddScoped<SrvSettings>();
builder.Services.AddScoped<SrvGuest>();
builder.Services.AddScoped<SrvCalendar>();

var app = builder.Build();

// Admin inicial: em todos os ambientes (sem ele não há como entrar no portal).
using (var scope = app.Services.CreateScope())
{
    await AdminSeeder.SeedAsync(scope.ServiceProvider, app.Configuration, app.Logger);

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await CashCategorySeeder.SeedAsync(db, app.Logger);
}

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
// "Site" é a política padrão; um [EnableCors("Portal")] no controller troca a política só naquela rota.
app.UseCors(CorsPolicies.Site);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
