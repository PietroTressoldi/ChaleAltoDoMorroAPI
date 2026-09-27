using Microsoft.AspNetCore.Identity;

namespace ChaleAltoDoMorroAPI.Data;

// Cria o admin inicial do portal a partir da configuração (AdminSeed:Email / AdminSeed:Password).
// Roda em TODOS os ambientes: sem ele não há como entrar no portal. Idempotente: se o e-mail já existe, não faz nada.
public static class AdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config, ILogger log)
    {
        var email = config["AdminSeed:Email"];
        var password = config["AdminSeed:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            log.LogWarning("AdminSeed não configurado (user-secrets ou variáveis de ambiente): admin inicial não foi criado.");
            return;
        }

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        // UserName = e-mail: o Identity exige um UserName único e o login é feito por e-mail.
        var admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(admin, password);

        // Melhor não subir do que subir sem admin (ex.: senha fraca para as regras do Identity).
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Falha ao criar o admin inicial: " + string.Join("; ", result.Errors.Select(e => e.Description)));

        // Sem o e-mail no log: dado pessoal (regra 7).
        log.LogInformation("Admin inicial criado.");
    }
}
