# Trilha de tasks — API do Chalé Alto do Morro

Como usar: peça ao Claude **"próxima task"**. Implemente, rode `dotnet build` e `dotnet test`, e peça
**"revisa"**. Quando estiver aprovada, a task ganha `[x]` e você faz o commit no **`develop`**.

Cada task tem: **Objetivo · Por que · O que fazer · Conceitos novos · Critérios de aceite · Como testar ·
Armadilhas**. Os critérios de aceite são o "pronto": se todos batem, a task está feita.

Referências:
- `docs/CONTRATO_SITE.md`: o que o site espera.
- `docs/CONTRATO_PORTAL.md`: o que o portal de gestão espera.
- `docs/REGRAS_RESERVA.md`: regras de reserva e folga, com casos de teste.
- `docs/REGRAS_CAIXA.md`: regras do Livro Caixa, com casos de teste.

## Ordem sugerida
O número da task é só um nome. A **ordem** abaixo segue o que o portal precisa primeiro:
**13 → 9 → 21 → 14 → 15 → 16 → 17 → 18 → 19 → 20 → 10 → 11 → 12.**
A 13 é pequena e destrava o portal no navegador. O Livro Caixa (14 e 15) vem cedo porque é a tela nova mais usada no dia a dia.

## Progresso
- [x] 0 — Arrumar a casa
- [x] 1 — Entidades, DbContext e primeira migration
- [x] 2 — Seed de desenvolvimento
- [x] 3 — Rotas públicas de leitura (e ligar o site na API)
- [x] 4 — Projeto de testes + BookingRules (TDD)
- [x] 5 — POST /api/pre-reservations
- [x] 6 — Bloqueios = manuais + reservas confirmadas
- [x] 7 — Login do portal: Identity + JWT
- [x] 8 — Admin: preços, pacotes, kits e bloqueios
- [x] 9 — Admin: reservas (pré-reservas e confirmadas)
- [x] 10 — Galeria e vídeos com upload
- [ ] 11 — Robustez: erros, rate limiting, logs
- [ ] 12 — Preparar o deploy
- [x] 13 — CORS do portal
- [x] 14 — Livro Caixa: domínio, regras puras e migration
- [x] 15 — Livro Caixa: rotas e despesas fixas
- [x] 16 — Folga entre hóspedes
- [x] 17 — Hóspedes
- [x] 18 — Calendário do portal
- [x] 19 — Dashboard
- [x] 20 — Trocar senha
- [x] 21 — Admin: criar e editar reservas

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
- [x] O JSON de cada rota bate campo a campo com `docs/CONTRATO_SITE.md` (conferir camelCase, `"weekday"`,
      datas `"2026-10-03"`, ausência de campos nulos).
- [x] O site em `localhost:5173` mostra o calendário com os preços do banco e os 3 kits no formulário,
      **sem erro de CORS** no console do navegador (F12).
- [x] Mudar o preço no banco e recarregar o site muda o valor exibido.
- [x] Galeria e vídeos voltam `[]` por enquanto, então essas seções **somem do site**. É esperado até a task 10.

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
- [x] **Todos** os casos das tabelas de `REGRAS_RESERVA.md` existem como teste e passam (`dotnet test`).
- [x] `Domain/BookingRules` não referencia EF Core, ASP.NET nem `DateTime.Now`.
- [x] Nenhum `double`/`float` em dinheiro.

**Nota:** a tabela "Hóspedes" do `REGRAS_RESERVA.md` é validação de entrada (DataAnnotations) e é testada na task 5.

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
- [x] O `POST` do corpo de exemplo do contrato (com datas futuras e ids reais de kit) → `201` e o registro no banco
      com total recalculado.
- [x] CPF inválido, telefone com 9 dígitos, e-mail `ana@`, CPF do hóspede 2 sem nome → `400` com o campo apontado.
- [x] Sexta → sábado (1 diária), noite bloqueada, pacote parcial → `409`/`422` com motivo claro.
- [x] `estimatedTotal` adulterado (ex.: 1) **não** muda o total gravado.
- [x] Teste real: fazer uma pré-reserva pelo site e ver a linha nova no banco.
- [x] Nenhum CPF/telefone/e-mail aparece em log.

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
3. No retorno público **não sai `reason`** (nem de bloqueio manual, nem de reserva): a rota é pública e o contrato marca o campo como opcional. Nunca expor nome de hóspede.
4. Sem datas repetidas (se um bloqueio manual coincide com uma reserva, sai uma vez só) e ordenado por data.

**Critérios de aceite**
- [x] Confirmar uma pré-reserva direto no banco (`status = 'Confirmed'`) faz as noites aparecerem riscadas no calendário do site.
- [x] O dia do check-out da reserva **não** aparece bloqueado.
- [x] Testes unitários para a expansão "reserva → noites" (pode ficar na classe pura).

---

## Task 7 — Login do portal: Identity + JWT

**Objetivo:** proteger as rotas `/api/admin/...` com usuário e senha, emitindo um JWT.

**Por que:** o portal de gestão vai editar preços e ver dados pessoais dos hóspedes. Nada disso pode ficar aberto.

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
- [x] `GET /api/admin/me` sem token → `401`; com token do login → `200`.
- [x] Senha errada 5 vezes → conta bloqueada temporariamente.
- [x] Nenhuma chave ou senha no `appsettings.json` nem no git.
- [x] Rotas públicas do site continuam funcionando sem token.

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
- [x] Tudo testável pelo Scalar com o token.
- [x] Criar pacote que se sobrepõe a outro → `409` com mensagem clara.
- [x] Mudanças aparecem no site ao recarregar (preço, pacote novo no calendário, kit desativado some).
- [x] Testes unitários para a regra de sobreposição de pacotes.

---

## Task 9 — Admin: reservas (pré-reservas e confirmadas)

**Objetivo:** o portal lista, filtra, ordena, abre, confirma e cancela reservas. É uma rota só para as duas telas:
**"Pré-reservas"** (`status=pending`) e **"Reservas"** (`status=confirmed`).

**Por que:** a pré-reserva é só uma reserva com status `Pending`. Uma rota com filtro evita duplicar código e deixa a
distinção entre as duas telas onde ela faz sentido: no portal.

**O que fazer** (formatos exatos em `docs/CONTRATO_PORTAL.md`, seção "Reservas")
1. `GET /api/admin/reservations` com os filtros `status` (pode repetir), `when` (`today`/`ongoing`/`upcoming`/`past`),
   `from`/`to` (a estadia toca o intervalo), `search` (nome do hóspede 1 ou 2, telefone), `sort`
   (`createdAt`/`checkIn`/`total`/`guestName`), `order` e paginação `page`/`pageSize`, com resposta
   `{ items, page, pageSize, totalCount }`.
   - Monte a query **incrementalmente**: `IQueryable` + `if (filtro) query = query.Where(...)`.
   - Ordenação por um `switch` numa **lista fechada** de campos. Nunca monte a ordenação com texto vindo do usuário.
   - Campo de `sort` desconhecido → `400`.
2. `GET /api/admin/reservations/{id}` → detalhe completo: hóspedes com CPF, valores e se o total do site divergiu.
   Inclui `nightsBreakdown`, recalculado pelo `BookingRules` só como referência.
3. `POST /api/admin/reservations/{id}/confirm`, com corpo opcional `{ bufferBeforeNights?, bufferAfterNights? }`:
   - só a partir de `Pending`;
   - revalida a disponibilidade pelo `SrvAvailability` **ignorando a própria reserva**, porque outra reserva pode ter
     sido confirmada nas mesmas datas;
   - em conflito → `409`, com a noite ou a reserva que está no caminho;
   - grava `ConfirmedAt`. Se a task 16 já existir, grava também a folga efetiva. Se ainda não existir, ignore o corpo.
4. `POST /api/admin/reservations/{id}/cancel` → de `Pending` ou `Confirmed` para `Cancelled`, o que libera as noites.
5. `PATCH /api/admin/reservations/{id}` com `{ adminNotes }`, um campo novo `AdminNotes` (string?, até 2000).
   Precisa de migration.
6. **Concorrência**: dois cliques em "confirmar", em reservas diferentes com as mesmas noites, não podem passar os dois.
   Use uma transação com nível de isolamento adequado e revalide dentro dela.
   Bônus avançado: constraint de exclusão do Postgres com `daterange`.
7. As transições de status ficam num lugar só (um método na entidade ou uma classe pura), não espalhadas nos serviços.
8. A antiga `ReservationController` vazia (`api/reservation`) pode ser removida ou reaproveitada.

**Conceitos novos:** paginação com `Skip`/`Take` + `CountAsync`; filtros opcionais com `IQueryable`; máquina de estados
simples; transações no EF Core (`Database.BeginTransactionAsync`); condição de corrida.

**Critérios de aceite**
- [x] Os filtros de status, when, período e busca funcionam combinados. `sort` inválido → `400`.
- [x] Confirmar → as noites aparecem bloqueadas no site. Cancelar → as noites somem.
- [x] Confirmar duas reservas que se sobrepõem → a segunda recebe `409`.
- [x] Confirmar uma reserva já cancelada → `409` (transição inválida).
- [x] O JSON da lista e do detalhe bate campo a campo com o `CONTRATO_PORTAL.md`. Na lista, o CPF **não** sai.
      (`bufferBeforeNights`/`bufferAfterNights` entram na task 16 e `source` na task 21.)
- [x] Testes unitários das transições de status e do filtro `when`, com "hoje" fixo.

**Armadilhas:** `Skip` sem `OrderBy` gera paginação instável; esquecer o `AsNoTracking`; `when` calculado com
`DateTime.Today` em vez do `IClock`; `search` com `ToLower()` (no Postgres, use `EF.Functions.ILike` e, se quiser
ignorar acentos, a extensão `unaccent`).

---

## Task 10 — Galeria e vídeos com upload

**Objetivo:** o portal envia fotos e cadastra vídeos, e o site volta a mostrar a galeria e o tour. As rotas e os formatos estão em `CONTRATO_PORTAL.md`, seção "Galeria e vídeos".

**O que fazer**
1. `Storage/IFileStorage` com:
   - `SaveAsync(stream, contentType, pasta)` → devolve a **chave** do arquivo (caminho relativo, ex.:
     `galeria/3f2a….webp`), **não** a URL;
   - `DeleteAsync(chave)`;
   - `GetPublicUrl(chave)` → URL absoluta.
2. `LocalFileStorage`: salva em `wwwroot/uploads/...` com **nome gerado** (Guid), nunca o nome enviado.
   `app.UseStaticFiles()` para servir. `wwwroot/uploads/` no `.gitignore`.
3. **No banco vai só a chave.** A URL absoluta (que o site exige) é montada **na leitura**, com
   `GetPublicUrl(chave)` = configuração `Storage:PublicBaseUrl` + chave. Em dev, a base é
   `http://localhost:5084/uploads/`. No futuro, vira o domínio do S3/CloudFront.
   - **Por que:** se o banco guardar `https://api.../uploads/x.webp`, migrar para o S3 exige um `UPDATE` em todas as
     linhas. Guardando a chave, migrar é copiar os arquivos (`aws s3 sync`), trocar a implementação no `Program.cs`
     e trocar a URL base na configuração.
   - `GalleryImage.Src`/`SrcSet` hoje foram pensados como URL. Decida se renomeia a coluna (ex.: `FileKey`) com uma
     migration, e como guardar as variantes do `srcSet` (item 8): lista de chaves ou um padrão de nome montado na leitura.
   - Link do YouTube continua gravado como URL: não é arquivo nosso.
4. Admin galeria: `POST` upload (`IFormFile` + `alt`), `PUT` alt, `PUT /reorder` (lista de ids na nova ordem), `DELETE` (apaga o arquivo também).
5. Admin vídeos: cadastro por **URL do YouTube** ou upload de `.mp4` + capa; `orientation`; reordenar; remover.
6. Validação: só `image/jpeg`, `image/png`, `image/webp` (confira também a **assinatura do arquivo**, não só a
   extensão); limite de tamanho (ex.: 10 MB foto, 100 MB vídeo) com `RequestSizeLimit`.
7. Lembrete no contrato: **nenhuma foto com hóspedes** (o portal mostra o aviso; a API só armazena).
8. Opcional: gerar versões 640/1280/1920 em WebP para o `srcSet` (biblioteca `SixLabors.ImageSharp`).

**Critérios de aceite**
- [x] Upload pelo Scalar → a foto aparece na galeria do site, na ordem certa.
- [x] Arquivo `.exe` renomeado para `.jpg` é recusado.
- [x] Remover a foto apaga o registro **e** o arquivo.
- [x] Nenhuma URL absoluta de arquivo nosso gravada no banco: trocar `Storage:PublicBaseUrl` muda todas as URLs da
  resposta sem mexer no banco.
- [x] Trocar `LocalFileStorage` por outra implementação exigiria mudar **só** o registro no `Program.cs`.

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
   `Cors__AllowedOrigins__0`, `Cors__PortalOrigins__0`, `PublicBaseUrl`...).
3. Migrations em produção: script (`dotnet ef migrations script --idempotent`) ou bundle (`dotnet ef migrations bundle`),
   **não** `Database.Migrate()` automático sem pensar.
4. CORS com o domínio real do site; HTTPS; `UseForwardedHeaders` se ficar atrás de proxy.
5. Uploads: disco persistente (volume) ou S3. O `S3FileStorage` **já existe** (pacotes `AWSSDK.S3` e
   `AWSSDK.Extensions.NETCore.Setup`) e é escolhido só por configuração, sem mudar código:
   ```
   Storage__Provider=S3
   Storage__BucketName=<bucket>
   Storage__PublicBaseUrl=https://<distribuição>.cloudfront.net/
   AWS__Region=sa-east-1
   ```
   - Credenciais por **IAM Role** da instância, com `s3:PutObject` e `s3:DeleteObject` só nesse bucket. Nenhuma chave
     da AWS em `appsettings`, variável de ambiente ou git.
   - Bucket **privado**, lido só pelo CloudFront (Origin Access Control).
   - Sem bucket ou sem credenciais, a API **não sobe**: o erro aparece no deploy, não na galeria do site.
   - No S3, a API não serve `/uploads`: quem serve é o CloudFront. Os objetos vão com `Cache-Control` de 1 ano
     (a chave nunca é reaproveitada).
   - Arquivos que já existirem no disco: `aws s3 sync ChaleAltoDoMorroAPI/wwwroot/uploads s3://<bucket>/`. O banco não muda.
   - Ainda não testado contra um bucket de verdade: no primeiro deploy, enviar uma foto pelo portal, abrir a URL e apagá-la.
6. Checklist de publicação: testes verdes no `develop` → merge `develop` → `master` → deploy.
   Na landing e no portal, `VITE_API_URL` aponta para a API publicada.

**Critérios de aceite**
- [ ] `docker build` + `docker run` com variáveis de ambiente sobe a API conectada a um Postgres.
- [ ] Nenhum segredo na imagem.

---

## Task 13 — CORS do portal

**Objetivo:** o portal (React, no navegador) consegue chamar as rotas admin.

**Por que:** a política "Site" libera só `GET`/`POST` e o header `Content-Type`. O portal usa `PUT`, `PATCH` e `DELETE`
e manda o header `Authorization`, então o navegador bloqueia a chamada no **preflight** (`OPTIONS`).

**O que fazer**
1. Crie uma segunda política, `"Portal"`, com:
   - as origens vindas de `Cors:PortalOrigins` (em dev `http://localhost:5174`);
   - os métodos `GET, POST, PUT, PATCH, DELETE`;
   - os headers `Content-Type` e `Authorization`.
2. Aplique a política do portal nos controllers admin e no login. Há dois caminhos: `[EnableCors("Portal")]` no
   `AdminControllerBase` e no `AuthController`, ou uma política única com as duas listas de origens. Justifique a escolha
   na revisão.
3. A origem do portal em produção vem de variável de ambiente (task 12).

**Conceitos novos:**
- O que é o preflight `OPTIONS`.
- Por que `AllowAnyOrigin` + credenciais é proibido.
- CORS **não** é segurança do servidor, é uma regra do navegador. Quem protege a API é o JWT.

**Critérios de aceite**
- [x] Do portal em `localhost:5174`, o login e o `PUT /api/admin/pricing/weekday` funcionam sem erro de CORS no console.
  *(Conferido simulando o preflight com `curl`; confirmar no navegador na fase P1 do portal.)*
- [x] O site em `localhost:5173` continua funcionando como antes.
- [x] Nenhuma origem fixa no código: tudo vem de configuração.

---

## Task 14 — Livro Caixa: domínio, regras puras e migration

**Objetivo:** modelar o Livro Caixa no banco e escrever as regras numa classe pura, **com os testes antes** (TDD, igual à task 4).

**Por que:** o caixa é dinheiro de verdade. Se o cálculo do caixa líquido ou da data da despesa fixa estiver errado, o
dono decide com base em número errado. Com a regra pura e testada, a API e o portal mostram o mesmo número.

**O que fazer** (regras e casos de teste em `docs/REGRAS_CAIXA.md`)
1. Entidades em `Domain/`:
   - `CashBook`: `Id` (int), `Year`, `Month`, `Status` (enum `Open/Closed`), `Notes?`, `CreatedAt`, `UpdatedAt`,
     `ClosedAt?` e a coleção `Transactions`.
   - `CashTransaction`: `Id` (Guid), `CashBookId`, `Date` (DateOnly), `Type` (enum `Income/Expense`), `Amount` (decimal),
     `Description`, `CategoryId`, `PaymentMethod` (enum `Pix/Cash/Card/Transfer/Other`), `Notes?`,
     `RecurringExpenseId?`, `CreatedAt`, `UpdatedAt`.
   - `CashCategory`: `Id` (int), `Name`, `Type`, `Color`, `Icon`, `IsActive`, `Order`.
   - `RecurringExpense`: `Id` (int), `Description`, `Amount`, `CategoryId`, `DayOfMonth` (1–31), `PaymentMethod`,
     `IsActive`, `StartMonth` e `EndMonth?`. Para os meses, uma sugestão: guardar como `DateOnly` do dia 1 e expor como `"AAAA-MM"`.
2. Configurações em `Data/Configurations/`:
   - `numeric(12,2)` nos valores e enums como texto.
   - Índice único em `(Year, Month)`.
   - Índice único **filtrado** em `(CashBookId, RecurringExpenseId)`, com `WHERE ... IS NOT NULL`.
   - `OnDelete(SetNull)` para `RecurringExpenseId` e `Restrict` para `CategoryId`.
3. Seed das categorias padrão (tabela da regra 19), em **todos os ambientes**, só quando a tabela estiver vazia.
4. `Domain/CashBookRules` (pura):
   - `Summarize(transações)` → income, expense, net, byCategory e daily.
   - `RecurringDate(dayOfMonth, ano, mês)` → a data.
   - `RecurringToApply(modelos, mês, idsJáLançados)` → o que criar.
   - Validações de transação (data dentro do mês, tipo da categoria).
5. Testes xUnit com **todas** as tabelas de `REGRAS_CAIXA.md`: resumo, transação, livro, data da despesa fixa e aplicar despesas fixas.
6. Migration `LivroCaixa`.

**Conceitos novos:**
- Índice único filtrado no Postgres (`HasFilter`).
- `DateTime.DaysInMonth`.
- Por que o valor é sempre positivo e o sinal vem do tipo (evita uma "saída de −50").

**Critérios de aceite**
- [x] Migration aplicada, com as tabelas e os índices visíveis no banco.
- [x] Todos os casos de `REGRAS_CAIXA.md` que não dependem de HTTP viram testes e passam.
- [x] `CashBookRules` não referencia EF nem relógio. Nenhum `double` em dinheiro.

**Armadilhas:**
- `DayOfMonth = 31` em fevereiro dá exceção no `new DateOnly(...)`.
- Somar `decimal` com LINQ no banco pode voltar `null` num livro vazio. Use `?? 0` ou some em memória.
- Esquecer o filtro no índice único faz todas as transações sem `RecurringExpenseId` colidirem.

---

## Task 15 — Livro Caixa: rotas e despesas fixas

**Objetivo:** expor tudo do caixa para o portal, exatamente como em `CONTRATO_PORTAL.md`, seção "Livro Caixa".

**O que fazer**
1. `SrvCashBook` e os controllers `AdminCashBookController`, `AdminCashTransactionController`, `AdminCashCategoryController`
   e `AdminRecurringExpenseController`, todos herdando `AdminControllerBase`.
2. Livros:
   - listar, com filtros e ordenação;
   - visão anual;
   - criar, com `applyRecurring`;
   - detalhe por `{year}/{month}`;
   - notas, fechar, reabrir e aplicar despesas fixas;
   - apagar, só quando vazio.
3. Transações: criar, editar e apagar, respeitando o livro fechado (`409`).
4. Categorias: CRUD, em que "apagar" vira "desativar" quando a categoria está em uso. Despesas fixas: CRUD.
5. Use o `AdminResult` para responder `404`, `409` e `422` com mensagens em PT-BR, que o portal mostra direto.

**Conceitos novos:**
- Rotas com mais de um parâmetro (`{year:int}/{month:int:range(1,12)}`).
- `201` com `CreatedAtAction` numa rota composta.
- Como decidir o que é `409` (estado) e o que é `422` (regra).

**Critérios de aceite**
- [x] Os casos HTTP de `REGRAS_CAIXA.md` (livro fechado, mês duplicado, categoria de tipo errado...) devolvem o status certo.
- [x] Criar outubro com `applyRecurring: true` já traz as despesas fixas ativas.
- [x] O JSON bate campo a campo com o `CONTRATO_PORTAL.md`.
- [x] Tudo testável pelo Scalar.

---

## Task 16 — Folga entre hóspedes

**Objetivo:** implementar "check-in bloqueia a véspera" e "check-out bloqueia a data", com um valor global que o portal
edita e um valor por reserva gravado na confirmação.

**Por que:** o dono precisa de tempo para limpar, preparar a cesta e decorar entre um hóspede e outro. A folga estava em
"ideias para o futuro" e agora entrou na v1. Regras e casos de teste em `docs/REGRAS_RESERVA.md`, seção "Folga entre hóspedes".

**O que fazer**
1. Configuração global no banco: uma tabela `Settings` (chave/valor) ou uma entidade `BookingSettings` de linha única,
   com `BufferBeforeNights` e `BufferAfterNights` (de 0 a 3, padrão 0). Rotas `GET/PUT /api/admin/settings/buffer`.
2. `Reservation` ganha `BufferBeforeNights?` e `BufferAfterNights?`, preenchidos **na confirmação** (task 9) com o valor
   do pedido ou, se ele não vier, com o global.
3. Classe pura `OccupancyRules`:
   - noites ocupadas publicadas por uma reserva confirmada, usando os globais atuais (regra 4);
   - conflito entre duas estadias (regra 3).

   O `BookingRules.Nights` **não muda**: ele continua sendo o que se cobra.
4. `SrvAvailability` passa a usar a `OccupancyRules`. Com isso, o `GET /api/blocked-dates`, a validação da pré-reserva e
   a confirmação passam a respeitar a folga sem mudança própria.
5. `GET /api/admin/calendar` (task 18) devolve as `bufferNights`. *(Fica para a task 18, que cria a rota.)*

**Critérios de aceite**
- [x] Todos os casos da tabela "Folga entre hóspedes" de `REGRAS_RESERVA.md` viram teste e passam.
- [x] Com 0/0, **nenhum** teste antigo quebra: o comportamento de hoje fica igual.
- [x] Com o global 0/1 e uma reserva confirmada, o calendário do **site** mostra a noite do check-out bloqueada.
- [x] Mudar o global depois não muda a folga de uma reserva já confirmada, que vale o snapshot (ver o caso "global mudou").

---

## Task 17 — Hóspedes

**Objetivo:** a tela "Hóspedes" do portal: lista com busca, filtros e ordenação, detalhe com o histórico e edição do contato.

**O que fazer** (formatos em `CONTRATO_PORTAL.md`, seção "Hóspedes")
1. `Guest` ganha `Notes?` (até 1000), com migration.
2. `GET /api/admin/guests`:
   - campos calculados `stays`, `totalSpent`, `lastStay` e `nextStay`, contando só reservas confirmadas, como hóspede 1 **ou** 2;
   - `search` por nome, telefone, e-mail ou CPF;
   - `sort` por `name`, `stays`, `totalSpent` ou `lastStay`;
   - paginação.
3. `GET /api/admin/guests/{id}`, com o CPF e todas as reservas, de qualquer status.
4. `PATCH /api/admin/guests/{id}` (nome, telefone, e-mail, notas), com a mesma validação de formato da pré-reserva.

**Conceitos novos:**
- Projeção com subconsultas agregadas (`Count`, `Sum`, `Max` dentro do `Select`).
- Ordenar por um campo calculado.
- Olhar o SQL gerado (`ToQueryString()`) para conferir que não virou N+1.

**Critérios de aceite**
- [x] Ordenar por `totalSpent desc` traz primeiro quem mais gastou, contando só as confirmadas.
- [x] O CPF não sai na lista, só no detalhe.
- [x] Buscar "5299" encontra o hóspede pelo CPF.

---

## Task 18 — Calendário do portal

**Objetivo:** `GET /api/admin/calendar?from=&to=`, que alimenta a agenda e o mini calendário do dashboard. Traz os
detalhes que o site público não pode ver: nome do hóspede, valor e motivo do bloqueio.

**O que fazer:** seguir o formato do `CONTRATO_PORTAL.md`, seção "Calendário". A rota devolve:
- as reservas `confirmed` e `pending` que tocam o intervalo;
- os bloqueios manuais, com o motivo;
- os pacotes que tocam o intervalo;
- as `bufferNights` das confirmadas (depende da task 16; antes dela, lista vazia).

O intervalo tem no máximo 100 dias; acima disso → `400`.

**Critérios de aceite**
- [x] Uma reserva de 30/10 → 02/11 aparece tanto em `from=2026-10-01&to=2026-10-31` quanto em novembro.
- [x] O `reason` dos bloqueios aparece aqui (rota autenticada) e continua fora da rota pública.

---

## Task 19 — Dashboard

**Objetivo:** `GET /api/admin/dashboard?period=&year=&index=`, com os números e as séries do dashboard, a comparação com o
período anterior e o bloco do Livro Caixa.

**Por que:** o dono quer ver de relance como estão o mês, o trimestre, o semestre ou o ano: faturamento, ocupação, caixa
líquido e o que precisa de atenção (pré-reservas pendentes, despesas fixas não lançadas, mês negativo).

**O que fazer:** seguir o formato e as **regras de cálculo** do `CONTRATO_PORTAL.md`, seção "Dashboard".
1. Uma classe pura para os períodos:
   - `DashboardPeriod.Of(kind, year, index)` → from, to, label, e `Previous()` → o período anterior;
   - o anterior do 1º trimestre de 2026 é o 4º trimestre de 2025, e o anterior de janeiro é dezembro do ano anterior.
2. Métricas de reserva: faturamento pelo check-in, noites vendidas recortadas ao período e ocupação.
3. Métricas do caixa: a soma dos livros dos meses do período, mais `topExpenses`, `pendingRecurring`,
   `monthsWithoutBook` e `negativeMonths`.
4. `series` (por dia no mês, por mês nos demais), `expensesByCategory`, `topKits`, `upcoming` e `pendingCount`.

**Critérios de aceite**
- [x] Testes da classe de período: todos os tipos, virada de ano, e `index` inválido → `400`.
- [x] Testes do recorte de noites: a estadia de 30/10 → 02/11 conta 2 noites em outubro e 1 em novembro.
- [x] `occupancyRate` fica entre 0 e 1. Em período sem noites disponíveis, vale 0 (sem divisão por zero).

**Armadilhas:** carregar tudo em memória. Filtre por período no banco e agregue lá quando der; o volume é pequeno, mas o
hábito importa.

---

## Task 20 — Trocar senha

**Objetivo:** `POST /api/admin/me/password` com `{ currentPassword, newPassword }`, usando o
`UserManager.ChangePasswordAsync`.

**Critérios de aceite**
- [x] Senha atual errada → `422` "Senha atual incorreta".
- [x] Senha nova fraca → `422` com as regras que falharam, em PT-BR.
- [x] Depois da troca, o login com a senha antiga falha e com a nova funciona.

---

## Task 21 — Admin: criar e editar reservas

**Objetivo:** o dono cadastra pelo portal as reservas fechadas direto no WhatsApp ou por telefone, sem passar pela
pré-reserva do site, e edita qualquer reserva que ainda não foi cancelada. O preço pode ser **diferente do calculado**
(desconto informal, cortesia, outros combinados).

**Por que:** nem todo cliente passa pelo site. Sem esta rota, o calendário e o dashboard ficam incompletos, e o dono
acaba bloqueando datas "na mão" sem saber de quem é a reserva.

**O que fazer** (formatos em `CONTRATO_PORTAL.md`, seção "Reservas"; regras em `REGRAS_RESERVA.md`, seção
"Reserva criada ou editada pelo portal")
1. `POST /api/admin/reservations` → `201 ReservationDetailDto` (com `Location`).
   - O corpo escolhe o `status` inicial: `pending` ou `confirmed`.
   - **Só a disponibilidade é validada**, contra reservas confirmadas e noites bloqueadas. O mínimo de diárias, o pacote
     fechado e as datas no passado ficam liberados para o admin.
   - Hóspede 1 com nome, **CPF** e telefone obrigatórios, igual ao site. O hóspede 2 é opcional.
   - Preço: `total` informado → vale ele. `total` nulo → calculado pelo `BookingRules`. Grava sempre também o
     `CalculatedTotal`, para o portal mostrar "preço ajustado".
2. `PUT /api/admin/reservations/{id}` → `200 ReservationDetailDto`. Edita **tudo**: datas, hóspedes, kit, preço e notas.
   - Reserva cancelada → `409`. O status não muda por aqui: para isso existem as rotas de confirmar e cancelar.
   - Mudou as datas de uma reserva **confirmada** → revalida a disponibilidade ignorando a própria reserva, dentro da mesma
     transação da confirmação (task 9, Parte C).
3. Hóspedes: a rota é autenticada, então **pode atualizar o cadastro** de um hóspede que já existe (achado pelo CPF).
   A rota pública continua sem sobrescrever nada.
4. Migration `ReservaPeloPortal`:
   - `Source` (enum `Site/Portal`, texto no banco). As reservas antigas ficam `Site`.
   - `CalculatedTotal` (decimal). Nas reservas antigas, recebe o próprio `Total`.
   - `EstimatedTotalFromSite` passa a aceitar `null`, porque a reserva do portal não tem esse valor.
5. As regras novas ficam no `BookingRules` ou numa classe pura irmã, com os testes da tabela de `REGRAS_RESERVA.md`.

**Conceitos novos:**
- `PUT` como substituição completa: o portal manda o formulário inteiro.
- Migration que altera coluna e preenche dado existente (`migrationBuilder.Sql`).
- Reaproveitar a mesma transação e revalidação entre dois casos de uso.

**Critérios de aceite**
- [x] Criar uma reserva `confirmed` pelo Scalar → as noites aparecem riscadas no site na hora.
- [x] Criar sexta → sábado (1 diária) pelo portal → ok. A mesma estadia pelo site continua `422`.
- [x] Criar ou editar sobre uma noite ocupada → `409` com a noite no `detail`.
- [x] `total` informado → gravado como veio, com `priceAdjusted: true` no detalhe. `total` nulo → igual ao calculado.
- [x] Editar as datas de uma confirmada para cima de outra confirmada → `409`. Editar uma cancelada → `409`.
- [x] CPF inválido ou ausente no hóspede 1 → `400`.
- [x] O JSON bate com o `CONTRATO_PORTAL.md`. Testes das regras novas passando.

**Armadilhas:**
- No `PUT`, `total` nulo **recalcula**: se o portal não mandar o total atual, um desconto combinado some ao editar só o
  telefone. O portal precisa mandar o valor que está na tela.
- Esquecer de atualizar o `ContactPhone` (snapshot) quando o telefone do hóspede 1 muda.
- `IncludesColonialBreakfast` depende do número de diárias: recalcule ao mudar as datas.

---

## Ideias para o futuro (fora do escopo atual)

- **Folga entre hóspedes**: entrou na v1 como **task 16**. A decisão tomada foi "só entre reservas" (regra 5 em
  `REGRAS_RESERVA.md`).
- **Refresh token**: hoje o token vale 60 minutos e o portal volta para o login quando ele vence. Uma melhoria é um refresh
  token em cookie `HttpOnly` com rotação, para o dono não precisar logar de novo no celular.
- **Estorno automático no caixa** ao cancelar uma reserva já lançada. Hoje o dono decide manualmente (regra 24 de
  `REGRAS_CAIXA.md`).
- **Anexos no caixa** (foto do comprovante ou da nota) usando o `IFileStorage` da task 10.
- **Papéis** (dono × ajudante): hoje qualquer usuário logado pode tudo.
- **Refúgio de Santiago / multi-hospedagem** (2027, decisão no ADR-014 de `ChaleAltoDoMorroDocs/DECISOES.md`): a mesma
  API e o mesmo portal atendem as duas operações. Modelo `Property` → `Unit`, acesso usuário × operação, caixa por
  operação com visão geral e hóspede único. Abrir a trilha de tasks **3 a 4 meses antes** da abertura. A migration cria o
  Chalé como primeira operação e liga os dados existentes a ele. Até lá, não deixar "Chalé" fixo no código: nomes,
  valores e textos vêm do banco ou da configuração.
- **Pagamento em duas partes (sinal + restante)**: hoje o combinado com o hóspede é **50% para confirmar e 50% no
  check-in**. A ideia é o portal mostrar, em cada reserva, quanto já foi **pago** e quanto **falta**. Um caminho simples:
  uma entidade `ReservationPayment` (valor, data, forma de pagamento, tipo `deposit`/`balance`) e os campos calculados
  `amountPaid` e `amountDue` no detalhe e na lista, com filtro "com saldo em aberto". Cada pagamento poderia gerar a sua
  entrada no Livro Caixa, no mês em que o dinheiro entrou. Por causa desse esquema, o "lançar reserva no caixa" saiu da
  v1: por enquanto, os recebimentos são lançados à mão no caixa.
