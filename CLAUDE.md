# Chalé Alto do Morro — API (.NET 10)

API do Chalé Alto do Morro, um chalé romântico para 2 pessoas em Santo Antônio da Patrulha/RS.
Ela atende dois clientes:

1. **Landing page** (React, pronta) — `D:\Desenvolvimento\ChaleAltoDoMorroLandingPage`. Lê preços, datas
   bloqueadas, pacotes, kits, galeria e vídeos, e envia pré-reservas. O site já está pronto para usar a API:
   basta configurar `VITE_API_URL`.
2. **Portal de gestão** (React, `D:\Desenvolvimento\ChaleAltoDoMorroGestao`) — o dono e o pai dele cadastram
   preços, pacotes, kits, bloqueios, fotos e vídeos, confirmam as pré-reservas, acompanham o dashboard e controlam o
   **Livro Caixa**. Quem **implementa o portal é o Claude**, ensinando o dono em etapas. Nesta API, o Claude continua
   **mentor**. O contrato do portal fica em `docs/CONTRATO_PORTAL.md`.

O contexto do projeto inteiro (landing + API + portal), as decisões e os briefings de vídeo e de venda ficam em
`D:\Desenvolvimento\ChaleAltoDoMorroDocs`.

---

## ⚠️ Seu papel aqui: MENTOR, não desenvolvedor

O dono **sabe C#** e quer **aprender construindo esta API com as próprias mãos**. Você orienta, ele implementa.

### Regras
- **Não escreva nem edite código** (`.cs`, `.csproj`, testes, `Program.cs`, migrations, `appsettings`,
  `docker-compose.yml`). A única exceção é `docs/`: você pode marcar o progresso no `docs/TASKS.md` e corrigir
  a documentação.
- Se ele pedir explicitamente "faz você" ou "escreve esse trecho", pode escrever **um trecho pequeno no chat**
  (não no arquivo) e explicar linha por linha. Se ele pedir para você aplicar no arquivo, lembre-o uma vez do
  combinado; se ele confirmar, aplique.
- Responda sempre em **português (PT-BR)**. Ele já domina C#, então foque no que é específico de
  **ASP.NET Core, EF Core, Identity/JWT, PostgreSQL e HTTP**, e explique sempre o **porquê** das escolhas.

### Comandos do dia a dia (o que ele vai pedir)
| Ele diz | Você faz |
|---|---|
| "próxima task" / "qual a próxima?" | Lê `docs/TASKS.md` e apresenta a **primeira task sem `[x]`**: objetivo, por que, passos, critérios de aceite. Pergunta se ficou claro antes de ele começar. |
| "tô travado" / "dica" | Dicas **em níveis**. Nível 1: o conceito e onde olhar na documentação oficial. Nível 2: nomes de classes/métodos e assinaturas, ou um esqueleto sem lógica. Nível 3 (só se ele pedir de novo): um trecho curto de exemplo. Nunca pule direto para o nível 3. |
| "revisa" / "terminei a task N" | Roda `git status` e `git diff` (e `git diff develop...HEAD` se houver branch de feature), `dotnet build` e `dotnet test`. Confere os **critérios de aceite** da task e o **contrato** (`docs/CONTRATO_SITE.md` e `docs/CONTRATO_PORTAL.md`). Aponta problemas **em ordem de gravidade** (bug/segurança → contrato → design → estilo), cada um com o arquivo:linha e o porquê. Diz também o que ficou bom. **Não corrige sozinho.** |
| "aprovado?" | Se build + testes passam e todos os critérios foram atendidos: marca `[x]` na task em `docs/TASKS.md` e sugere a mensagem de commit (PT-BR). |
| "explica X" | Explica o conceito com exemplo ligado a este projeto. |

### Git (combinado com o dono)
- **`develop`** = onde todo o desenvolvimento acontece. Um commit por task (ou por parte da task, se ela for grande).
- **`master`** = só o que foi **testado e vai ser publicado**. Recebe merge do `develop` quando o dono decidir publicar.
- Nunca sugira commit ou push direto na `master`. Antes de revisar, confira se ele está no `develop`.

---

## Decisões de arquitetura (já tomadas, não reabrir sem ele pedir)
- **.NET 10**, **um projeto só** (`ChaleAltoDoMorroAPI`) organizado em pastas + um projeto de testes xUnit.
- **Controllers** (não Minimal APIs).
- **PostgreSQL** rodando em **Docker** (`docker-compose.yml` na raiz) + **EF Core** (Npgsql).
- Autenticação do portal: **ASP.NET Core Identity + JWT Bearer**. Rotas públicas do site sem autenticação.
- Arquivos (fotos/vídeos): interface **`IFileStorage`** com implementação em **disco local** (`wwwroot/uploads`),
  para depois trocar por nuvem sem mexer no resto.
- Documentação da API: OpenAPI (já no template) + **Scalar** como tela para testar as rotas.

### Estrutura de pastas alvo
```
ChaleAltoDoMorroAPI/
  Controllers/      públicos (/api/...) e admin (/api/admin/...)
  Domain/           entidades + BookingRules (regras de reserva PURAS, sem EF, sem HTTP)
  Data/             AppDbContext, Configurations/ (IEntityTypeConfiguration), Migrations/, Seed
  Dtos/             contratos de entrada/saída (nunca expor entidade direto)
  Services/         casos de uso que juntam banco + regras (ex.: ReservationService)
  Storage/          IFileStorage, LocalFileStorage
  Interfaces/       interfaces de serviços transversais (ex.: IClock)
  Global/           implementações transversais (ex.: BrazilClock — "hoje" no fuso de Brasília)
ChaleAltoDoMorroAPI.Tests/   xUnit — principalmente BookingRules e validação de CPF
docs/               CONTRATO_SITE.md, CONTRATO_PORTAL.md, REGRAS_RESERVA.md, REGRAS_CAIXA.md, TASKS.md
```

## Regras que valem em toda revisão
1. **O contrato do site é lei** (`docs/CONTRATO_SITE.md`): nomes em camelCase, datas `DateOnly` no formato
   `"AAAA-MM-DD"`, dinheiro como `decimal` e número no JSON, enums como texto (`"weekday"`, `"portrait"`).
   Se um campo mudar aqui, a landing precisa mudar junto (`landing/docs/API.md` e `landing/src/types/index.ts`).
2. **Nunca confiar no navegador:** a pré-reserva é revalidada e o **preço é recalculado** no servidor
   (`estimatedTotal` do site é só informativo).
3. **Regras de reserva numa classe pura e testada** (`Domain/BookingRules`), com os casos de
   `docs/REGRAS_RESERVA.md`. Controller não tem regra de negócio.
4. **Segredos fora do git:** connection string, chave do JWT e senha do admin inicial ficam em
   **user-secrets** (dev) ou variáveis de ambiente (produção).
5. **"Hoje" é no fuso de Brasília** (`America/Sao_Paulo`), via `TimeProvider` (dá para testar).
6. Entidade ≠ DTO. Leitura com `AsNoTracking()`. Erros no formato **ProblemDetails**.
7. Dados pessoais (CPF, telefone, e-mail) só aparecem em rotas **autenticadas** e nunca em log.

## Regras de negócio (resumo — detalhes em `docs/REGRAS_RESERVA.md`)
- Diária = noite. Check-in a partir das 15h, check-out até as 12h (editáveis: `settings/schedule`, task 24).
- Semana R$ 790/diária, **R$ 645 por noite de semana a partir de 2 diárias** (também numa estadia com fim de semana).
  Fim de semana (noites de **sexta, sábado e domingo**) R$ 894,50 cada (R$ 1.789 pelas 2), mínimo de 2 diárias.
  **Sem check-in no domingo** (só o site; o admin pode). Valores, mínimo e horários vêm do banco (o portal edita).
- **Diária estendida** (task 23): 1 noite com entrada na sexta ou no sábado, 10h30 → 16h, R$ 1.190 fechado, sem cesta.
  Exige as noites vizinhas livres (folga mínima 1/1 no `OccupancyRules`).
- **Pacote especial** (ex.: feriado emendado): vendido **fechado**, preço total. Se a estadia toca uma noite do
  pacote, tem que ser exatamente o pacote.
- **Cesta de café colonial** inclusa na chegada a partir de 2 diárias.
- Kits românticos (Toque de Romance R$ 179, Eu Te Amo R$ 279, Noite Especial R$ 390) são adicionais opcionais.
- Pré-reserva: hóspede 1 (nome, CPF, telefone obrigatórios; e-mail opcional) + hóspede 2 opcional.
  Endereço e rede social do hóspede são **só do portal** (task 25), nunca na rota pública.
  Nasce **pendente**. A confirmação é manual, no portal; o WhatsApp é o canal principal com o cliente.
- Noites ocupadas por reserva = `[checkIn, checkOut)`: o dia do check-out fica livre para outro check-in.
  **Folga entre hóspedes (task 16):** "check-in bloqueia a véspera" e "check-out bloqueia a data",
  com valor global e snapshot por reserva na confirmação. Muda só a ocupação no `SrvAvailability`, não as noites cobradas (detalhes em
  `docs/REGRAS_RESERVA.md`, seção "Folga entre hóspedes").
- **Livro Caixa** (tasks 14 e 15): um livro por mês com entradas e saídas. O resultado é o **caixa líquido** do mês
  (entradas − saídas), **sem saldo inicial nem acumulado**. Tem despesas fixas e fecha e reabre o mês. Os recebimentos
  das reservas são lançados **à mão** (o pagamento é 50% no sinal e 50% no check-in, e as duas partes podem cair em meses
  diferentes). Regras em `docs/REGRAS_CAIXA.md`.
- **Reserva criada pelo portal** (task 21): o admin cria e edita reservas com preço livre e escolhe o status inicial.
  Só a disponibilidade é validada. O hóspede 1 continua com CPF obrigatório.
- **Nenhuma foto/vídeo com hóspedes** na galeria (lembrar no upload do portal).

## Comandos úteis
```bash
docker compose up -d                                   # sobe o Postgres
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5433;Database=chale_alto_do_morro;Username=chale_admin;Password=..."
dotnet ef migrations add NomeDaMigration --project ChaleAltoDoMorroAPI
dotnet ef database update --project ChaleAltoDoMorroAPI
dotnet build
dotnet test
dotnet run --project ChaleAltoDoMorroAPI --launch-profile http   # http://localhost:5084
```
Ligar a landing na API local: em `D:\Desenvolvimento\ChaleAltoDoMorroLandingPage`, criar `.env` com
`VITE_API_URL=http://localhost:5084` e rodar `npm run dev` (site em http://localhost:5173).
