# Contrato da API com o portal de gestão

Este documento lista as rotas que o **portal de gestão** (`D:\Desenvolvimento\ChaleAltoDoMorroGestao`, em React) usa.
É a **fonte da verdade** do portal. Ele é implementado com mocks (MSW) que seguem exatamente este formato, então
**qualquer diferença de nome, tipo ou formato quebra o portal** quando ele é ligado na API de verdade.

- As rotas marcadas ✅ já existem (tasks 7 e 8).
- As marcadas 🆕 são descritas nas tasks do `docs/TASKS.md` (o número da task está ao lado).
- Regras de negócio do caixa: `docs/REGRAS_CAIXA.md`. Regras de reserva e folga: `docs/REGRAS_RESERVA.md`.
- Se mudar algo aqui, avise o Claude do portal (ou atualize o mock em `src/mocks/` do portal).

## Convenções (as mesmas do `CONTRATO_SITE.md`, mais estas)

| Assunto | Regra |
|---|---|
| Autenticação | Todas as rotas `/api/admin/...` exigem `Authorization: Bearer <accessToken>` (sem token → `401`) |
| Mês | `"AAAA-MM"` (ex.: `"2026-10"`) quando o campo representa um mês inteiro |
| Data e hora | `DateTimeOffset` ISO 8601 (`"2026-10-01T14:32:00-03:00"`) |
| Enums | texto camelCase (`"pending"`, `"income"`, `"pix"`) |
| Dinheiro | número com até 2 casas (`1789`, `894.5`). No banco `numeric(12,2)` |
| Listas pequenas | array direto (preços, pacotes, kits, bloqueios, categorias, despesas fixas, livros caixa) |
| Listas paginadas | reservas e hóspedes: `{ "items": [...], "page": 1, "pageSize": 20, "totalCount": 57 }` |
| Ordenação | `?sort=<campo>&order=asc\|desc`. Cada rota lista os campos aceitos. Campo desconhecido → `400` |
| Busca | `?search=texto` (sem diferenciar maiúsculas nem acentos, quando a rota aceitar) |
| Erros | ProblemDetails. `400` formato inválido · `401` sem login · `404` não existe · `409` conflito de estado/datas · `422` regra de negócio. O portal mostra o `detail` para o usuário, então escreva em PT-BR claro |
| CORS | a origem do portal (dev `http://localhost:5174`) com `GET, POST, PUT, PATCH, DELETE` e os headers `Content-Type` e `Authorization` (task 13) |

---

## Autenticação

### ✅ `POST /api/auth/login`
```json
{ "email": "dono@exemplo.com", "password": "..." }
```
→ `200 { "accessToken": "eyJ...", "expiresAt": "2026-10-01T15:32:00+00:00" }` · `401` com mensagem genérica.

O token vale 60 minutos e não há refresh token. Quando o token vence, o portal volta para o login com o aviso
"Sua sessão expirou".

### ✅ `GET /api/admin/me` → `{ "email": "dono@exemplo.com" }`

### ✅ `POST /api/admin/me/password` (task 20)
```json
{ "currentPassword": "...", "newPassword": "..." }
```
→ `204` · `422` quando a senha atual está errada ou a nova é fraca (a mensagem diz qual regra falhou).

---

## Preços, pacotes, kits e bloqueios (✅ task 8)

### Preços
- `GET /api/admin/pricing` → `PricingRuleDto[]` (mesmo formato do site: `kind`, `label`, `pricePerNight`, `minNights?`).
- `GET /api/admin/pricing/{kind}` → `PricingRuleDto`.
- `PUT /api/admin/pricing/{kind}` com `{ "label": "...", "pricePerNight": 894.5, "minNights": 2 }` → `PricingRuleDto`.
  - O preço é sempre **por diária**. O portal deixa digitar o total do fim de semana e divide por `minNights`.

### Pacotes
- `GET /api/admin/packages?includePast=false` → `AdminPackageDto[]`.
- `GET /api/admin/packages/{id}` → `AdminPackageDto`.
- `POST /api/admin/packages` → `201` + `AdminPackageDto` · `409` (sobreposição) · `422`.
- `PUT /api/admin/packages/{id}` → `AdminPackageDto`.
- `DELETE /api/admin/packages/{id}` → `204` · `404`.

```json
// AdminPackageDto
{ "id": "5f0c...", "name": "Feriadão de Finados", "description": "...", "checkIn": "2026-10-30",
  "checkOut": "2026-11-02", "price": 2490, "pendingReservationsInDates": 0 }
// SavePackageRequest
{ "name": "...", "description": "...", "checkIn": "2026-10-30", "checkOut": "2026-11-02", "price": 2490 }
```

### Kits
- `GET /api/admin/kits` → `AdminKitDto[]`.
- `GET /api/admin/kits/{id}` → `AdminKitDto`.
- `POST /api/admin/kits` → `201` + `AdminKitDto`.
- `PUT /api/admin/kits/{id}` → `AdminKitDto`.
- `PATCH /api/admin/kits/{id}/active` com `{ "isActive": false }` → `AdminKitDto`.
- `DELETE /api/admin/kits/{id}`:
  - `204` = o kit foi apagado.
  - `200 { "deactivated": true, "message": "..." }` = o kit já foi usado em reservas, então foi só desativado.

```json
// AdminKitDto (SaveKitRequest = igual, sem id)
{ "id": "8a1b...", "name": "Kit Toque de Romance", "description": null, "price": 179,
  "items": ["Espumante...", "Pétalas de rosa"], "isActive": true, "order": 0 }
```

### Bloqueios manuais
- `GET /api/admin/blocked-dates?from=2026-10-01&to=2026-12-31` → `[{ "date": "2026-10-14", "reason": "Manutenção" }]`.
  Sem `from`, a lista começa em hoje.
- `POST /api/admin/blocked-dates` com `{ "from": "2026-10-10", "to": "2026-10-12", "reason": "Uso próprio" }`
  → `{ "created": 3, "alreadyBlocked": 0 }`. `from` e `to` são **noites**, e as duas entram no bloqueio.
- `DELETE /api/admin/blocked-dates/{date}` → `204` · `404`.

---

## Reservas (🆕 task 9)
A "pré-reserva" é uma reserva com `status = "pending"`. As **duas telas do portal usam a mesma rota**:
- a tela "Pré-reservas" usa `status=pending` (e `cancelled` no histórico);
- a tela "Reservas" usa `status=confirmed`.

### `GET /api/admin/reservations`

| Parâmetro | Valores | Observação |
|---|---|---|
| `status` | `pending` \| `confirmed` \| `cancelled` (pode repetir: `status=pending&status=cancelled`) | sem `status` = todas |
| `when` | `today` \| `ongoing` \| `upcoming` \| `past` | Só faz sentido com `confirmed`. Ver a regra abaixo |
| `from`, `to` | `AAAA-MM-DD` | A estadia precisa **tocar** o intervalo: `checkIn <= to` e `checkOut >= from` |
| `search` | texto | Procura no nome do hóspede 1 ou 2 e no telefone |
| `sort` | `createdAt` (padrão para pending) \| `checkIn` (padrão para confirmed) \| `total` \| `guestName` | |
| `order` | `asc` \| `desc` | Padrão: `desc` para `createdAt`, `asc` para `checkIn` |
| `page`, `pageSize` | padrão 1 e 20, máximo 100 | |

Regra do `when` (com "hoje" no fuso de Brasília):
- `today`: check-in ou check-out hoje;
- `ongoing`: `checkIn < hoje < checkOut`;
- `upcoming`: `checkIn > hoje`;
- `past`: `checkOut < hoje`.

→ `Paged<ReservationListItemDto>`
```json
{
  "items": [{
    "id": "c1d2...",
    "status": "pending",
    "checkIn": "2026-10-09", "checkOut": "2026-10-11", "nights": 2,
    "guestName": "Ana Souza", "secondGuestName": "Bruno Lima",
    "contactPhone": "51989098631",
    "packageName": null, "kitName": "Kit Toque de Romance",
    "total": 1968, "includesColonialBreakfast": true,
    "createdAt": "2026-10-01T14:32:00-03:00", "confirmedAt": null, "cancelledAt": null,
    "bufferBeforeNights": null, "bufferAfterNights": null,
    "source": "site"
  }],
  "page": 1, "pageSize": 20, "totalCount": 1
}
```
- `bufferBeforeNights` e `bufferAfterNights` só são preenchidos na confirmação (task 16).
- `source`: `site` (pré-reserva do site) ou `portal` (criada pelo admin, task 21).

### `GET /api/admin/reservations/{id}` → `ReservationDetailDto`
Tem tudo o que está na lista, mais:
```json
{
  "guest1": { "id": "g1...", "name": "Ana Souza", "cpf": "52998224725", "phone": "51989098631", "email": null },
  "guest2": { "id": "g2...", "name": "Bruno Lima", "cpf": null, "phone": null, "email": null },
  "contactEmail": null,
  "observations": "Aniversário de namoro",
  "adminNotes": "Pediu decoração extra",
  "packageId": null, "kitId": "8a1b...", "kitPrice": 179,
  "nightsTotal": 1789,
  "calculatedTotal": 1968, "priceAdjusted": false,
  "estimatedTotalFromSite": 1968, "siteTotalDiverged": false,
  "attribution": {
    "source": "meta_ads",
    "utmSource": "meta", "utmMedium": "paid", "utmCampaign": "inverno-2026", "utmContent": "video-banheira",
    "utmTerm": "casais-poa", "fbclid": "IwAR0abc123",
    "firstVisitAt": "2026-10-05T14:30:00+00:00"
  },
  "nightsBreakdown": [
    { "date": "2026-10-09", "kind": "weekend", "price": 894.5 },
    { "date": "2026-10-10", "kind": "weekend", "price": 894.5 }
  ]
}
```
`nightsBreakdown` é recalculado com o `BookingRules` a partir dos preços atuais e serve só de referência. Os valores que
valem são os gravados na reserva (snapshot).
- `calculatedTotal`: o total que o `BookingRules` calculou quando a reserva foi criada ou editada. `priceAdjusted` é
  `total != calculatedTotal` (desconto ou outro combinado do admin).
- `estimatedTotalFromSite` é `null` nas reservas criadas pelo portal, e aí `siteTotalDiverged` é `false`.
- `attribution` (origem da pré-reserva, UTM): `null` (ou ausente) quando a origem é desconhecida — reservas do portal sem `trafficSource`,
  pré-reservas antigas ou de navegador sem armazenamento. `source` é sempre um de `meta_ads`, `instagram`, `google`,
  `direct` ou `other`; os `utm*`, `fbclid` e `firstVisitAt` (primeira visita, UTC) podem ser `null`. Só existe no detalhe, não na lista.

### `POST /api/admin/reservations/{id}/confirm`
```json
{ "bufferBeforeNights": null, "bufferAfterNights": 1 }
```
- `null` (ou corpo vazio) = usar o valor global de `settings/buffer`. O valor efetivo é **gravado na reserva**.
- → `200 ReservationDetailDto`.
- `409` quando a reserva não está pendente ou quando as datas (com a folga) batem com outra reserva confirmada. O
  `detail` diz qual noite ou qual reserva está no caminho.

### `POST /api/admin/reservations/{id}/cancel`
Vale para reservas `pending` ou `confirmed` → `200 ReservationDetailDto` · `409` se a reserva já está cancelada.

### `PATCH /api/admin/reservations/{id}`
```json
{ "adminNotes": "texto interno, até 2000 caracteres" }
```
→ `200 ReservationDetailDto`. As observações do hóspede (`observations`) não são editáveis.

### `POST /api/admin/reservations` (🆕 task 21)
Reserva criada pelo admin, sem passar pela pré-reserva do site.
```json
{
  "status": "confirmed",
  "checkIn": "2026-10-09", "checkOut": "2026-10-11",
  "guest1": { "name": "Ana Souza", "cpf": "52998224725", "phone": "51989098631", "email": null },
  "guest2": { "name": "Bruno Lima", "cpf": null, "phone": null, "email": null },
  "kitId": "8a1b...",
  "total": 1800,
  "adminNotes": "Fechado pelo WhatsApp, desconto de aniversário",
  "trafficSource": "instagram"
}
```
- `status`: `pending` ou `confirmed`.
- `guest1`: `name`, `cpf` e `phone` obrigatórios, com as mesmas validações do site. `guest2` é opcional.
- `total`: opcional. `null` = o total calculado pelo `BookingRules`. Com valor (`>= 0`), vale o informado.
- `trafficSource` (🆕): opcional. De onde o hóspede veio, informado pelo admin (o código `(ref: IG)` que a landing põe na
  mensagem do WhatsApp). Um de `meta_ads`, `instagram`, `google`, `direct`, `other`, ou `null` = não sabe. Valor fora
  da lista → `400` com a chave `TrafficSource`. Grava a origem da reserva com `source` = esse valor e os `utm*` e
  `firstVisitAt` nulos. O detalhe devolve isso em `attribution`, ou `null` quando não foi informado.
- Só a disponibilidade é validada. Mínimo de diárias, pacote fechado e datas no passado ficam liberados.
- → `201 ReservationDetailDto` · `400` formato · `409` noite ocupada (o `detail` diz qual) · `422` sem preço cadastrado
  e sem `total` informado.

### `PUT /api/admin/reservations/{id}` (🆕 task 21)
Mesmo corpo do `POST`, **sem** `status`. Substitui tudo: datas, hóspedes, kit, total, notas e origem.
- `trafficSource` só vale para reservas com `source: "portal"`. Nas do site ele é **ignorado**: a origem veio da
  landing (UTM) e não muda.
- `total: null` **recalcula** o preço. Para manter um valor combinado, o portal manda o total que está na tela.
- → `200 ReservationDetailDto` · `404` · `409` se a reserva está cancelada ou se as novas datas batem com outra
  reserva confirmada.

---

## Hóspedes (🆕 task 17)

### `GET /api/admin/guests`

| Parâmetro | Valores |
|---|---|
| `search` | nome, telefone, e-mail ou CPF (só dígitos) |
| `sort` | `name` (padrão) \| `stays` \| `totalSpent` \| `lastStay` |
| `order` | `asc` \| `desc` |
| `page`, `pageSize` | padrão 1 e 20 |

→ `Paged<GuestListItemDto>`
```json
{ "items": [{ "id": "g1...", "name": "Ana Souza", "phone": "51989098631", "email": null,
  "stays": 2, "totalSpent": 3757, "lastStay": "2026-10-09", "nextStay": null }],
  "page": 1, "pageSize": 20, "totalCount": 1 }
```
- `stays` e `totalSpent` contam só reservas **confirmadas** em que a pessoa é hóspede 1 ou 2.
- Na lista, o CPF **não** aparece.

### `GET /api/admin/guests/{id}`
Mesmo formato do item da lista, mais:
- `cpf`;
- `notes`;
- `reservations`: um array de `ReservationListItemDto` com todas as reservas, qualquer status, da mais recente para a mais antiga.

### `PATCH /api/admin/guests/{id}`
```json
{ "name": "...", "phone": "51...", "email": null, "notes": "Prefere espumante moscatel" }
```
→ `200`. O CPF não é editável pelo portal nesta versão.

---

## Calendário (🆕 task 18)

### `GET /api/admin/calendar?from=2026-10-01&to=2026-10-31`
Traz tudo o que ocupa ou marca noites no intervalo (máximo de 100 dias). O portal monta a grade dia a dia.
```json
{
  "reservations": [{
    "id": "c1d2...", "status": "confirmed", "checkIn": "2026-10-09", "checkOut": "2026-10-11",
    "guestName": "Ana Souza", "total": 1968, "kitName": "Kit Toque de Romance", "packageName": null,
    "bufferBeforeNights": 0, "bufferAfterNights": 1
  }],
  "blocks": [{ "date": "2026-10-14", "reason": "Manutenção" }],
  "packages": [{ "id": "5f0c...", "name": "Feriadão de Finados", "checkIn": "2026-10-30", "checkOut": "2026-11-02", "price": 2490 }],
  "bufferNights": [{ "date": "2026-10-11", "reservationId": "c1d2..." }]
}
```
- `reservations` traz as reservas `confirmed` e `pending` (a pendente aparece tracejada no portal).
- `bufferNights` sai só para as reservas confirmadas.

---

## Dashboard (🆕 task 19)

### `GET /api/admin/dashboard?period=quarter&year=2026&index=4`
- `period` aceita `month`, `quarter`, `semester` ou `year`.
- `index` depende do período: mês 1–12, trimestre 1–4, semestre 1–2, e é ignorado no `year`.
- `previous` é o período imediatamente anterior, do mesmo tamanho, para a comparação ("+12% vs trimestre passado").
```json
{
  "period": { "kind": "quarter", "year": 2026, "index": 4, "from": "2026-10-01", "to": "2026-12-31", "label": "4º trimestre de 2026" },
  "current":  { "reservationRevenue": 15230, "confirmedReservations": 7, "nightsSold": 16, "availableNights": 90,
                "occupancyRate": 0.178, "averageTicket": 2175.71, "cashIncome": 14800, "cashExpense": 3120, "cashNet": 11680 },
  "previous": { "...mesmos campos..." : 0 },
  "series": [{ "label": "out", "from": "2026-10-01", "to": "2026-10-31", "reservationRevenue": 5230,
               "cashIncome": 5100, "cashExpense": 1040, "cashNet": 4060, "occupancyRate": 0.19 }],
  "expensesByCategory": [{ "categoryId": 5, "name": "Energia", "color": "#e8c97a", "total": 740 }],
  "topKits": [{ "name": "Kit Toque de Romance", "count": 3, "revenue": 537 }],
  "cash": {
    "topExpenses": [{ "id": "t9...", "date": "2026-10-05", "description": "Conta de luz", "categoryName": "Energia", "amount": 260 }],
    "pendingRecurring": [{ "id": 2, "description": "Internet", "amount": 120 }],
    "monthsWithoutBook": ["2026-11"],
    "negativeMonths": []
  },
  "upcoming": [{ "reservationId": "c1d2...", "guestName": "Ana Souza", "date": "2026-10-09", "type": "checkIn" }],
  "pendingCount": 2
}
```

Regras de cálculo, para o número bater com o que o dono espera:

**Faturamento e reservas**
- `reservationRevenue` soma o `total` das reservas **confirmadas** com **check-in dentro do período**. Uma estadia que
  atravessa a virada do mês conta inteira no mês do check-in.
- `nightsSold` conta as noites cobradas de reservas confirmadas que caem **dentro** do período, mesmo quando a
  estadia começa fora dele.
- `averageTicket` = `reservationRevenue ÷ confirmedReservations`. Com 0 reservas, vale 0.

**Ocupação**
- `availableNights` = dias do período − noites com bloqueio manual. A folga não desconta.
- `occupancyRate` = `nightsSold ÷ availableNights`, de 0 a 1.

**Caixa**
- `cashIncome`, `cashExpense` e `cashNet` vêm do **Livro Caixa**: a soma dos livros dos meses do período.
- `pendingRecurring` lista as despesas fixas ativas que ainda não foram lançadas no livro do **mês atual**.
- `monthsWithoutBook` lista os meses do período, até o mês atual, que ainda não têm livro.
- `negativeMonths` lista os meses do período com `net < 0`.

**Série e agenda**
- `series` tem um item por **dia** quando o período é `month` e um item por **mês** nos demais.
- `upcoming` traz os próximos 10 check-ins e check-outs de reservas confirmadas, a partir de hoje.

---

## Livro Caixa (🆕 tasks 14 e 15)
Um livro por mês. Ele registra **entradas** e **saídas**, e o resultado é o **caixa líquido**
(entradas − saídas) do mês. **Não existe saldo inicial nem saldo acumulado.** Regras completas em `REGRAS_CAIXA.md`.

### Livros
- `GET /api/admin/cashbooks?year=2026&status=open&sort=period&order=desc` → `CashBookSummaryDto[]`.
  - `sort` aceita `period`, `income`, `expense` ou `net`.
  - Sem `year`, vêm todos os livros.
- `GET /api/admin/cashbooks/year-summary?year=2026` → a visão anual, que traz **todos os 12 meses**, inclusive os que ainda não têm livro.
- `POST /api/admin/cashbooks` com `{ "year": 2026, "month": 10, "notes": null, "applyRecurring": true }` → `201 CashBookDetailDto`.
  Se o mês já tem livro → `409`.
- `GET /api/admin/cashbooks/{year}/{month}` → `CashBookDetailDto` · `404` quando o mês ainda não tem livro (o portal oferece criar).
- `PATCH /api/admin/cashbooks/{id}` com `{ "notes": "..." }` → `CashBookSummaryDto`.
- `POST /api/admin/cashbooks/{id}/close` e `POST /api/admin/cashbooks/{id}/reopen` → `CashBookSummaryDto` · `409` quando o livro já está nesse estado.
- `POST /api/admin/cashbooks/{id}/apply-recurring` → `{ "created": 3, "skipped": 2 }` · `409` quando o livro está fechado.
- `DELETE /api/admin/cashbooks/{id}` → `204` · `409` quando o livro tem transações.

```json
// CashBookSummaryDto
{ "id": 12, "year": 2026, "month": 10, "status": "open", "notes": null,
  "income": 1968, "expense": 670, "net": 1298, "transactionCount": 5, "closedAt": null }

// CashBookDetailDto = CashBookSummaryDto +
{ "transactions": [ /* CashTransactionDto, ordenadas por date desc, depois createdAt desc */ ],
  "byCategory": [{ "categoryId": 1, "name": "Hospedagem", "type": "income", "color": "#4a7a60", "total": 1789 }],
  "daily": [{ "date": "2026-10-09", "income": 1968, "expense": 0 }] }

// Visão anual
{ "year": 2026, "income": 21000, "expense": 6400, "net": 14600,
  "months": [{ "month": 1, "hasBook": false, "status": null, "income": 0, "expense": 0, "net": 0 }, "..."] }
```

### Transações
- `POST /api/admin/cashbooks/{id}/transactions` → `201 CashTransactionDto`.
- `PUT /api/admin/cash-transactions/{id}` → `200 CashTransactionDto`.
- `DELETE /api/admin/cash-transactions/{id}` → `204`.
- Com o livro fechado, as três rotas devolvem `409`.

```json
// SaveCashTransactionRequest
{ "date": "2026-10-05", "type": "expense", "amount": 260, "description": "Conta de luz",
  "categoryId": 5, "paymentMethod": "pix", "notes": null }

// CashTransactionDto
{ "id": "t9...", "cashBookId": 12, "date": "2026-10-05", "type": "expense", "amount": 260,
  "description": "Conta de luz", "categoryId": 5, "categoryName": "Energia", "categoryColor": "#e8c97a",
  "categoryIcon": "zap", "paymentMethod": "pix", "notes": null,
  "recurringExpenseId": 3,
  "createdAt": "2026-10-05T09:10:00-03:00", "updatedAt": "2026-10-05T09:10:00-03:00" }
```
- `type` aceita `income` (entrada) ou `expense` (saída).
- `paymentMethod` aceita `pix`, `cash`, `card`, `transfer` ou `other`.
- Filtro, busca e ordenação das transações de um mês são feitos **no portal**, porque o mês inteiro já vem no detalhe.

### Categorias
- `GET /api/admin/cash-categories?type=expense&includeInactive=false` → `CashCategoryDto[]`, ordenadas por `order`.
- `POST /api/admin/cash-categories` → `201`.
- `PUT /api/admin/cash-categories/{id}` → `200`.
- `DELETE /api/admin/cash-categories/{id}`:
  - `204` = apagada.
  - `200 { "deactivated": true, "message": "..." }` = já tinha transações, então foi só desativada (mesmo padrão dos kits).
```json
{ "id": 5, "name": "Energia", "type": "expense", "color": "#e8c97a", "icon": "zap", "isActive": true, "order": 4 }
```
- `color` é `#RRGGBB`.
- `icon` é o nome de um ícone do [Lucide](https://lucide.dev/icons) (ex.: `bed`, `gift`, `sparkles`, `wrench`,
  `zap`, `droplet`, `wifi`, `landmark`, `shopping-basket`, `megaphone`, `circle-ellipsis`).

### Despesas fixas
- `GET /api/admin/recurring-expenses` → `RecurringExpenseDto[]`.
- `POST /api/admin/recurring-expenses` → `201`.
- `PUT /api/admin/recurring-expenses/{id}` → `200`.
- `DELETE /api/admin/recurring-expenses/{id}` → `204`. As transações já lançadas **ficam** no livro, só perdem o vínculo.
```json
{ "id": 3, "description": "Conta de luz", "amount": 250, "categoryId": 5, "categoryName": "Energia",
  "dayOfMonth": 10, "paymentMethod": "pix", "isActive": true, "startMonth": "2026-10", "endMonth": null }
```

---

## Configurações

### 🆕 Folga entre hóspedes (task 16)
- `GET /api/admin/settings/buffer` → `{ "bufferBeforeNights": 0, "bufferAfterNights": 1 }`.
- `PUT /api/admin/settings/buffer` com o mesmo corpo → `200`. Cada valor vai de 0 a 3.
- "Check-in bloqueia a véspera" = `bufferBeforeNights`. "Check-out bloqueia a data" = `bufferAfterNights`.
- Mudar o valor global **não altera reservas já confirmadas**, porque cada uma guardou o seu valor na confirmação.

---

## Galeria e vídeos (✅ task 10)
Os formatos de leitura são os mesmos do site (`GalleryImageDto` e `TourVideoDto`). As URLs saem sempre absolutas
(montadas na leitura a partir de `Storage:PublicBaseUrl`; o banco guarda só a chave do arquivo).
- `GET /api/admin/gallery` → `GalleryImageDto[]` · `GET /api/admin/gallery/{id}` → `GalleryImageDto`.
- `POST /api/admin/gallery`: `multipart/form-data` com `file` e `alt` → `201 GalleryImageDto`.
  - Só JPEG, PNG ou WebP (conferido pela **assinatura do arquivo**), até 10 MB → senão `422`.
- `PUT /api/admin/gallery/{id}` com `{ "alt": "..." }` → `200 GalleryImageDto`.
- `PUT /api/admin/gallery/reorder` com `{ "ids": ["b3f1...", "a2c4..."] }` → `204`. Leva a lista completa, na nova ordem
  (faltou, sobrou ou repetiu id → `422`).
- `DELETE /api/admin/gallery/{id}` → `204` (apaga o registro e o arquivo).
- Vídeos:
  - `GET /api/admin/videos` → `TourVideoDto[]` · `GET /api/admin/videos/{id}` → `TourVideoDto`.
  - `POST /api/admin/videos` com JSON `{ title, description?, url, orientation? }` para um link do YouTube
    (`watch?v=`, `youtu.be/`, `shorts/`, `embed/`) → `201`.
  - `POST /api/admin/videos/upload` com `multipart/form-data`: `file` (mp4, até 100 MB) + `poster?` (imagem, até 10 MB)
    + `title`, `description?`, `orientation?` → `201`.
    - Rota separada (e não a mesma do JSON) porque o OpenAPI não aceita duas operações no mesmo método + caminho.
  - `PUT /api/admin/videos/{id}` com `{ title, description?, orientation, url? }` → `200`. `url` só vale para vídeo do
    YouTube; num mp4 enviado → `422`.
  - `PUT /api/admin/videos/reorder` reordena (mesmas regras da galeria) e `DELETE /api/admin/videos/{id}` remove
    (apaga o mp4 e a capa).
