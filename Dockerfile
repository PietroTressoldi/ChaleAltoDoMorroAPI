# Imagem de produção da API. Build a partir da raiz do repositório:
#   docker build -t chale-api .
# Nenhuma configuração sensível entra aqui: tudo chega por variável de ambiente no "docker run"/compose.

# ---------- Estágio 1: build (SDK completo, ~800 MB, só existe durante o build) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Só o .csproj primeiro: o restore fica em cache e só roda de novo quando os pacotes mudarem.
COPY ChaleAltoDoMorroAPI/ChaleAltoDoMorroAPI.csproj ChaleAltoDoMorroAPI/
RUN dotnet restore ChaleAltoDoMorroAPI/ChaleAltoDoMorroAPI.csproj

COPY ChaleAltoDoMorroAPI/ ChaleAltoDoMorroAPI/
RUN dotnet publish ChaleAltoDoMorroAPI/ChaleAltoDoMorroAPI.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ---------- Estágio 2: runtime (só o ASP.NET Core, sem SDK nem código-fonte) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Pasta dos uploads (storage Local) com dono "app": o volume montado aqui herda essa permissão.
RUN mkdir -p /app/wwwroot/uploads && chown -R $APP_UID /app/wwwroot

# Usuário sem privilégios que já vem na imagem da Microsoft. Se alguém explorar uma falha na API, não vira root.
USER $APP_UID

# A imagem do .NET 8+ escuta em 8080 (porta alta, que usuário não-root pode abrir).
EXPOSE 8080

ENTRYPOINT ["dotnet", "ChaleAltoDoMorroAPI.dll"]
