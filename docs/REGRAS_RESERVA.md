# Regras de reserva e preço

Estas regras já existem no site (`landing/src/lib/booking.ts` e `reservation.ts`, com testes). A API precisa
aplicar **exatamente as mesmas**, senão o site mostra um preço e o servidor grava outro.

A implementação fica numa classe **pura** em `Domain/` (sem EF, sem HTTP, sem `DateTime.Now`): recebe os dados
e "hoje" como parâmetros e devolve o resultado. Assim dá para testar tudo com xUnit, sem banco.

## Vocabulário
- **Diária = noite.** Check-in sex + check-out dom = 2 diárias (noites de sexta e sábado).
- **Noites da estadia** = do dia do check-in até o dia **anterior** ao check-out.
- **Noite bloqueada** = ninguém dorme nela. Dá para fazer **check-out** nesse dia (sai até as 12h).
- **Noite de fim de semana** = sexta, sábado ou **domingo** (🆕 patch de preços, task 23).
- **Noite de pacote** = `pacote.CheckIn <= noite < pacote.CheckOut`.
- **Diária estendida** (🆕 task 23) = estadia de **1 noite** com check-in na **sexta ou no sábado** (sex → sáb ou
  sáb → dom). Preço fechado (padrão R$ 1.190), entrada às 10h30 e saída às 16h do dia seguinte. **Não** inclui a cesta de café.
- Horários (só informativos, 🆕 editáveis no portal pela task 24): check-in a partir das 15h e check-out até as 12h.
  Na diária estendida, a entrada é às 10h30 e a saída às 16h.

## Validação da estadia (nesta ordem; devolve o PRIMEIRO problema)
1. **Data inválida** → erro `InvalidDate` (não acontece com `DateOnly` já desserializado, mas trate `default`).
2. **Check-in antes de hoje** → `PastCheckIn`. Check-in **hoje** é permitido.
3. **Check-out <= check-in** → `CheckOutNotAfterCheckIn`.
4. **Alguma noite bloqueada** → `BlockedNight` (informar qual).
5. **Pacote parcial**: se alguma noite pertence a um pacote e a estadia **não é exatamente** aquele pacote
   (mesmo check-in **e** mesmo check-out) → `PartialPackage` (informar qual).
6. Se a estadia **é exatamente** um pacote → **válida** (o mínimo de fim de semana e o domingo não se aplicam).
   🆕 (task 28) Antes disso: se o **check-in do pacote** cai depois do mês aberto → `MonthNotOpen`.
6b. 🆕 (task 28) **Mês fechado**: se a última noite (`checkOut − 1`) cai depois do mês aberto (`OpenUntil`) →
   `MonthNotOpen` (informar a noite). O check-out pode cair no dia 1º do mês fechado. Ver "Meses abertos".
7. 🆕 **Check-in no domingo** → `SundayCheckIn`.
8. 🆕 Se a estadia é uma **diária estendida** (sex → sáb ou sáb → dom) e existe a regra `extended`: a noite **anterior**
   e a noite do **dia da saída** precisam estar livres (senão `BlockedNight` com a primeira delas). Se estiverem → **válida**.
   🆕 (task 27) A noite anterior só é checada se o global `BufferBeforeNights` é **0**, e a noite do dia da saída só se o
   global `BufferAfterNights` é **0**. Com o global ≥ 1, as noites publicadas já trazem essa folga (regra 4 da "Folga
   entre hóspedes"), e checar de novo contaria a folga duas vezes.
9. **Mínimo de diárias**: se alguma noite é de fim de semana (sex, sáb ou dom), o mínimo é o `MinNights` da regra
   `weekend` (padrão **2** se não houver regra ou o campo for nulo). Senão, o mínimo é 1. Abaixo disso → `MinNights`.

Essas regras valem para o **site**. O admin (task 21) continua validando só a disponibilidade: pode criar 1 diária na
sexta ou um check-in no domingo.

## Preço
- **Pacote** (estadia = exatamente o pacote): total das diárias = `pacote.Price` (preço fechado).
- 🆕 **Diária estendida** (sex → sáb ou sáb → dom, com a regra `extended` cadastrada, fora de pacote): total das diárias = `pricePerNight`
  da regra `extended` (preço fechado), **sem** cesta de café.
- **Fora de pacote**, cada noite custa:
  - sexta, sábado ou domingo → `pricePerNight` da regra `weekend`. Se ela não existir, vale a regra `weekday`;
  - demais noites → `pricePerNight` da regra `weekday`. 🆕 Se a estadia tem **2 ou mais diárias** e a regra `weekday`
    tem `longStayPricePerNight` (padrão 645), cada noite de semana custa esse valor. Isso vale mesmo quando a estadia
    mistura semana e fim de semana: qui → sáb = 645 + 894,50;
  - sem regra `weekday` cadastrada → **não dá para orçar** (retorne erro/nulo, não invente valor).
- O desconto **não** vale para as noites de fim de semana: 2 noites de fim de semana continuam custando 1.789.
- **Total** = diárias + preço do kit escolhido (**um kit por reserva**, decisão do dono; o site ainda permite
  vários e precisa ser ajustado). Arredondar para centavos (`decimal` já evita o problema
  de ponto flutuante do JavaScript).
- **Cesta de café colonial** inclusa (entregue na chegada) quando a estadia tem **2 ou mais diárias**. A diária
  estendida não inclui a cesta.

## Hóspedes
- Hóspede 1: nome (mín. 2 caracteres, sem contar espaços nas pontas), CPF válido, telefone com 10 ou 11 dígitos.
  E-mail opcional; se informado, formato válido.
- Hóspede 2 (opcional): se tiver CPF, o CPF precisa ser válido **e** o nome é obrigatório.
- Observações: até 1000 caracteres.
- 🆕 (task 30) Rede social do hóspede 1: opcional, até 150 caracteres. Num hóspede que já existe (mesmo CPF), só
  preenche se estiver vazia. O endereço continua **só** no portal.

### CPF — dígitos verificadores
Com os 9 primeiros dígitos `d0..d8`:
1. `soma1 = d0*10 + d1*9 + ... + d8*2`; `dv1 = (soma1 * 10) % 11`; se `dv1 == 10` → `0`. Tem que ser igual a `d9`.
2. `soma2 = d0*11 + d1*10 + ... + d9*2`; `dv2 = (soma2 * 10) % 11`; se `dv2 == 10` → `0`. Tem que ser igual a `d10`.
3. CPFs com os 11 dígitos iguais (`111.111.111-11`) passam na conta, mas são **inválidos**.

## Folga entre hóspedes (task 16)
A folga serve para limpeza, preparar a cesta e decorar. São duas configurações globais, cada uma de 0 a 3 noites:
- **"Check-in bloqueia a véspera"** (`BufferBeforeNights`, **B**): noites livres **antes** de cada reserva.
- **"Check-out bloqueia a data"** (`BufferAfterNights`, **A**): noites livres **depois** de cada reserva.

O padrão é 0/0, que é exatamente o comportamento de hoje.

**Regras**
1. **Na confirmação**, a reserva guarda os valores efetivos (os do pedido, ou os globais quando o pedido vem com `null`).
   Mudar a configuração global depois **não mexe** em reservas já confirmadas.
2. As noites **cobradas** continuam `[checkIn, checkOut)`. A folga **nunca** entra no preço nem em `Nights`.
3. **Duas estadias S e R não podem conviver** quando alguma delas invade a área protegida da outra. A área protegida
   de R vai de `R.checkIn − R.B` até `R.checkOut + R.A`, sem incluir o final. Em fórmula: há conflito se
   `[S.in − S.B, S.out + S.A) ∩ [R.in, R.out) ≠ ∅` **ou** `[R.in − R.B, R.out + R.A) ∩ [S.in, S.out) ≠ ∅`.
   - As folgas podem se sobrepor entre si: as duas são "casa vazia".
   - Uma estadia nova (pré-reserva) usa os valores **globais** atuais como `S.B` e `S.A`.
4. **Noites indisponíveis publicadas** (`GET /api/blocked-dates` e a validação da pré-reserva): para cada reserva
   confirmada R, com os globais atuais `gB` e `gA`, publique as noites
   `[R.in − max(R.B, gA), R.out + max(R.A, gB))`.
   - Assim o calendário do site, que só conhece noites bloqueadas, impede exatamente o que a regra 3 proíbe. A nova
     estadia precisa **terminar** a tempo de respeitar a folga de R e a própria folga depois (`max(R.B, gA)`), e
     **começar** depois da folga de R e da própria folga antes (`max(R.A, gB)`).
   - O `reason` continua sem sair na rota pública.
5. A folga vale **só entre reservas**. Bloqueios manuais e pacotes não ganham folga, e a folga de uma reserva pode cair
   sobre um bloqueio manual. Motivo: bloqueio é uma decisão manual do dono, e o pacote vira reserva quando é vendido.
6. Como a regra é pura, implemente as contas numa classe de domínio (ex.: `OccupancyRules.OccupiedNights(reserva, gB, gA)`)
   separada de `BookingRules.Nights`, que continua sendo o que se cobra.
7. 🆕 **Folga escolhida pelo admin** (task 26): ao criar uma reserva pelo portal (e ao editá-la), o admin pode informar
   `bufferBeforeNights` e `bufferAfterNights`, como já acontece na confirmação. `null` = usar o global. Uma reserva criada
   já `confirmed` grava o snapshot na hora. Ao editar uma reserva confirmada, a folga nova revalida a disponibilidade
   ignorando a própria reserva. Uma reserva pendente guarda a folga escolhida, e ela vira o padrão do formulário de confirmação.
8. 🆕 **Diária estendida exige as noites vizinhas livres** (decidido em 10/10/2026): a entrada às 10h30 bate com o check-out
   normal das 12h, e a saída às 16h bate com o check-in normal das 15h. Toda diária estendida (do site ou do portal) usa
   `B = max(B, 1)` e `A = max(A, 1)` nas contas de ocupação. O site recusa a estendida com `BlockedNight` quando a noite
   anterior ou a do dia da saída está indisponível (regra 8 da validação).
   🆕 **Correção (task 27, 14/10/2026):** no site e na pré-reserva, essa checagem só vale do lado em que o global é **0**.
   As noites publicadas (regra 4) já incluem a folga global da estadia nova (`gA` antes de cada reserva, `gB` depois).
   Com o global em 1, checar a noite vizinha de novo exigia **2** noites livres. Foi o bug do 19/12: Bianca 21 → 23,
   global 0/1, noite 20 publicada só por causa do `gA`, e a estendida 19 → 20 era recusada sem conflito real.
   A confirmação não muda: continua exata, com `Effective` + `Conflicts`.
   Limite conhecido: com o global 0, se a reserva vizinha tem folga **própria**, a noite publicada é a folga dela e a
   estendida continua recusada no site (conservador). O admin pode criar a reserva pelo portal.

## Meses abertos (task 28)
O site só aceita reservas até um **mês-limite**, aberto pelo portal ("reservas abertas até dezembro/2026").
1. Configuração global `BookingOpenUntil` (`null` = sem limite). Guarda o **último dia** do mês. Qualquer data recebida
   é normalizada para o fim do mês dela.
2. Estadia comum: todas as noites cobradas precisam estar até o `OpenUntil` → senão `MonthNotOpen`. O check-out pode cair
   no dia 1º do mês fechado (a noite anterior ainda é do mês aberto).
3. Pacote: vale o **mês do check-in** do pacote. Ano Novo 31/12 → 03/01 sai com dezembro aberto (decidido em 14/10/2026).
4. Vale **só para o site**. O admin cria reservas em qualquer mês.

---

## Reserva criada ou editada pelo portal (task 21)
1. O admin pode criar a reserva já `confirmed` ou `pending`.
2. Só vale a **disponibilidade**: nenhuma noite da estadia pode estar bloqueada nem ocupada por outra reserva
   confirmada (com a folga da task 16, quando existir). As outras regras de validação da estadia **não** valem para o admin.
3. `checkOut > checkIn` continua obrigatório.
4. Preço: `total` informado vale como está (desconto ou combinado). Sem `total`, usa o `Quote`. `CalculatedTotal` sempre
   guarda o `Quote`. Se não há preço cadastrado e o `total` não veio → `422`.
5. Uma estadia que toca um pacote só em parte é cobrada noite a noite pelo `PriceNight` (a noite do pacote vale o preço
   do pacote dividido pelas diárias). Se as datas são exatamente as do pacote, vale o preço fechado do pacote.
6. A cesta de café colonial segue a regra de sempre (a partir de 2 diárias).
7. Editar uma reserva cancelada → `409`. Editar as datas de uma confirmada revalida a disponibilidade **ignorando a
   própria reserva**.

# Casos de teste (portar para xUnit)

## Cenário de referência
"Hoje" = **quinta, 01/10/2026**.

- Preços: `weekday` = 790 com `longStayPricePerNight` = 645; `weekend` = 894,50 com `MinNights = 2`;
  🆕 `extended` = 1.190.
- Noites bloqueadas: **14/10/2026** (qua), **24/10/2026** (sáb), **14/11/2026** (sáb).
- Pacotes:
  - **"Finados"**: check-in **30/10/2026** (sex), check-out **02/11/2026** (seg), preço **2.490** (3 diárias).
  - **"Indisponível"**: check-in **13/11/2026** (sex), check-out **16/11/2026** (seg), preço 2.000.
    Tem a noite 14/11 bloqueada, então nunca pode ser reservado.

Calendário útil: out/2026 → sex 09, sáb 10, dom 11, seg 12, ter 13, qua 14 · sex 23, sáb 24 · sex 30.

## Validação — fim de semana
| Caso | Check-in | Check-out | Esperado |
|---|---|---|---|
| sex → dom | 2026-10-09 | 2026-10-11 | ok |
| sáb → seg | 2026-10-10 | 2026-10-12 | ok |
| sex → sáb sem regra `extended` | 2026-10-09 | 2026-10-10 | MinNights (exige 2, tem 1, motivo fim de semana) |
| 🆕 sáb → dom (diária estendida) | 2026-10-10 | 2026-10-11 | ok |
| 🆕 sex → sáb (diária estendida) | 2026-10-09 | 2026-10-10 | ok |
| 🆕 sex → sáb com a noite 08/10 bloqueada | 2026-10-09 | 2026-10-10 | BlockedNight (08/10) |
| 🆕 sáb → dom com a noite 11/10 bloqueada | 2026-10-10 | 2026-10-11 | BlockedNight (11/10) |
| 🆕 sáb → dom sem regra `extended` | 2026-10-10 | 2026-10-11 | MinNights |
| 🆕 sex → seg | 2026-10-09 | 2026-10-12 | ok |
| qui → sex (noite de quinta) | 2026-10-08 | 2026-10-09 | ok |
| qui → sáb (quinta + sexta) | 2026-10-08 | 2026-10-10 | ok |
| 🆕 dom → seg | 2026-10-11 | 2026-10-12 | SundayCheckIn |
| 🆕 dom → ter | 2026-10-11 | 2026-10-13 | SundayCheckIn |
| seg → ter | 2026-10-05 | 2026-10-06 | ok |

Mínimo configurável:
- Regra `weekend` com `MinNights = 3` → noite de sexta exige 3.
- Sem regra `weekend` → noite de sexta exige 2 (padrão).
- Noite de terça → exige 1.

## Validação — datas
| Caso | Check-in | Check-out | Esperado |
|---|---|---|---|
| check-in ontem | 2026-09-30 | 2026-10-02 | PastCheckIn |
| check-in hoje | 2026-10-01 | 2026-10-02 | ok |
| check-out = check-in | 2026-10-06 | 2026-10-06 | CheckOutNotAfterCheckIn |
| check-out antes | 2026-10-06 | 2026-10-05 | CheckOutNotAfterCheckIn |

## Validação — noites bloqueadas
| Caso | Check-in | Check-out | Esperado |
|---|---|---|---|
| dorme na noite bloqueada | 2026-10-13 | 2026-10-15 | BlockedNight (2026-10-14) |
| entra na noite bloqueada | 2026-10-14 | 2026-10-15 | BlockedNight |
| sai no dia bloqueado | 2026-10-13 | 2026-10-14 | ok |

## Validação — pacote vendido fechado
| Caso | Check-in | Check-out | Esperado |
|---|---|---|---|
| pacote inteiro | 2026-10-30 | 2026-11-02 | ok |
| só parte do pacote | 2026-10-30 | 2026-11-01 | PartialPackage (Finados) |
| começando no meio | 2026-10-31 | 2026-11-02 | PartialPackage |
| emendando antes | 2026-10-29 | 2026-11-02 | PartialPackage |
| emendando depois | 2026-10-30 | 2026-11-03 | PartialPackage |
| sai no dia em que o pacote começa | 2026-10-28 | 2026-10-30 | ok |
| entra no dia em que o pacote termina | 2026-11-02 | 2026-11-03 | ok |
| pacote com noite bloqueada | 2026-11-13 | 2026-11-16 | BlockedNight |
| pacote que já começou (hoje = 31/10/2026) | 2026-10-30 | 2026-11-02 | PastCheckIn |

## Preço de cada noite
| Noite | Tipo | Preço |
|---|---|---|
| 2026-10-06 (ter) | weekday | 790 |
| 2026-10-09 (sex) | weekend | 894,50 |
| 2026-10-10 (sáb) | weekend | 894,50 |
| 2026-10-11 (dom) | 🆕 weekend | 894,50 |
| 2026-10-12 (seg) | weekday | 790 (645 numa estadia de 2+ diárias) |
| 2026-10-31 (noite do pacote) | package | 830 (2.490 ÷ 3, só referência) |
| 2026-10-09 sem regra `weekend` | weekday | 790 |
| 2026-10-06 sem nenhuma regra | — | sem preço (não inventa) |

## Orçamento (quote)
| Caso | Estadia | Kits | Esperado |
|---|---|---|---|
| fim de semana | 09/10 → 11/10 | — | 2 diárias `weekend` × 894,50 = **1.789**; total 1.789; **com** cesta de café; sem pacote |
| 🆕 sáb → seg | 10/10 → 12/10 | — | 2 × weekend 894,50 = **1.789**; com cesta |
| 🆕 sex → seg | 09/10 → 12/10 | — | 3 × weekend 894,50 = **2.683,50**; com cesta |
| 🆕 diária estendida | 10/10 → 11/10 ou 09/10 → 10/10 | — | uma linha "diária estendida" = **1.190**; **sem** cesta |
| semana + fim de semana + kit | 08/10 → 11/10 | Toque de Romance (179) | 🆕 diárias: 1 × weekday **645** + 2 × weekend 1.789 = **2.434**; kits 179; **total 2.613** |
| 🆕 qui → sáb | 08/10 → 10/10 | — | 645 + 894,50 = **1.539,50**; com cesta |
| pacote | 30/10 → 02/11 | — | uma linha "pacote Finados, 3 diárias" = **2.490**; total 2.490; pacote = Finados |
| 1 diária | 06/10 → 07/10 | — | **790**, **sem** cesta de café |
| 🆕 2 diárias na semana | 05/10 → 07/10 | — | 2 × 645 = **1.290**; com cesta |
| 🆕 2 diárias sem `longStayPricePerNight` | 05/10 → 07/10 | — | 2 × 790 = **1.580** |
| valores quebrados | 06/10 → 09/10 com weekday = 894,50 e sem `longStayPricePerNight` | — | 3 × 894,50 = **2.683,50** |
| sem preços cadastrados | 06/10 → 08/10 | — | sem orçamento |

## CPF
| Entrada | Válido? |
|---|---|
| `529.982.247-25` | sim |
| `52998224725` | sim |
| `111.444.777-35` | sim |
| `529.982.247-24` | não (último dígito errado) |
| `111.111.111-11` | não (todos iguais) |
| `529.982.247` | não (incompleto) |

## Hóspedes
| Caso | Esperado |
|---|---|
| nome "Ana Souza", CPF 52998224725, telefone 51989098631, resto vazio | válido |
| nome " ", CPF "123", telefone "9890" | erros em nome, CPF e telefone |
| e-mail vazio / `ana@exemplo.com` / `ana@` | ok / ok / erro |
| hóspede 2 só com nome | válido |
| hóspede 2 com nome + CPF `111.444.777-35` | válido |
| hóspede 2 com nome + CPF `111.444.777-00` | erro no CPF do hóspede 2 |
| hóspede 2 só com CPF válido, sem nome | erro no nome do hóspede 2 |
| telefone `+55 (51) 98909-8631` | o site já manda só dígitos (`51989098631`); a API valida 10–11 dígitos |

## Folga entre hóspedes
Mesmo cenário de referência ("hoje" = 01/10/2026). Reserva confirmada **R**: 09/10 → 11/10 (noites 09 e 10).

| Caso | Folga de R (B/A) | Globais (gB/gA) | Noites publicadas por R | Estadia nova | Esperado |
|---|---|---|---|---|---|
| sem folga (comportamento atual) | 0/0 | 0/0 | 09, 10 | 11/10 → 12/10 | ok (entra no dia em que R sai) |
| sem folga | 0/0 | 0/0 | 09, 10 | 07/10 → 09/10 | ok (sai no dia em que R entra) |
| check-out bloqueia a data | 0/1 | 0/1 | 08, 09, 10, 11 | 11/10 → 12/10 | BlockedNight (11/10) |
| check-out bloqueia a data | 0/1 | 0/1 | 08, 09, 10, 11 | 12/10 → 13/10 | ok |
| check-out bloqueia a data, nova antes de R | 0/1 | 0/1 | 08, 09, 10, 11 | 07/10 → 09/10 | BlockedNight (08/10): a própria folga depois da nova cairia na noite 09 de R |
| check-out bloqueia a data, nova antes de R | 0/1 | 0/1 | 08, 09, 10, 11 | 07/10 → 08/10 | ok |
| check-in bloqueia a véspera | 1/0 | 1/0 | 08, 09, 10, 11 | 07/10 → 09/10 | BlockedNight (08/10) |
| check-in bloqueia a véspera | 1/0 | 1/0 | 08, 09, 10, 11 | 12/10 → 13/10 | ok |
| R confirmada antes com folga, global mudou para 0/0 | 0/1 | 0/0 | 09, 10, 11 | 11/10 → 12/10 | BlockedNight (11/10): vale o snapshot de R |
| global passou para 0/2 depois de R (0/0) | 0/0 | 0/2 | 07, 08, 09, 10 | 07/10 → 08/10 | BlockedNight (07/10): a folga de 2 noites da nova invadiria R |
| folga sobre bloqueio manual | 0/1 | 0/1 | 08, 09, 10, 11 | — | confirmar R com bloqueio manual em 11/10 → ok (regra 5) |
| preço de R com folga 0/1 | 0/1 | — | — | — | continua **2 diárias**, 1.789 (a folga não é cobrada) |

🆕 Diária estendida (regra 8), globais 0/0:

| Caso | Esperado |
|---|---|
| R estendida 10/10 → 11/10 com folga 0/0 confirmada | publica as noites **09**, 10 e **11** |
| estendida nova 10/10 → 11/10, com a noite 09/10 bloqueada (reserva ou bloqueio manual) | BlockedNight (09/10) |
| estendida nova 10/10 → 11/10, com a noite 08/10 ocupada (qui → sex) | ok |
| confirmar a estendida com outra confirmada 08/10 → 10/10 (sai no sábado) | 409: a sexta da estendida está ocupada |

🆕 Diária estendida com a folga global (task 27). R confirmada **12/10 → 14/10** (seg → qua), folga própria 0/0:

| Globais (gB/gA) | Noites publicadas por R | Estadia nova | Esperado |
|---|---|---|---|
| 0/1 | **11**, 12, 13 | sáb 10/10 → dom 11/10 (estendida) | ok: a noite 11 é só a folga da própria estendida (caso 19/12) |
| 0/0 | 12, 13 + bloqueio manual em 11 | sáb 10/10 → dom 11/10 (estendida) | BlockedNight (11/10): comportamento de antes |
| 1/0 | R2 confirmada 07/10 → 09/10 (qua → sex) publica 07, 08, **09** | sex 09/10 → sáb 10/10 (estendida) | BlockedNight (09/10): é a própria noite da estadia |
| 1/0 | R2 confirmada 06/10 → 08/10 (ter → qui) publica 06, 07, **08** | sex 09/10 → sáb 10/10 (estendida) | ok: a quinta 08 já é a folga publicada |
| 0/0 | bloqueio manual em 08/10 | sex 09/10 → sáb 10/10 (estendida) | BlockedNight (08/10): comportamento de antes |

## Meses abertos
Mesmo cenário, com `OpenUntil = 31/12/2026` e os pacotes **"Ano Novo"** (31/12 → 03/01, 2.990) e **"Reis"**
(08/01/2027 → 10/01/2027). As estadias **comuns** da tabela são testadas **sem** os pacotes (com o Ano Novo, 30/12 → 01/01
tocaria a noite 31/12 dele e daria `PartialPackage`, que vem antes do `MonthNotOpen`):

| Estadia | Esperado |
|---|---|
| 30/12 → 01/01 | ok (última noite 31/12) |
| 31/12 → 02/01 | MonthNotOpen (01/01) |
| 05/01 → 07/01 | MonthNotOpen (05/01) |
| pacote Ano Novo 31/12 → 03/01 | ok: o check-in é em dezembro |
| pacote Reis 08/01 → 10/01 | MonthNotOpen (08/01) |
| `OpenUntil = null`, 05/01 → 07/01 | ok |
| admin cria 05/01 → 07/01 | ok (o admin não tem limite) |
| `PUT` com 15/01/2027 | `OpenUntil` = 31/01/2027 |

Confirmação (task 9) com o global 0/1: uma pendente P de 11/10 → 12/10, com R já confirmada (0/1) → `409` (P invade a
folga de R). Se R tivesse sido confirmada com `bufferAfterNights: 0` → ok.

## Reserva criada ou editada pelo portal (task 21)
Com o cenário de referência (preços 790 / 894,50, 14/10 e 24/10 bloqueadas, Finados de 30/10 a 02/11 por 2.490).

| Caso | Entrada | Esperado |
|---|---|---|
| sexta → sábado (1 diária) | 09/10 → 10/10, sem `total` | ok, 🆕 diária estendida 1.190, sem cesta |
| 🆕 check-in no domingo | 11/10 → 12/10, sem `total` | ok (o admin pode), total 894,50 |
| sobre noite bloqueada | 13/10 → 15/10 | 409 (14/10) |
| sobre reserva confirmada | outra confirmada em 09/10 → 11/10; nova 10/10 → 12/10 | 409 (10/10) |
| datas no passado | 20/09 → 22/09 | ok (registro de reserva antiga) |
| pacote em parte | 31/10 → 01/11 | ok, 830 (2.490 / 3) |
| pacote inteiro | 30/10 → 02/11 | ok, 2.490 |
| desconto | 09/10 → 11/10, `total` 1.600 | total 1.600, calculado 1.789, `priceAdjusted` |
| sem preço cadastrado | sem regra weekday, sem `total` | 422 |
| sem preço cadastrado, com total | sem regra weekday, `total` 700 | ok, 700 |
| editar cancelada | qualquer | 409 |
| editar confirmada 09→11 para 10→12 | sem outras reservas | ok (ignora a si mesma) |
