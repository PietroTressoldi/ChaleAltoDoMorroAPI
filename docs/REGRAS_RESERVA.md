# Regras de reserva e preço

Estas regras já existem no site (`landing/src/lib/booking.ts` e `reservation.ts`, com testes). A API precisa
aplicar **exatamente as mesmas**, senão o site mostra um preço e o servidor grava outro.

A implementação fica numa classe **pura** em `Domain/` (sem EF, sem HTTP, sem `DateTime.Now`): recebe os dados
e "hoje" como parâmetros e devolve o resultado. Assim dá para testar tudo com xUnit, sem banco.

## Vocabulário
- **Diária = noite.** Check-in sex + check-out dom = 2 diárias (noites de sexta e sábado).
- **Noites da estadia** = do dia do check-in até o dia **anterior** ao check-out.
- **Noite bloqueada** = ninguém dorme nela. Dá para fazer **check-out** nesse dia (sai até as 12h).
- **Noite de fim de semana** = sexta ou sábado (`DayOfWeek.Friday`/`Saturday`).
- **Noite de pacote** = `pacote.CheckIn <= noite < pacote.CheckOut`.
- Horários (só informativos): check-in a partir das 15h, check-out até as 12h.

## Validação da estadia (nesta ordem; devolve o PRIMEIRO problema)
1. **Data inválida** → erro `InvalidDate` (não acontece com `DateOnly` já desserializado, mas trate `default`).
2. **Check-in antes de hoje** → `PastCheckIn`. Check-in **hoje** é permitido.
3. **Check-out <= check-in** → `CheckOutNotAfterCheckIn`.
4. **Alguma noite bloqueada** → `BlockedNight` (informar qual).
5. **Pacote parcial**: se alguma noite pertence a um pacote e a estadia **não é exatamente** aquele pacote
   (mesmo check-in **e** mesmo check-out) → `PartialPackage` (informar qual).
6. Se a estadia **é exatamente** um pacote → **válida** (o mínimo de fim de semana não se aplica).
7. **Mínimo de diárias**: se alguma noite é sexta ou sábado, mínimo = `MinNights` da regra `weekend`
   (padrão **2** se não houver regra ou o campo for nulo); senão mínimo = 1. Abaixo disso → `MinNights`.

## Preço
- **Pacote** (estadia = exatamente o pacote): total das diárias = `pacote.Price` (preço fechado).
- **Fora de pacote**, cada noite custa:
  - sexta/sábado → `pricePerNight` da regra `weekend`; se não existir, usa a `weekday`;
  - demais noites → `pricePerNight` da regra `weekday`;
  - sem regra `weekday` cadastrada → **não dá para orçar** (retorne erro/nulo, não invente valor).
- **Total** = diárias + preço do kit escolhido (**um kit por reserva**, decisão do dono; o site ainda permite
  vários e precisa ser ajustado). Arredondar para centavos (`decimal` já evita o problema
  de ponto flutuante do JavaScript).
- **Cesta de café colonial** inclusa (entregue na chegada) quando a estadia tem **2 ou mais diárias**.

## Hóspedes
- Hóspede 1: nome (mín. 2 caracteres, sem contar espaços nas pontas), CPF válido, telefone com 10 ou 11 dígitos.
  E-mail opcional; se informado, formato válido.
- Hóspede 2 (opcional): se tiver CPF, o CPF precisa ser válido **e** o nome é obrigatório.
- Observações: até 1000 caracteres.

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

---

# Casos de teste (portar para xUnit)

## Cenário de referência
"Hoje" = **quinta, 01/10/2026**.

- Preços: `weekday` = 790; `weekend` = 894,50 com `MinNights = 2`.
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
| sex → sáb (1 diária) | 2026-10-09 | 2026-10-10 | MinNights (exige 2, tem 1, motivo fim de semana) |
| sáb → dom (1 diária) | 2026-10-10 | 2026-10-11 | MinNights |
| qui → sex (noite de quinta) | 2026-10-08 | 2026-10-09 | ok |
| qui → sáb (quinta + sexta) | 2026-10-08 | 2026-10-10 | ok |
| dom → seg | 2026-10-11 | 2026-10-12 | ok |
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
| 2026-10-11 (dom) | weekday | 790 |
| 2026-10-12 (seg) | weekday | 790 |
| 2026-10-31 (noite do pacote) | package | 830 (2.490 ÷ 3, só referência) |
| 2026-10-09 sem regra `weekend` | weekday | 790 |
| 2026-10-06 sem nenhuma regra | — | sem preço (não inventa) |

## Orçamento (quote)
| Caso | Estadia | Kits | Esperado |
|---|---|---|---|
| fim de semana | 09/10 → 11/10 | — | 2 diárias `weekend` × 894,50 = **1.789**; total 1.789; **com** cesta de café; sem pacote |
| semana + fim de semana + kit | 08/10 → 11/10 | Toque de Romance (179) | diárias: 1 × weekday 790 + 2 × weekend 1.789 = **2.579**; kits 179; **total 2.758** |
| pacote | 30/10 → 02/11 | — | uma linha "pacote Finados, 3 diárias" = **2.490**; total 2.490; pacote = Finados |
| 1 diária | 06/10 → 07/10 | — | **sem** cesta de café |
| valores quebrados | 06/10 → 09/10 com weekday = 894,50 | — | 3 × 894,50 = **2.683,50** |
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

Confirmação (task 9) com o global 0/1: uma pendente P de 11/10 → 12/10, com R já confirmada (0/1) → `409` (P invade a
folga de R). Se R tivesse sido confirmada com `bufferAfterNights: 0` → ok.
