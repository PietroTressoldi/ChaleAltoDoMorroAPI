# Variáveis de ambiente e segredos — checklist de publicação

Tudo que precisa ser configurado para publicar os 3 projetos (API, landing e portal).
Troque `<dominio>` pelo domínio real (ex.: `chalealtodomorro.com.br`).

**Legenda:** 🔒 = segredo (só no `.env` do servidor e no gerenciador de senhas; **nunca** no git, print ou chat) ·
🌐 = público (vai para o navegador ou não é sensível).

> **Onde guardar os segredos:** num gerenciador de senhas (Bitwarden, 1Password...), num item "Chalé — produção".
> No servidor, ficam num arquivo `.env` ao lado do `docker-compose.prod.yml`, com `chmod 600` (só o dono lê) e
> **fora do git** (o `.gitignore` já ignora `.env`). Se o VPS morrer, o gerenciador é a única cópia.

---

## 1. API (.NET) — VPS da Hostinger, `.env` do docker compose

`__` (dois sublinhados) = o `:` da configuração. `Cors__AllowedOrigins__0` = `Cors:AllowedOrigins[0]`.
Em desenvolvimento os mesmos valores ficam em **user-secrets** (os 🔒) e em `appsettings.Development.json` (os 🌐).

### Obrigatórias (sem elas a API não sobe ou não tem como entrar no portal)
| ☐ | Variável | Tipo | Exemplo / como gerar | Observação |
|---|---|---|---|---|
| ☐ | `ConnectionStrings__Default` | 🔒 | `Host=postgres;Port=5432;Database=chale_alto_do_morro;Username=chale_admin;Password=<senha do banco>` | `Host=postgres` = nome do serviço no compose (não `localhost`). A senha tem que ser **a mesma** do `POSTGRES_PASSWORD` (seção 2). |
| ☐ | `Jwt__Key` | 🔒 | `openssl rand -base64 48` | Mínimo 32 bytes (a API não sobe com menos). **Diferente** da de dev. Trocar = todo mundo é deslogado. |
| ☐ | `AdminSeed__Email` | 🔒 | e-mail do dono | Dado pessoal. Cria o admin **só na primeira subida**. |
| ☐ | `AdminSeed__Password` | 🔒 | 10+ caracteres | Depois de entrar no portal e trocar a senha, **pode apagar** as duas `AdminSeed__*` do `.env`: o admin já existe e o seeder não faz nada sem elas (só um aviso no log). |
| ☐ | `Cors__AllowedOrigins__0` | 🌐 | `https://<dominio>` | Origem da **landing**. `https`, minúsculas, **sem barra no fim** (senão a API não sobe — task 12). |
| ☐ | `Cors__AllowedOrigins__1` | 🌐 | `https://www.<dominio>` | Só se o site também abrir com `www`. Pode adicionar `__2` com o `*.workers.dev` enquanto o domínio não estiver pronto. |
| ☐ | `Cors__PortalOrigins__0` | 🌐 | `https://portal.<dominio>` | Origem do **portal**. Mesmas regras. |
| ☐ | `Storage__PublicBaseUrl` | 🌐 | `https://api.<dominio>/uploads/` | De onde o navegador baixa fotos/vídeos. Com barra no fim. |

### Já definidas (não precisa mexer)
| Variável | Valor | Onde está |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Dockerfile` (desliga Scalar, OpenAPI e DevSeeder) |
| `Storage__Provider` | `Local` (padrão) | `StorageOptions` — uploads no volume do VPS |
| `Jwt__Issuer` / `Jwt__Audience` / `Jwt__ExpiresMinutes` | `ChaleAltoDoMorroApi` / `ChaleAltoDoMorroPortal` / `60` | `appsettings.json` |

### Só se um dia trocar o disco por S3 ou Cloudflare R2
| Variável | Tipo | Observação |
|---|---|---|
| `Storage__Provider=S3` | 🌐 | |
| `Storage__BucketName` | 🌐 | |
| `Storage__PublicBaseUrl` | 🌐 | Passa a ser o domínio do CDN do bucket. |
| `AWS__Region` | 🌐 | ex.: `sa-east-1` |
| `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` | 🔒 | Fora da AWS não há IAM Role: usuário IAM com permissão **só** nesse bucket. O R2 ainda pede o endpoint (`ServiceURL`), que exige uma pequena mudança no código. |

### A definir na Parte B da task 12
- [ ] Variável de proxy confiável para o `UseForwardedHeaders` / `CF-Connecting-IP` (o nome sai na implementação).

---

## 2. Postgres — mesmo `.env` do docker compose

| ☐ | Variável | Tipo | Exemplo / como gerar | Observação |
|---|---|---|---|---|
| ☐ | `POSTGRES_DB` | 🌐 | `chale_alto_do_morro` | |
| ☐ | `POSTGRES_USER` | 🌐 | `chale_admin` | |
| ☐ | `POSTGRES_PASSWORD` | 🔒 | `openssl rand -hex 32` | **Hex** de propósito: sem `;` `=` `+` `/`, que quebram a connection string. **Nunca** `chale_dev`. |

⚠️ O Postgres só lê essas três na **primeira** vez que cria o volume. Trocar depois no `.env` não muda a senha do banco:
precisa de `ALTER USER chale_admin PASSWORD '...'` dentro do container.

---

## 3. Cloudflare Tunnel — mesmo `.env` do docker compose

| ☐ | Variável | Tipo | Onde pegar | Observação |
|---|---|---|---|---|
| ☐ | `TUNNEL_TOKEN` | 🔒 | Cloudflare → Zero Trust → Networks → Tunnels → criar túnel → "Docker" | Quem tem o token pode publicar coisas no seu domínio. Vazou? Gere outro no painel. |

No painel do túnel (não é variável, mas entra na checklist):
- [ ] `api.<dominio>` → `http://api:8080`
- [ ] Se o portal ficar no VPS: `portal.<dominio>` → `http://caddy:80`

---

## 4. Landing (React) — Cloudflare Workers (`wrangler.jsonc` já existe)

⚠️ Variáveis `VITE_*` entram **no build** e ficam **visíveis** para qualquer um que abrir o site. Nunca coloque
segredo aqui. Trocar o valor exige **um novo build/deploy** (não basta reiniciar).

Onde configurar: Cloudflare → Workers & Pages → `chalealtodomorro` → Settings → **Build** → Variables and secrets.

| ☐ | Variável | Tipo | Valor | Observação |
|---|---|---|---|---|
| ☐ | `VITE_API_URL` | 🌐 | `https://api.<dominio>` | **Sem barra no fim.** Vazia = o site usa dados mockados (preço falso!). |
| ☐ | `VITE_META_PIXEL_ID` | 🌐 | ID do Pixel da Meta | Opcional. Sem ela, o pixel fica desligado. |

Fora das variáveis:
- [ ] Domínio personalizado `<dominio>` (e `www`, se usar) ligado ao Worker.
- [ ] A origem final (`https://<dominio>`) igual ao `Cors__AllowedOrigins__0` da API.

---

## 5. Portal de gestão (React) — host a definir (Cloudflare Workers como a landing, ou VPS)

Mesmo aviso: `VITE_*` = build e público.

| ☐ | Variável | Tipo | Valor | Observação |
|---|---|---|---|---|
| ☐ | `VITE_API_URL` | 🌐 | `https://api.<dominio>` | Sem barra no fim. |
| ☐ | `VITE_USE_MOCKS` | 🌐 | `false` | Sem ela, o build de produção já desliga os mocks. Deixar `false` explícito evita surpresa. **Nunca `true` em produção** (o portal mostraria dados falsos). |

Fora das variáveis:
- [ ] Domínio `portal.<dominio>` ligado ao host.
- [ ] Recarregar uma rota interna (ex.: `/reservas`) não dá 404 (no Workers: `"not_found_handling": "single-page-application"`).
- [ ] A origem final igual ao `Cors__PortalOrigins__0` da API.

---

## 6. Conferência final (depois de subir tudo)
- [ ] Nenhum segredo no git: `git grep -i "password\|jwt__key\|tunnel_token"` nos 3 repositórios só acha documentação.
- [ ] `https://api.<dominio>/health` → `{"status":"ok"}`.
- [ ] Landing mostra os preços **do banco** (mude um preço no portal e recarregue).
- [ ] Login no portal funciona; F12 → Console sem erro de CORS.
- [ ] Upload de uma foto no portal → aparece no site → apagar.
- [ ] `AdminSeed__*` removidas do `.env` depois de trocar a senha do admin.
- [ ] Todos os 🔒 salvos no gerenciador de senhas.
