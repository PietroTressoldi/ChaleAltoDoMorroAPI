# Trilha de tasks — API do Chalé Alto do Morro

Como usar: peça ao Claude **"próxima task"**. Implemente, rode `dotnet build` e `dotnet test`, e peça
**"revisa"**. Quando estiver aprovada, a task ganha `[x]` e você faz o commit no **`develop`**.

Cada task tem: **Objetivo · Por que · O que fazer · Conceitos novos · Critérios de aceite · Como testar ·
Armadilhas**. Os critérios de aceite são o "pronto": se todos batem, a task está feita.

Referências: `docs/CONTRATO_SITE.md` (o que o site espera) e `docs/REGRAS_RESERVA.md` (regras + casos de teste).

## Progresso
- [x] 0 — Arrumar a casa
- [x] 1 — Entidades, DbContext e primeira migration
- [x] 2 — Seed de desenvolvimento
- [ ] 3 — Rotas públicas de leitura (e ligar o site na API)
- [ ] 4 — Projeto de testes + BookingRules (TDD)
- [ ] 5 — POST /api/pre-reservations
- [ ] 6 — Bloqueios = manuais + reservas confirmadas
- [ ] 7 — Login do portal: Identity + JWT
- [ ] 8 — Admin: preços, pacotes, kits e bloqueios
- [ ] 9 — Admin: gestão das pré-reservas
- [ ] 10 — Galeria e vídeos com upload
- [ ] 11 — Robustez: erros, rate limiting, logs
- [ ] 12 — Preparar o deploy

---

## Task 0 — Arrumar a casa

**Objetivo:** deixar o projeto pronto para crescer: sem código de exemplo, com Postgres rodando, pacotes
instalados, segredos fora do git e uma rota de saúde.

**Por que:** tudo o que vem depois depende de banco e configuração. Resolver isso antes evita que você misture
"aprender EF Core" com "por que não conecta?".

**O que fazer**
1. Confirme que está no branch `develop` (`git branch`). Tudo daqui em diante é commitado nele.
2. Apague `WeatherForecast.cs` e `Controllers/WeatherForecastController.cs`.
3. Crie as pastas vazias da estrutura (ver `CLAUDE.md`): `Domain`, `Data`, `Dtos`, `Services`, `Storage`.
4. Crie `docker-compose.yml` na **raiz da solution** com um serviço `postgres` (imagem `postgres:17`), usuário,
   senha e banco (aqui: usuário `chale_admin`, banco `chale_alto_do_morro`), porta `5433:5432` (a 5432 do Windows é de um Postgres instalado localmente) e um **volume nomeado** para os dados.
5. Instale os pacotes (versão **10.x** mais recente de cada):
   - `Npgsql.EntityFrameworkCore.PostgreSQL`
   - `Microsoft.EntityFrameworkCore.Design`
   - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
   - `Microsoft.AspNetCore.Authentication.JwtBearer`
   - `Scalar.AspNetCore`
   - Ferramenta global: `dotnet tool install --global dotnet-ef`
6. Inicialize os **user-secrets** no projeto e grave `ConnectionStrings:Default`.
7. Configure o **Scalar** para abrir em `/scalar` (só em Development), junto do `MapOpenApi()` que já existe.
8. Crie `GET /health` que responde `200` com `{ "status": "ok" }`. Pode ser um `HealthController` simples ou o
   `AddHealthChecks()` nativo; se usar o nativo, inclua o check do banco na task 1.
9. Configure o **JSON global** em `AddControllers().AddJsonOptions(...)`: enums como texto camelCase e
   ignorar nulos na escrita (ver tabela de convenções do contrato).

**Conceitos novos**
- `docker compose` e volumes (os dados sobrevivem a `docker compose down`, mas não a `down -v`).
- **User-secrets**: um `secrets.json` fora da pasta do projeto, lido automaticamente em Development.
- Ordem de configuração no `Program.cs`: `builder.Services...` (injeção de dependência) antes de `Build()`;
  `app.Use.../app.Map...` (pipeline HTTP) depois.

**Critérios de aceite**
- [x] `docker compose up -d` sobe o Postgres, e `docker ps` mostra o container saudável.
- [x] Nenhuma senha ou connection string em `appsettings*.json` nem no git.
- [x] `dotnet run` → `http://localhost:5084/health` devolve `{"status":"ok"}`.
- [x] `http://localhost:5084/scalar` abre a documentação.
- [x] Nenhum vestígio do WeatherForecast. `dotnet build` sem warnings novos.

**Como testar:** navegador ou o arquivo `ChaleAltoDoMorroAPI.http` do Visual Studio (atualize-o para chamar `/health`).

**Armadilhas**
- Versões misturadas (EF 9 com ASP.NET 10) dão erros estranhos: tudo em 10.x.
- A senha do `docker-compose.yml` é só para o banco **local** (tudo bem ir para o git); a de produção nunca.

---

## Task 1 — Entidades, DbContext e primeira migration

**Objetivo:** modelar no banco tudo que o site e o portal usam.

**Por que:** o modelo é a base das rotas e das regras. Errar tipo de data ou de dinheiro aqui vira bug de preço depois.

**O que fazer**
1. Em `Domain/`, crie as entidades (nomes sugeridos; ajuste se preferir):
   - `PricingRule`: `Id`, `Kind` (enum `PricingKind { Weekday, Weekend }`, **único**), `Label`, `PricePerNight` (decimal), `MinNights` (int?).
   - `BlockedDate`: `Id`, `Date` (DateOnly, **único**), `Reason` (string?).
   - `SpecialPackage`: `Id` (Guid), `Name`, `Description`, `CheckIn`, `CheckOut` (DateOnly), `Price` (decimal).
   - `Extra` (kit): `Id` (Guid), `Name`, `Description` (string?), `Price`, `Items` (`List<string>`), `IsActive`, `Order`.
   - `GalleryImage`: `Id`, `Src`, `SrcSet` (string?), `Alt`, `Order`.
   - `TourVideo`: `Id`, `Title`, `Description?`, `Url`, `PosterUrl?`, `Orientation` (enum `Portrait/Landscape`), `Order`.
   - `Reservation` (a "pré-reserva" é só o status `Pending`): `Id`, `CheckIn`, `CheckOut`, `Nights`, `PackageId?` + `PackageName?` (cópia do nome),
     `NightsTotal`, `ExtrasTotal`, `Total`, `EstimatedTotalFromSite`, `IncludesColonialBreakfast`,
     `Status` (enum `Pending/Confirmed/Cancelled`), `CreatedAt`, `ConfirmedAt?`, `CancelledAt?`,
     hóspede 1 (`Name`, `Cpf`, `Email?`, `Phone`, `Notes`) e hóspede 2 (`Name?`, `Cpf?`).
     Dica: hóspede como **owned type** (`OwnsOne`) deixa a entidade organizada.
   - **Um kit por reserva** (decisão do dono): a `Reservation` guarda `KitId?`, `KitName?` e `KitPrice?` — **cópia** do kit
     no momento da reserva. O catálogo de kits é a entidade `Kit` (acima, `Extra`). ⚠️ O site hoje manda `extraIds` (lista):
     ajustar contrato + landing para aceitar só um kit.
   - **Hóspedes em tabela própria `guests`** (decisão do dono, para a lista de hóspedes do portal): `Guest` com `Id`,
     `Name`, `Cpf?` (único; aceita vários NULL), `PhoneNumber?`, `Email?`. A `Reservation` tem `Guest1Id` (obrigatório) e
     `Guest2Id?`, mais `ContactPhone`/`ContactEmail?` (snapshot do formulário). Regra da task 5: CPF já cadastrado →
     só liga a reserva ao hóspede existente, **nunca sobrescreve** o cadastro pela rota pública. Hóspede 2 sem CPF →
     cadastro novo a cada reserva.
2. Em `Data/`, crie `AppDbContext : IdentityDbContext<IdentityUser>` (já pensando na task 7) com os `DbSet`s.
3. Uma classe `IEntityTypeConfiguration<T>` por entidade em `Data/Configurations/`, aplicadas com
   `ApplyConfigurationsFromAssembly`. Nelas: tamanhos máximos de string, `HasPrecision(10, 2)` nos decimais,
   enums `HasConversion<string>()`, índices únicos, obrigatórios.
4. Registre o DbContext no `Program.cs` com `UseNpgsql(builder.Configuration.GetConnectionString("Default"))`.
5. `dotnet ef migrations add Inicial` e `dotnet ef database update`.

**Conceitos novos**
- `DateOnly` vira `date` no Postgres; `List<string>` vira `text[]` (o Npgsql faz isso sozinho).
- **Snapshot**: a pré-reserva guarda nome/preço do kit e do pacote *daquele momento*. Se o portal mudar o preço
  amanhã, a reserva de ontem não pode mudar.
- Por que `CreatedAt` deve ser `DateTimeOffset` (ou `DateTime` UTC): o Npgsql exige UTC em `timestamptz`.

**Critérios de aceite**
- [x] Migration `Inicial` criada e aplicada. As tabelas aparecem no banco (pgAdmin, DBeaver ou `psql`).
- [x] Nenhum `decimal` sem precisão, nenhum enum salvo como número, `Kind` e `Date` com índice único.
- [x] `Items` do kit é `text[]`; datas são `date`.
- [x] Entidades sem atributos de EF espalhados (configuração fica nas classes de `Configurations/`).

**Armadilhas**
- `DateTime` no lugar de `DateOnly` traz fuso para uma data que não tem fuso: bug clássico de "dia anterior".
- Apagar migration à mão depois de aplicada: use `dotnet ef migrations remove` (antes do update) ou crie outra.

---

## Task 2 — Seed de desenvolvimento

**Objetivo:** banco local já nasce com os dados que o site usa hoje (os mesmos dos mocks).

**Por que:** para ligar o site na API (task 3) e ver tudo igual ao que já funciona.

**O que fazer**
1. Crie `Data/DevSeeder` (ou `SeedData`) que roda **só em Development**, na inicialização, e **só insere se a
   tabela estiver vazia** (idempotente).
2. Dados:
   - Preços: `Weekday` "Diária durante a semana" 790; `Weekend` "Fim de semana (sex e sáb)" 894,50, `MinNights` 2.
   - Kits (ativos, nesta ordem):
     - **Kit Toque de Romance** — 179 — Espumante nacional Brut ou Moscatel (1 garrafa); Pétalas de rosa; Velas decorativas; Balões de coração.
     - **Kit Eu Te Amo** — 279 — Espumante nacional Brut ou Moscatel (1 garrafa); Pétalas de rosa; Velas decorativas; Balões com a frase “Te amo”.
     - **Kit Noite Especial** — 390 — Arco de coração com rosas; Espumante nacional Brut ou Moscatel (1 garrafa); Pétalas de rosa; Velas decorativas; Balões de coração; Chocolates; Frase com o pedido “Casa comigo” ou “Namora comigo”.
   - Alguns bloqueios **relativos a hoje** (ex.: hoje+3, hoje+4, hoje+11), para o mock não "vencer".
   - Pacotes: **nenhum** (os reais serão cadastrados no portal). Opcional: um pacote de teste comentado.
3. Chame o seeder no `Program.cs` depois do `Build()`, dentro de um `scope` (`app.Services.CreateScope()`).

**Conceitos novos:** *scoped services* (o DbContext vive por requisição, então fora de uma requisição você
precisa criar um escopo); diferença entre `HasData` (vai para a migration) e seed em código (roda no startup).
Aqui usamos seed em código porque os bloqueios dependem de "hoje".

**Critérios de aceite**
- [x] Banco vazio + `dotnet run` → dados inseridos. Rodar de novo não duplica nada.
- [x] Em Production o seeder não roda.

---

## Task 3 — Rotas públicas de leitura (e ligar o site na API)

**Objetivo:** implementar as 6 rotas `GET` do contrato e ver o **site real** consumindo a API.

**Por que:** é a primeira entrega visível: o site passa a ler dados do banco.

**O que fazer**
1. Em `Dtos/`, um `record` de saída por recurso, **idêntico** ao contrato (`PricingRuleDto`, `BlockedDateDto`,
   `SpecialPackageDto`, `ExtraDto`, `GalleryImageDto`, `TourVideoDto`).
2. Controllers públicos: `GET /api/pricing`, `/api/blocked-dates`, `/api/packages`, `/api/extras`,
   `/api/gallery`, `/api/videos`. Regras de cada um estão no contrato (ex.: só kits ativos, só datas de hoje
   em diante, ordenar por `Order`).
3. Consultas com `AsNoTracking()` e **projeção** (`Select(x => new XDto(...))`) direto na query.
4. "Hoje": registre `TimeProvider.System` e crie um jeito único de obter a data de hoje em
   `America/Sao_Paulo` (ex.: um serviço `IClock`/`BrazilClock` ou extensão sobre `TimeProvider`). Vai ser
   reutilizado nas tasks 4 e 5.
5. **CORS**: política nomeada liberando a origem vinda de configuração (`Cors:AllowedOrigins`), em dev
   `http://localhost:5173`. Só `GET` e `POST` bastam para o site.
6. Teste com o site: na landing, crie `.env` com `VITE_API_URL=http://localhost:5084` e rode `npm run dev`.

**Conceitos novos:** projeção evita trazer colunas desnecessárias e evita expor a entidade; `[ApiController]`
e `ActionResult<T>`; por que CORS só afeta o **navegador** (o Scalar e o `.http` funcionam sem ele).

**Critérios de aceite**
- [ ] O JSON de cada rota bate campo a campo com `docs/CONTRATO_SITE.md` (conferir camelCase, `"weekday"`,
      datas `"2026-10-03"`, ausência de campos nulos).
- [ ] O site em `localhost:5173` mostra o calendário com os preços do banco e os 3 kits no formulário,
      **sem erro de CORS** no console do navegador (F12).
- [ ] Mudar o preço no banco e recarregar o site muda o valor exibido.
- [ ] Galeria e vídeos voltam `[]` por enquanto, então essas seções **somem do site**. É esperado até a task 10.

**Armadilhas:** esquecer `app.UseCors(...)` ou colocar na ordem errada; `DateTime.Today` (fuso do servidor,
que em produção costuma ser UTC → "hoje" vira amanhã às 21h).

---

## Task 4 — Projeto de testes + BookingRules (TDD)

**Objetivo:** implementar as regras de reserva numa classe pura, **escrevendo os testes antes**.

**Por que:** é o coração do negócio (preço e disponibilidade). Com testes, você muda o código sem medo, e a
API fica garantidamente igual ao site.

**O que fazer**
1. Crie o projeto `ChaleAltoDoMorroAPI.Tests` (xUnit), adicione à solution e referencie o projeto da API.
2. Monte o **cenário de referência** de `docs/REGRAS_RESERVA.md` num helper de teste.
3. **Primeiro os testes** (`[Theory]` + `[InlineData]` para as tabelas), todos falhando (vermelho).
4. Depois implemente em `Domain/`:
   - `BookingData` (preços, bloqueios, pacotes) — dados de entrada, sem EF.
   - `ValidateStay(checkIn, checkOut, data, today)` → primeiro erro ou sucesso. Modele o erro como você preferir
     (enum + dados extras, `record`s, ou um `Result`), mas cada erro precisa carregar o que o `REGRAS_RESERVA.md` pede
     (qual noite bloqueada, qual pacote, qual mínimo).
   - `Quote(checkIn, checkOut, data, extras)` → linhas, total das diárias, total dos kits, total, pacote,
     cesta de café. Retorna "sem orçamento" se faltar preço.
   - `CpfValidator.IsValid(string)`.
5. Até ficar tudo verde. Depois refatore com calma (os testes te protegem).

**Conceitos novos:** TDD (vermelho → verde → refatorar); por que a classe recebe `today` como parâmetro em vez
de ler o relógio (teste determinístico); `DateOnly.DayOfWeek`, `AddDays`.

**Critérios de aceite**
- [ ] **Todos** os casos das tabelas de `REGRAS_RESERVA.md` existem como teste e passam (`dotnet test`).
- [ ] `Domain/BookingRules` não referencia EF Core, ASP.NET nem `DateTime.Now`.
- [ ] Nenhum `double`/`float` em dinheiro.

**Armadilhas:** off-by-one nas noites (a noite do check-out **não** conta); esquecer que pacote exato dispensa
o mínimo de fim de semana; comparar pacote só pelo check-in.

---

## Task 5 — `POST /api/pre-reservations`

**Objetivo:** receber a pré-reserva do site, validar tudo, recalcular o preço e gravar como pendente.

**Por que:** é o que faz a pré-reserva do WhatsApp aparecer no portal.

**O que fazer**
1. DTO de entrada idêntico ao corpo do contrato (`CreatePreReservationRequest` com `GuestDto` e `SecondGuestDto?`).
   Validação de formato com **DataAnnotations** (`[Required]`, `[StringLength]`, `[EmailAddress]`, regex de
   dígitos) + um atributo de validação próprio `[Cpf]` que usa o `CpfValidator` da task 4.
   Regra condicional (CPF do hóspede 2 exige nome): `IValidatableObject` ou validação no service.
2. `Services/ReservationService`: carrega do banco os dados de reserva (preços, bloqueios **de hoje em
   diante**, pacotes, kits pedidos), chama `ValidateStay` e `Quote`, confere `packageId` e `extraIds`, e grava
   a `Reservation` + `ReservationExtra`s com status `Pending`.
3. Controller fino: chama o service e traduz o resultado em HTTP:
   - sucesso → `201 Created` com `{ id, total, status }` (use `CreatedAtAction` apontando para o GET do admin da
     task 9, ou só `Created` por enquanto);
   - validação de campos → `400` automático do `[ApiController]`;
   - datas indisponíveis / pacote parcial / kit inexistente → `409 Conflict` ou `422` com **ProblemDetails**
     explicando o motivo (sem dados pessoais).
4. Grave `EstimatedTotalFromSite` para comparação, mas o `Total` é sempre o recalculado. Se forem diferentes,
   registre um **log de aviso** (sem CPF/telefone).

**Conceitos novos:** model binding e validação automática do `[ApiController]`; `ProblemDetails` e
`ValidationProblemDetails`; por que regra de negócio não mora no controller; injeção do `DbContext` no service.

**Critérios de aceite**
- [ ] O `POST` do corpo de exemplo do contrato (com datas futuras e ids reais de kit) → `201` e o registro no banco
      com total recalculado.
- [ ] CPF inválido, telefone com 9 dígitos, e-mail `ana@`, CPF do hóspede 2 sem nome → `400` com o campo apontado.
- [ ] Sexta → sábado (1 diária), noite bloqueada, pacote parcial → `409`/`422` com motivo claro.
- [ ] `estimatedTotal` adulterado (ex.: 1) **não** muda o total gravado.
- [ ] Teste real: fazer uma pré-reserva pelo site e ver a linha nova no banco.
- [ ] Nenhum CPF/telefone/e-mail aparece em log.

---

## Task 6 — Bloqueios = manuais + reservas confirmadas

**Objetivo:** `GET /api/blocked-dates` passa a incluir as noites das pré-reservas **confirmadas**, sem
duplicar dados no banco.

**Por que:** reserva confirmada ocupa as noites. Se fossem gravadas também em `BlockedDate`, cancelar exigiria
lembrar de apagar os bloqueios: fonte de bug. Calcular na hora mantém uma única fonte da verdade.

**O que fazer**
1. Crie um serviço de disponibilidade (ex.: `AvailabilityService.GetUnavailableNightsAsync(from)`) que une:
   bloqueios manuais + cada noite de cada reserva `Confirmed` que termina depois de hoje.
2. Use esse serviço **no `GET` público** e também na **validação da pré-reserva** (task 5) e na confirmação (task 9).
3. No retorno público, reservas saem com `reason: "Reservado"` (nunca o nome do hóspede).
4. Sem datas repetidas (se um bloqueio manual coincide com uma reserva, sai uma vez só) e ordenado por data.

**Critérios de aceite**
- [ ] Confirmar uma pré-reserva direto no banco (`status = 'Confirmed'`) faz as noites aparecerem riscadas no calendário do site.
- [ ] O dia do check-out da reserva **não** aparece bloqueado.
- [ ] Testes unitários para a expansão "reserva → noites" (pode ficar na classe pura).

---

## Task 7 — Login do portal: Identity + JWT

**Objetivo:** proteger as rotas `/api/admin/...` com usuário e senha, emitindo um JWT.

**Por que:** o portal Blazor vai editar preços e ver dados pessoais dos hóspedes. Nada disso pode ficar aberto.

**O que fazer**
1. `AddIdentityCore<IdentityUser>(opções de senha forte e lockout)` + `.AddEntityFrameworkStores<AppDbContext>()`.
   Migration nova para as tabelas do Identity.
2. Configuração `Jwt` (`Issuer`, `Audience`, `Key` com 32+ bytes aleatórios **em user-secrets**, `ExpiresMinutes`).
3. `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)` validando issuer, audience,
   assinatura e expiração. `UseAuthentication()` antes de `UseAuthorization()`.
4. `POST /api/auth/login` com `{ email, password }`: `UserManager.FindByEmailAsync` +
   `CheckPasswordAsync` (ou `SignInManager.CheckPasswordSignInAsync` para ter lockout) → devolve
   `{ accessToken, expiresAt }`. Erro de login sempre com a **mesma mensagem genérica** (não revelar se o e-mail existe).
5. Seed do usuário admin inicial a partir de configuração (`AdminSeed:Email`/`AdminSeed:Password` em user-secrets).
6. Um `GET /api/admin/me` com `[Authorize]` que devolve o e-mail do usuário logado (para testar).
7. Scalar: configurar o esquema Bearer no documento OpenAPI para aparecer o botão de autenticar.

**Conceitos novos:** diferença entre autenticação (quem é) e autorização (pode?); claims; por que o JWT é
assinado e não criptografado (não coloque dado sensível nele); `[Authorize]` em controller inteiro.

**Critérios de aceite**
- [ ] `GET /api/admin/me` sem token → `401`; com token do login → `200`.
- [ ] Senha errada 5 vezes → conta bloqueada temporariamente.
- [ ] Nenhuma chave ou senha no `appsettings.json` nem no git.
- [ ] Rotas públicas do site continuam funcionando sem token.

**Armadilhas:** chave curta demais (erro em runtime); relógio/expiração; esquecer `UseAuthentication`.

---

## Task 8 — Admin: preços, pacotes, kits e bloqueios

**Objetivo:** CRUD autenticado para o portal gerenciar tudo que o site lê.

**O que fazer** (todas em `/api/admin/...`, `[Authorize]`, com DTOs de entrada próprios e validação)
1. **Preços** — `GET` e `PUT /api/admin/pricing/{kind}`. `PricePerNight > 0`; `MinNights` entre 1 e 7 (só weekend).
   Ofereça também gravar o fim de semana pelo **valor total** ("1.789 pelas 2 diárias" → 894,50): decida se isso
   fica na API ou no portal e justifique na revisão.
2. **Pacotes** — CRUD. Validar: `CheckOut > CheckIn`, `Price > 0`, **sem sobreposição** com outro pacote,
   sem noite bloqueada/confirmada dentro. Avisar (não impedir?) se já existem pré-reservas pendentes nas datas.
3. **Kits** — CRUD + ativar/desativar + ordem. Apagar kit que já está em pré-reserva: **desativar** em vez de apagar.
4. **Bloqueios manuais** — listar (com filtro de período), criar por **intervalo** (de/até, cria uma linha por
   noite, ignorando as que já existem), remover.

**Conceitos novos:** `PUT` vs `PATCH` vs `POST`; códigos `204 No Content`, `404`, `409`; validação que depende
do banco (sobreposição) fica no service, não em atributo.

**Critérios de aceite**
- [ ] Tudo testável pelo Scalar com o token.
- [ ] Criar pacote que se sobrepõe a outro → `409` com mensagem clara.
- [ ] Mudanças aparecem no site ao recarregar (preço, pacote novo no calendário, kit desativado some).
- [ ] Testes unitários para a regra de sobreposição de pacotes.

---

## Task 9 — Admin: gestão das pré-reservas

**Objetivo:** o portal lista, abre, confirma e cancela pré-reservas.

**O que fazer**
1. `GET /api/admin/pre-reservations?status=pending&from=...&to=...&page=1&pageSize=20` → lista paginada
   (mais recentes primeiro) com dados resumidos.
2. `GET /api/admin/pre-reservations/{id}` → detalhe completo (hóspedes, kits, valores, se o valor do site divergiu).
3. `POST /api/admin/pre-reservations/{id}/confirm`:
   - só de `Pending`;
   - revalida disponibilidade usando o `AvailabilityService` **ignorando a própria reserva** (outra pode ter sido
     confirmada nas mesmas datas);
   - em conflito → `409`.
4. `POST /api/admin/pre-reservations/{id}/cancel` → de `Pending` ou `Confirmed` para `Cancelled` (libera as noites).
5. **Concorrência**: dois cliques em "confirmar" em reservas diferentes com as mesmas noites não podem ambos
   passar. Use uma transação com nível de isolamento adequado e revalidação dentro dela. (Bônus avançado:
   constraint de exclusão do Postgres com `daterange`.)

**Conceitos novos:** paginação; máquina de estados simples (quais transições são permitidas); transações no
EF Core (`Database.BeginTransactionAsync`); condição de corrida.

**Critérios de aceite**
- [ ] Confirmar → as noites aparecem bloqueadas no site. Cancelar → somem.
- [ ] Confirmar duas reservas que se sobrepõem → a segunda recebe `409`.
- [ ] Confirmar uma reserva já cancelada → `409` (transição inválida).
- [ ] Testes unitários das transições de status.

---

## Task 10 — Galeria e vídeos com upload

**Objetivo:** o portal envia fotos e cadastra vídeos; o site volta a mostrar galeria e tour.

**O que fazer**
1. `Storage/IFileStorage` com `SaveAsync(stream, fileName, contentType, pasta)` → caminho/URL, e `DeleteAsync`.
2. `LocalFileStorage`: salva em `wwwroot/uploads/...` com **nome gerado** (Guid), nunca o nome enviado.
   `app.UseStaticFiles()` para servir. `wwwroot/uploads/` no `.gitignore`.
3. Montar **URL absoluta** a partir de configuração (`PublicBaseUrl`), porque o site exige.
4. Admin galeria: `POST` upload (`IFormFile` + `alt`), `PUT` alt, `PUT /reorder` (lista de ids na nova ordem), `DELETE` (apaga o arquivo também).
5. Admin vídeos: cadastro por **URL do YouTube** ou upload de `.mp4` + capa; `orientation`; reordenar; remover.
6. Validação: só `image/jpeg`, `image/png`, `image/webp` (confira também a **assinatura do arquivo**, não só a
   extensão); limite de tamanho (ex.: 10 MB foto, 100 MB vídeo) com `RequestSizeLimit`.
7. Lembrete no contrato: **nenhuma foto com hóspedes** (o portal mostra o aviso; a API só armazena).
8. Opcional: gerar versões 640/1280/1920 em WebP para o `srcSet` (biblioteca `SixLabors.ImageSharp`).

**Critérios de aceite**
- [ ] Upload pelo Scalar → a foto aparece na galeria do site, na ordem certa.
- [ ] Arquivo `.exe` renomeado para `.jpg` é recusado.
- [ ] Remover a foto apaga o registro **e** o arquivo.
- [ ] Trocar `LocalFileStorage` por outra implementação exigiria mudar **só** o registro no `Program.cs`.

---

## Task 11 — Robustez: erros, rate limiting, logs

**O que fazer**
1. `AddProblemDetails()` + um `IExceptionHandler` global: exceção inesperada → `500` em ProblemDetails, sem
   stack trace em produção, com log de erro.
2. **Rate limiting** (`AddRateLimiter`, nativo): login (ex.: 5/min por IP) e `POST /api/pre-reservations`
   (ex.: 10/hora por IP) → `429`.
3. Logs estruturados (`ILogger` com templates `{Campo}`), sem dados pessoais. Revisar todos os logs criados até aqui.
4. Health check incluindo o banco (`AddHealthChecks().AddDbContextCheck<AppDbContext>()`).

**Critérios de aceite**
- [ ] 6 logins seguidos em 1 minuto → o 6º recebe `429`.
- [ ] Uma exceção forçada devolve ProblemDetails sem detalhes internos (em Production).
- [ ] `grep` no código não encontra log com CPF, telefone ou e-mail.

---

## Task 12 — Preparar o deploy (quando for publicar)

**O que fazer**
1. `Dockerfile` multi-stage (sdk → aspnet), rodando como usuário não-root.
2. Configuração de produção só por **variáveis de ambiente** (`ConnectionStrings__Default`, `Jwt__Key`,
   `Cors__AllowedOrigins__0`, `PublicBaseUrl`...).
3. Migrations em produção: script (`dotnet ef migrations script --idempotent`) ou bundle (`dotnet ef migrations bundle`),
   **não** `Database.Migrate()` automático sem pensar.
4. CORS com o domínio real do site; HTTPS; `UseForwardedHeaders` se ficar atrás de proxy.
5. Uploads: disco persistente (volume) ou trocar o `IFileStorage` por nuvem.
6. Checklist de publicação: testes verdes no `develop` → merge `develop` → `master` → deploy.
   Na landing: `VITE_API_URL` apontando para a API publicada.

**Critérios de aceite**
- [ ] `docker build` + `docker run` com variáveis de ambiente sobe a API conectada a um Postgres.
- [ ] Nenhum segredo na imagem.
