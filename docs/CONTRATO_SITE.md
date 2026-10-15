# Contrato da API com o site

Este é o formato que a **landing page já espera**. O site está pronto: quando `VITE_API_URL` aponta para esta
API, ele passa a ler estas rotas no lugar dos dados de exemplo. **Qualquer diferença de nome, tipo ou formato
quebra o site.**

> Fonte original (manter em sincronia): `D:\Desenvolvimento\ChaleAltoDoMorroLandingPage\docs\API.md` e
> `src/types/index.ts`. Se algo mudar aqui, mudar lá também.

## Convenções

| Assunto | Regra | No .NET |
|---|---|---|
| Nomes | **camelCase** | padrão do `System.Text.Json` no ASP.NET Core |
| Datas sem hora | `"AAAA-MM-DD"` | `DateOnly` (já serializa assim) |
| Dinheiro | número (`790`, `894.5`) | `decimal`; no Postgres `numeric(10,2)` |
| Enums | texto (`"weekday"`, `"portrait"`) | `JsonStringEnumConverter` com política camelCase |
| Campos opcionais | podem ser **omitidos** ou `null` | recomendado: `DefaultIgnoreCondition = WhenWritingNull` |
| URLs de imagem/vídeo | **absolutas** (`https://...`) | montar com a URL base da API/CDN |
| Listas | já ordenadas por `order` | `OrderBy(x => x.Order)` |
| Ids | string | `Guid` serializa como string, ok |
| Autenticação | rotas públicas **sem** auth | `[AllowAnonymous]` / sem `[Authorize]` |
| CORS | liberar o domínio do site (dev: `http://localhost:5173`) | `AddCors` + `UseCors` |
| Erros | status HTTP adequado. Se uma rota de leitura falhar, a seção correspondente some do site | ProblemDetails |

Todas as rotas de leitura devolvem um **array JSON direto** (sem envelope `{ data: ... }`).

---

## Rotas públicas de leitura

### `GET /api/pricing` — preço por tipo de diária
Sem preço de `weekday` o site não consegue orçar e mostra só o botão do WhatsApp.

```json
[
  { "kind": "weekday", "label": "Diária durante a semana", "pricePerNight": 790, "longStayPricePerNight": 645 },
  { "kind": "weekend", "label": "Fim de semana (sex, sáb e dom)", "pricePerNight": 894.5, "minNights": 2 },
  { "kind": "extended", "label": "Diária estendida (sex → sáb ou sáb → dom)", "pricePerNight": 1190 }
]
```

| Campo | Tipo | Obrig. | Observação |
|---|---|---|---|
| `kind` | `"weekday"` \| `"weekend"` \| 🆕 `"extended"` | sim | Fim de semana = noites de **sexta, sábado e domingo**. `extended` = diária estendida: 1 noite com entrada na sexta ou no sábado (preço fechado, sem cesta) |
| `label` | string | sim | Texto livre |
| `pricePerNight` | number | sim | Valor de cada diária. No portal, o fim de semana pode ser digitado como total ("R$ 1.789 pelas 2 diárias") e gravado como `total ÷ minNights`. No `extended`, é o preço fechado da estadia |
| `minNights` | number | não | Só no `weekend`. Mínimo de diárias quando a estadia tem noite de fim de semana. Padrão 2 |
| 🆕 `longStayPricePerNight` | number | não | Só no `weekday`. Valor de cada noite de semana quando a estadia tem 2 ou mais diárias. Ausente = sem desconto |

Regras que o site precisa repetir (`REGRAS_RESERVA.md`): **não** deixar check-in no domingo, e aceitar 1 diária
na sexta ou no sábado só quando existir o item `extended` e as noites vizinhas estiverem livres.

### 🆕 `GET /api/schedule` — horários (task 24)
```json
{ "checkInTime": "15:00:00", "checkOutTime": "12:00:00", "extendedCheckInTime": "10:30:00", "extendedCheckOutTime": "16:00:00" }
```
`TimeOnly` no formato padrão do `System.Text.Json` (`"HH:mm:ss"`). O site mostra "a partir das 15h" etc. É objeto, não array.

### 🆕 `GET /api/booking-settings` — mês aberto e folga global (tasks 27 e 28)
```json
{ "openUntil": "2026-12-31", "bufferBeforeNights": 0, "bufferAfterNights": 1 }
```

| Campo | Tipo | Observação |
|---|---|---|
| `openUntil` | `"AAAA-MM-DD"` ou `null` | Último dia do mês aberto para reservas. `null` = sem limite. O site desabilita as noites depois dele (o pacote vale pelo mês do check-in) |
| `bufferBeforeNights` | número (0–3) | Global "check-in bloqueia a véspera". Na estendida, a véspera só é checada se for `0` |
| `bufferAfterNights` | número (0–3) | Global "check-out bloqueia a data". Na estendida, a noite do dia da saída só é checada se for `0` |

É objeto, não array. Regras em `REGRAS_RESERVA.md` ("Meses abertos" e regra 8 da "Folga entre hóspedes").

### `GET /api/blocked-dates` — noites indisponíveis
Inclui bloqueios manuais **e** as noites das pré-reservas **confirmadas**.

```json
[{ "date": "2026-10-03", "reason": "Reservado" }]
```

| Campo | Tipo | Obrig. | Observação |
|---|---|---|---|
| `date` | `"AAAA-MM-DD"` | sim | A **noite** bloqueada: não dá para dormir nela, mas dá para fazer check-out nesse dia |
| `reason` | string | não | Não é exibido ao visitante. **Não colocar nome de hóspede aqui** (a rota é pública) |

Retornar só datas de **hoje em diante** (o site não precisa do passado).

### `GET /api/packages` — pacotes especiais
Normalmente um feriado emendado com o fim de semana. **Vendido fechado**: o visitante só reserva todas as
diárias, pelo preço total. No calendário, clicar numa noite do pacote seleciona o pacote inteiro.

```json
[
  {
    "id": "5f0c...",
    "name": "Feriadão de Finados",
    "description": "Três noites para emendar o feriado.",
    "checkIn": "2026-10-30",
    "checkOut": "2026-11-02",
    "price": 2490
  }
]
```

| Campo | Tipo | Obrig. | Observação |
|---|---|---|---|
| `id` | string | sim | |
| `name` | string | sim | |
| `description` | string | sim | Pode ser `""` |
| `checkIn` | `"AAAA-MM-DD"` | sim | Dia de entrada; a primeira diária é a noite deste dia |
| `checkOut` | `"AAAA-MM-DD"` | sim | Dia de saída; a última diária é a noite anterior (30/10 → 02/11 = 3 diárias) |
| `price` | number | sim | Preço **total** do pacote |

Pacotes não podem se sobrepor. Retornar só pacotes com `checkOut` depois de hoje.

### `GET /api/extras` — kits especiais
Aparecem no formulário, na seção "Torne a noite inesquecível".

```json
[
  {
    "id": "8a1b...",
    "name": "Kit Toque de Romance",
    "price": 179,
    "items": ["Espumante nacional Brut ou Moscatel (1 garrafa)", "Pétalas de rosa", "Velas decorativas", "Balões de coração"]
  }
]
```

| Campo | Tipo | Obrig. | Observação |
|---|---|---|---|
| `id` | string | sim | É o que volta em `extraIds` na pré-reserva |
| `name` | string | sim | |
| `price` | number | sim | |
| `description` | string | não | Texto curto |
| `items` | string[] | não | Lista do que vem no kit |

Só kits **ativos** (o portal pode desativar um kit sem apagar, pois pré-reservas antigas apontam para ele).

### `GET /api/gallery` — fotos da galeria

```json
[
  {
    "id": "b3f1...",
    "src": "https://api.exemplo.com/uploads/galeria/por-do-sol-1920.webp",
    "srcSet": "https://api.exemplo.com/uploads/galeria/por-do-sol-640.webp 640w, https://api.exemplo.com/uploads/galeria/por-do-sol-1920.webp 1920w",
    "alt": "Pôr do sol sobre a Lagoa dos Barros, com vinho no balde de gelo no deck",
    "order": 0
  }
]
```

| Campo | Tipo | Obrig. | Observação |
|---|---|---|---|
| `id` | string | sim | |
| `src` | string | sim | URL absoluta da maior versão |
| `srcSet` | string | não | Formato HTML `srcset`: `"url 640w, url 1280w"` |
| `alt` | string | sim | Descrição da cena (acessibilidade + legenda) |
| `order` | number | sim | As 8 primeiras aparecem no carrossel |

Lista vazia = a galeria some do site. **Nenhuma foto com hóspedes.**

### `GET /api/videos` — tour em vídeo

```json
[
  {
    "id": "a91e...",
    "title": "Do dia à noite no Alto do Morro",
    "description": "A chegada, a vista para a lagoa, a banheira...",
    "url": "https://www.youtube.com/watch?v=XXXXXXXXXXX",
    "posterUrl": "https://api.exemplo.com/uploads/videos/tour-capa.webp",
    "orientation": "portrait",
    "order": 0
  }
]
```

| Campo | Tipo | Obrig. | Observação |
|---|---|---|---|
| `id`, `title`, `url`, `order` | | sim | `url` = link do YouTube (watch, youtu.be, shorts) **ou** `.mp4` direto |
| `description` | string | não | |
| `posterUrl` | string | não | Capa. Sem capa e YouTube → o site usa a miniatura do YouTube |
| `orientation` | `"portrait"` \| `"landscape"` | não | Padrão `landscape`. Vídeo em pé = `portrait` |

Lista vazia = a seção "Tour pelo Chalé" some do site.

---

## Rota pública de escrita

### `POST /api/pre-reservations` — registrar pré-reserva
O site chama esta rota quando o visitante toca em "Enviar pré-reserva pelo WhatsApp". **O site não espera a
resposta nem mostra erro**: o WhatsApp é o canal principal. Mesmo assim, a API deve validar tudo e responder
certo, porque é daqui que o portal lista as pré-reservas pendentes.

Corpo enviado pelo site (exatamente assim):
```json
{
  "checkIn": "2026-10-09",
  "checkOut": "2026-10-11",
  "nights": 2,
  "packageId": null,
  "extraIds": ["8a1b..."],
  "estimatedTotal": 1968,
  "guest": { "name": "Ana Souza", "cpf": "52998224725", "email": null, "phone": "51989098631", "socialMedia": "@anasouza", "notes": "" },
  "secondGuest": { "name": "Bruno Lima", "cpf": null },
  "attribution": {
    "source": "meta_ads",
    "utmSource": "meta", "utmMedium": "paid", "utmCampaign": "inverno-2026", "utmContent": "video-banheira",
    "utmTerm": "casais-poa", "fbclid": "IwAR0abc123",
    "firstVisitAt": "2026-10-05T14:30:00.000Z"
  }
}
```

| Campo | Observação |
|---|---|
| `checkIn`, `checkOut` | Validar com as regras de `REGRAS_RESERVA.md` |
| `nights` | Informativo. Recalcular no servidor |
| `packageId` | Id do pacote ou `null`. Conferir: se a estadia é um pacote, tem que bater |
| `extraIds` | **No máximo 1 kit por reserva**: `[]` ou `["id"]` (com mais de um → `400`). O kit precisa existir e estar ativo. O nome continua no plural por compatibilidade com o site |
| `estimatedTotal` | **Só informativo. Nunca usar como preço.** O total gravado é o recalculado |
| `guest.name` | Obrigatório, mín. 2 caracteres |
| `guest.cpf` | Obrigatório, **só dígitos**, CPF válido (dígitos verificadores) |
| `guest.email` | Opcional (`null`). Se vier, precisa ser e-mail válido |
| `guest.phone` | Obrigatório, **só dígitos**, 10 ou 11 dígitos (DDD + número, sem +55) |
| `guest.notes` | Opcional, até 1000 caracteres (pode vir `""`) |
| 🆕 `guest.socialMedia` | Opcional (`null` ou ausente), até 150 caracteres (task 30). Ex.: `"@anasouza"`. Vazio vira `null`. Num hóspede que já existe, só preenche se estiver vazio |
| `secondGuest` | Opcional (`null`). Se vier: `name` obrigatório; `cpf` opcional, mas válido se vier |
| `attribution` | Opcional (ausente ou `null` = origem desconhecida). De onde o visitante veio, para medir os anúncios. **Nunca causa erro**: valor ruim é limpo, não recusado (`AttributionRules`) |
| `attribution.source` | `"meta_ads"` \| `"instagram"` \| `"google"` \| `"direct"` \| `"other"`. Qualquer outro valor é gravado como `"other"` |
| `attribution.utmSource`, `utmMedium`, `utmCampaign`, `utmContent`, `utmTerm` | Texto ou `null` (digitado à mão nos links dos anúncios). Sem espaços nas pontas, vazio vira `null`, cortado em 150 caracteres |
| `attribution.fbclid` | ID do clique da Meta (parâmetro `fbclid` que o Facebook/Instagram acrescenta ao link), ou `null`. Mesma limpeza, cortado em 500 caracteres |
| `attribution.firstVisitAt` | Data e hora da **primeira** visita (first-touch), ISO 8601 em UTC, ou `null`. Data que não dá para ler vira `null` (nunca `400`) |

Respostas sugeridas:
- `201 Created` com `{ "id": "...", "total": 1968, "status": "pending" }` e header `Location`.
- `400 Bad Request` (ProblemDetails de validação) para campos inválidos.
- `409 Conflict` quando as datas não estão mais disponíveis (bloqueio, pacote parcial...).
- 🆕 `422` com "As reservas para dezembro/2026 ainda não foram abertas." quando alguma noite cai depois do `openUntil`
  (task 28). Erros de regra da estadia (mínimo de diárias, check-in no domingo, data passada) também são `422`.

---

## Rotas do portal
Estão todas em **`docs/CONTRATO_PORTAL.md`**, que é o contrato do portal de gestão (React). Elas usam o prefixo
`/api/admin/...`, exigem JWT e não são usadas pelo site.
