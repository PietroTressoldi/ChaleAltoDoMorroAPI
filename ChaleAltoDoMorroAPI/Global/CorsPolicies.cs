namespace ChaleAltoDoMorroAPI.Global;

// Nomes das políticas de CORS (Program.cs registra; os controllers do portal usam [EnableCors(CorsPolicies.Portal)]).
// Lembrete: CORS é regra do NAVEGADOR, não segurança do servidor. Quem protege as rotas admin é o JWT.
public static class CorsPolicies
{
    // Site público: só lê e manda pré-reserva (GET/POST, Content-Type). É a política padrão do app.UseCors.
    public const string Site = "Site";

    // Portal de gestão: rotas admin e login, com PUT/PATCH/DELETE e o header Authorization.
    public const string Portal = "Portal";

    // Origem = esquema + domínio (+ porta), exatamente como o navegador manda no header Origin.
    // "https://chalealtodomorro.com.br" casa; com barra no fim, caminho ou "www" a mais, nunca casa e o navegador
    // bloqueia em silêncio (o site só "não carrega"). Por isso, fora do Development, a API nem sobe com origem errada.
    public static void EnsureValidOrigins(string key, string[] origins)
    {
        if (origins.Length == 0)
            throw new InvalidOperationException($"{key} vazio. Configure a variável {key.Replace(":", "__")}__0.");

        foreach (var origin in origins)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || uri.GetLeftPart(UriPartial.Authority) != origin)
                throw new InvalidOperationException(
                    $"{key}: \"{origin}\" não é uma origem válida. Use https://dominio, em minúsculas, sem barra nem caminho no fim.");
        }
    }
}
