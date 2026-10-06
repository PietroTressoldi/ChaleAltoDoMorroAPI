using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Amazon.S3;
using ChaleAltoDoMorroAPI.Data;
using ChaleAltoDoMorroAPI.Global;
using ChaleAltoDoMorroAPI.Interfaces;
using ChaleAltoDoMorroAPI.Services;
using ChaleAltoDoMorroAPI.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
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

// Erros: todo 4xx/5xx sai em ProblemDetails (com traceId), e exceção não tratada passa pelo GlobalExceptionHandler.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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
    .AddSignInManager()
    .AddErrorDescriber<PtBrIdentityErrorDescriber>();

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

// Arquivos: disco local (dev) ou S3 (produção), escolhido por Storage:Provider. Nenhum serviço sabe qual é:
// todos só conhecem o IFileStorage e a chave do arquivo.
var storageOptions = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();

builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection("Storage"))
    .Validate(o => Uri.TryCreate(o.PublicBaseUrl, UriKind.Absolute, out _), "Storage:PublicBaseUrl precisa ser uma URL absoluta.")
    .Validate(o => o.Provider != StorageProvider.S3 || !string.IsNullOrWhiteSpace(o.BucketName), "Storage:BucketName é obrigatório com o S3.")
    .ValidateOnStart();

if (storageOptions.Provider == StorageProvider.S3)
{
    // Região: seção "AWS" ou variável AWS_REGION. Credenciais: IAM Role da EC2 (nenhuma chave na configuração).
    builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
    builder.Services.AddAWSService<IAmazonS3>();
    builder.Services.AddSingleton<IFileStorage, S3FileStorage>();
}
else
{
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
}


// CORS: quais origens podem chamar a API a partir do NAVEGADOR. As origens vêm da configuração
// (dev: appsettings.Development.json; produção: Cors__AllowedOrigins__0 e Cors__PortalOrigins__0).
// Duas políticas (e não uma com as duas listas): o site só ganha o que ele usa (GET/POST), e o portal
// ganha PUT/PATCH/DELETE + Authorization só nas rotas dele ([EnableCors(CorsPolicies.Portal)]).
var siteOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var portalOrigins = builder.Configuration.GetSection("Cors:PortalOrigins").Get<string[]>() ?? [];

// Em produção, origem vazia ou malformada derruba a subida (erro no deploy, e não "o site não carrega" sem motivo).
if (!builder.Environment.IsDevelopment())
{
    CorsPolicies.EnsureValidOrigins("Cors:AllowedOrigins", siteOrigins);
    CorsPolicies.EnsureValidOrigins("Cors:PortalOrigins", portalOrigins);
}

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
builder.Services.AddScoped<SrvDashboard>();
builder.Services.AddScoped<SrvGallery>();
builder.Services.AddScoped<SrvVideo>();

builder.Services.AddRateLimiter(options =>
{
    // O padrão é 503 ("o servidor caiu"). 429 diz "você está indo rápido demais".
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Teto geral por IP para qualquer rota da API (os arquivos de /uploads ficam de fora: são servidos antes).
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
        RateLimitPolicies.FixedWindowPerClient(http, permitLimit: 100, TimeSpan.FromMinutes(1)));

    // Força bruta: senha do login e senha atual na troca de senha.
    options.AddPolicy(RateLimitPolicies.Login, http =>
        RateLimitPolicies.FixedWindowPerClient(http, permitLimit: 5, TimeSpan.FromMinutes(1)));
    options.AddPolicy(RateLimitPolicies.Password, http =>
        RateLimitPolicies.FixedWindowPerClient(http, permitLimit: 5, TimeSpan.FromMinutes(1)));

    // Spam de pré-reservas falsas no site.
    options.AddPolicy(RateLimitPolicies.PreReservation, http =>
        RateLimitPolicies.FixedWindowPerClient(http, permitLimit: 10, TimeSpan.FromHours(1)));

    options.OnRejected = async (context, ct) =>
    {
        var http = context.HttpContext;

        // Retry-After: quantos segundos faltam para a janela abrir de novo (o portal pode mostrar "tente em 40 s").
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

        // Sem o IP no log: IP também é dado pessoal (LGPD).
        http.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiting")
            .LogWarning("Limite de requisições atingido em {Method} {Path}.", http.Request.Method, http.Request.Path);

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Muitas tentativas",
                Detail = "Espere um pouco e tente de novo."
            }
        });
    };
});

// /health de verdade: além de "a API respondeu", testa se consegue conectar no Postgres.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

// Cria o storage já na subida. No S3, sem credenciais (IAM Role) a API nem sobe, em vez de quebrar a galeria do site
// na primeira requisição.
_ = app.Services.GetRequiredService<IFileStorage>();

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

// Primeiro middleware de verdade: precisa "envolver" todos os outros para pegar exceção de qualquer um.
app.UseExceptionHandler();
// 404 de rota inexistente, 405, 401 sem corpo... ganham corpo em ProblemDetails também.
app.UseStatusCodePages();

app.UseHttpsRedirection();

// Disco local: a própria API serve wwwroot/uploads em /uploads. No S3 quem serve é o CloudFront.
if (storageOptions.Provider == StorageProvider.Local)
{
    var uploadsRoot = LocalFileStorage.RootPath(app.Environment);
    Directory.CreateDirectory(uploadsRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsRoot),
        RequestPath = "/uploads"
    });
}

// Antes do UseAuthorization e do MapControllers: o preflight (OPTIONS) precisa ser respondido aqui.
// "Site" é a política padrão; um [EnableCors("Portal")] no controller troca a política só naquela rota.
app.UseCors(CorsPolicies.Site);

// Depois do CORS: o preflight (OPTIONS) é respondido pelo CORS e não gasta a cota, e o 429 já sai com os headers de CORS.
// Antes da autenticação: quem passou do limite nem chega a validar o token.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Mantém o contrato da Task 0: {"status":"ok"}. Banco fora → 503 {"status":"unhealthy"} (o status HTTP vem do padrão).
// Sem o motivo da falha na resposta: "connection refused em localhost:5433" revelaria a infraestrutura.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (context, report) => context.Response.WriteAsJsonAsync(new
    {
        status = report.Status == HealthStatus.Healthy ? "ok" : "unhealthy"
    })
});

app.Run();
