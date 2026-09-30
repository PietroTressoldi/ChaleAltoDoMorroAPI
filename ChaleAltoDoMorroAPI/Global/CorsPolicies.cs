namespace ChaleAltoDoMorroAPI.Global;

// Nomes das políticas de CORS (Program.cs registra; os controllers do portal usam [EnableCors(CorsPolicies.Portal)]).
// Lembrete: CORS é regra do NAVEGADOR, não segurança do servidor. Quem protege as rotas admin é o JWT.
public static class CorsPolicies
{
    // Site público: só lê e manda pré-reserva (GET/POST, Content-Type). É a política padrão do app.UseCors.
    public const string Site = "Site";

    // Portal de gestão: rotas admin e login, com PUT/PATCH/DELETE e o header Authorization.
    public const string Portal = "Portal";
}
