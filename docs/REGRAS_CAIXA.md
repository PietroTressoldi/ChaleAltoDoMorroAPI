# Regras do Livro Caixa

O Livro Caixa é o registro do dinheiro que **entrou** e **saiu** em cada mês. O portal mostra, lança, edita e
exporta; a API guarda e **garante as regras abaixo**, sem confiar no navegador.

Assim como o `BookingRules`, as contas (resumo, caixa líquido, data da despesa fixa) ficam numa classe **pura**
(ex.: `Domain/CashBookRules`), sem EF e sem relógio. Isso permite testar tudo com xUnit usando as tabelas daqui.

## Vocabulário
- **Livro (do mês)**: um por mês (`Year` + `Month`, **únicos juntos**). Estado `Open` (aberto) ou `Closed` (fechado).
- **Transação**: uma movimentação do mês, com `Type` `Income` (entrada) ou `Expense` (saída) e `Amount` **sempre positivo**.
  O sinal vem do tipo.
- **Caixa líquido do mês** = soma das entradas − soma das saídas. Pode ser negativo.
  **Não existe saldo inicial nem saldo acumulado entre meses** (decisão do dono). Cada mês é independente.
- **Categoria**: agrupa transações (Hospedagem, Energia...). Cada uma tem um tipo (`Income` ou `Expense`), além de cor e ícone.
- **Despesa fixa** (`RecurringExpense`): um modelo que gera uma saída em todo mês em que está ativa, no dia `DayOfMonth`.
- **Forma de pagamento**: `Pix`, `Cash`, `Card`, `Transfer` ou `Other`.

## Regras

### Livro
1. Só existe **um livro por mês**. Criar o mesmo `Year`+`Month` de novo → `409`.
2. `Month` vai de 1 a 12. `Year` vai de 2020 a 2100.
3. Ao criar o livro com `applyRecurring = true`, as despesas fixas do mês já são lançadas (regra 12).
4. **Livro fechado é somente leitura.** Criar, editar ou apagar transação, ou aplicar despesas fixas, → `409`
   ("O livro de outubro/2026 está fechado. Reabra para editar.").
5. Fechar um livro já fechado, ou reabrir um livro aberto → `409`.
6. Apagar um livro só é possível **quando ele não tem transações** → `409` caso contrário.

### Transação
7. `Date` precisa estar **dentro do mês do livro** → `422` caso contrário.
8. `Amount > 0`, com no máximo 2 casas decimais e até 9.999.999,99 → `400` caso contrário.
9. `Description` é obrigatória, com 1 a 200 caracteres. `Notes` é opcional, até 500.
10. A categoria precisa existir, estar **ativa** e ter o **mesmo tipo** da transação → `422` caso contrário.
    Editar uma transação antiga cuja categoria **foi desativada depois** é permitido, desde que ela continue com a mesma
    categoria.
11. Mudar a data de uma transação para outro mês **não** é permitido nesta versão (`422`). Para isso, apague e lance no outro mês.

### Despesas fixas
12. **Aplicar despesas fixas** no livro do mês `M` lança uma saída para cada despesa fixa que:
    - está ativa;
    - tem `StartMonth <= M`;
    - tem `EndMonth` nulo ou `EndMonth >= M`;
    - **ainda não tem** transação nesse livro com o mesmo `RecurringExpenseId`.

    A transação lançada tem:
    - **data**: dia `DayOfMonth` do mês. Se o mês for mais curto, usa o **último dia** (31 em fevereiro vira 28 ou 29);
    - **valor, descrição, categoria e forma de pagamento**: os mesmos do modelo;
    - **`RecurringExpenseId`**: o id da despesa fixa.
13. A aplicação é **idempotente**: aplicar duas vezes não duplica nada. A resposta informa `created` e `skipped`.
    Para o índice único, use `(CashBookId, RecurringExpenseId)` filtrado por `RecurringExpenseId IS NOT NULL`.
14. Depois de lançada, a transação é **independente**: dá para editar o valor (a conta de luz varia) sem mudar o modelo.
    Mudar o modelo também **não** altera os meses já lançados.
15. Se o dono **apagar** uma transação gerada e aplicar de novo, ela **volta**. Isso é esperado: o portal avisa antes de aplicar.
16. Apagar uma despesa fixa **não apaga** as transações já lançadas. Elas ficam com `RecurringExpenseId = null`
    (`OnDelete(SetNull)`).

### Categorias
17. Nome único por tipo (sem diferenciar maiúsculas). `Color` no formato `#RRGGBB`. `Icon` com até 50 caracteres.
18. Apagar uma categoria **com transações ou despesas fixas** → só **desativa** (mesmo padrão dos kits).
    Categoria desativada não aparece para novos lançamentos.
19. **Seed** (em todos os ambientes, só se a tabela estiver vazia):

    | Tipo | Nome | Ícone | Cor |
    |---|---|---|---|
    | Entrada | Hospedagem | `bed` | `#4a7a60` |
    | Entrada | Kits e extras | `gift` | `#c9a84c` |
    | Entrada | Outras entradas | `circle-plus` | `#2a4436` |
    | Saída | Limpeza | `sparkles` | `#7c9a8a` |
    | Saída | Manutenção | `wrench` | `#a0714f` |
    | Saída | Energia | `zap` | `#e8c97a` |
    | Saída | Água | `droplet` | `#5b8fa8` |
    | Saída | Internet | `wifi` | `#6b7fa8` |
    | Saída | Impostos e taxas | `landmark` | `#8a6d8f` |
    | Saída | Produtos e insumos | `shopping-basket` | `#b5838d` |
    | Saída | Marketing | `megaphone` | `#c47a5a` |
    | Saída | Outras saídas | `circle-ellipsis` | `#7c7268` |

### Lançar reserva no caixa (`POST /api/admin/reservations/{id}/cash-entry`)
20. Só vale para reserva **confirmada** → `409` caso contrário.
21. Uma reserva só pode ser lançada **uma vez**, enquanto a transação existir → `409` caso contrário.
    Para isso, use um índice único em `ReservationId`, filtrado por não nulo.
22. Os valores padrão da entrada lançada:
    - **Valor**: o `Total` da reserva.
    - **Data**: o check-in (ou a `date` enviada).
    - **Descrição**: `"Reserva — {nome do hóspede 1} ({dd/MM}–{dd/MM})"`.
    - **Categoria**: "Hospedagem".
    - **Forma de pagamento**: a enviada.
23. Se o mês da data não tem livro, ele é **criado** (sem aplicar despesas fixas). Se o livro do mês está fechado → `409`.
24. Cancelar uma reserva já lançada **não apaga** a transação: dinheiro recebido é fato. O portal mostra um aviso
    ("Esta reserva tem uma entrada no caixa") para o dono decidir se apaga ou lança um estorno.

### Resumo e visão anual
25. O **resumo do livro** traz:
    - `income`, a soma das entradas;
    - `expense`, a soma das saídas;
    - `net` = `income − expense`;
    - `byCategory`, com o total de cada categoria usada no mês;
    - `daily`, com um item por dia **que teve movimento**, ordenado.
26. A **visão anual** devolve os 12 meses. Meses sem livro vêm com `hasBook = false` e zeros. Os totais do ano somam os 12.
27. Todo o dinheiro é `decimal` com `numeric(12,2)`. Nada de `double`.

---

# Casos de teste (portar para xUnit)

## Cenário de referência
Livro **outubro/2026**, aberto:

| Data | Tipo | Categoria | Descrição | Valor |
|---|---|---|---|---|
| 2026-10-05 | Saída | Energia | Conta de luz | 250,00 |
| 2026-10-09 | Entrada | Hospedagem | Reserva — Ana Souza (09/10–11/10) | 1.789,00 |
| 2026-10-09 | Entrada | Kits e extras | Kit Toque de Romance | 179,00 |
| 2026-10-10 | Saída | Internet | Internet | 120,00 |
| 2026-10-12 | Saída | Limpeza | Faxina pós-estadia | 300,00 |

## Resumo
| Caso | Esperado |
|---|---|
| cenário de referência | income **1.968,00** · expense **670,00** · net **1.298,00** |
| `byCategory` | Hospedagem 1.789 · Kits e extras 179 · Energia 250 · Internet 120 · Limpeza 300 |
| `daily` | 05/10 (0 / 250) · 09/10 (1.968 / 0) · 10/10 (0 / 120) · 12/10 (0 / 300), só dias com movimento |
| só saídas: 250 + 120 | income 0 · expense 370 · net **−370,00** |
| livro vazio | tudo 0, `byCategory` e `daily` vazios |
| valores quebrados: 0,10 + 0,20 de entrada | income **0,30** exato |

## Transação
| Caso | Esperado |
|---|---|
| data 2026-10-31 no livro de outubro | ok |
| data 2026-11-01 no livro de outubro | 422 (fora do mês) |
| valor 0 ou −10 | 400 |
| valor 10,005 | 400 (mais de 2 casas) |
| saída com categoria "Hospedagem" (que é de entrada) | 422 |
| categoria desativada em lançamento novo | 422 |
| criar transação em livro fechado | 409 |
| editar ou apagar transação em livro fechado | 409 |
| reabrir e editar | ok |

## Livro
| Caso | Esperado |
|---|---|
| criar 2026-10 de novo | 409 |
| mês 13 ou 0 | 400 |
| apagar livro com 1 transação | 409 |
| apagar livro vazio | 204 |
| fechar livro fechado / reabrir livro aberto | 409 |

## Data da despesa fixa (`DayOfMonth` → data no mês)
| DayOfMonth | Mês | Data |
|---|---|---|
| 10 | 2026-10 | 2026-10-10 |
| 31 | 2026-10 | 2026-10-31 |
| 31 | 2026-11 | 2026-11-30 |
| 31 | 2027-02 | 2027-02-28 |
| 30 | 2028-02 | 2028-02-29 (bissexto) |
| 1 | 2026-12 | 2026-12-01 |

## Aplicar despesas fixas
Modelos: Luz (ativa, desde 2026-10, sem fim) · Internet (ativa, desde 2026-11) · Seguro (ativa, 2026-01 a 2026-09) ·
Jardinagem (inativa).

| Caso | Esperado |
|---|---|
| aplicar em 2026-10 | cria só **Luz** · `created 1, skipped 0` |
| aplicar em 2026-10 de novo | `created 0, skipped 1` (idempotente) |
| aplicar em 2026-11 | cria Luz e Internet |
| aplicar em 2026-09 | cria só Seguro (Luz ainda não começou) |
| apagar a Luz lançada em 2026-10 e aplicar de novo | a Luz volta (`created 1`) |
| editar o valor da Luz lançada (250 → 262,40) | ok; o modelo continua 250 |
| aplicar em livro fechado | 409 |
| criar o livro 2026-12 com `applyRecurring = false` | livro vazio |

## Lançar reserva no caixa
| Caso | Esperado |
|---|---|
| reserva confirmada de 1.968, check-in 2026-10-09 | entrada 1.968 em 09/10 no livro de outubro, categoria Hospedagem, `reservationId` preenchido |
| lançar a mesma reserva de novo | 409 |
| reserva pendente ou cancelada | 409 |
| mês sem livro | livro criado sem despesas fixas, e a entrada lançada |
| livro do mês fechado | 409 |
| cancelar reserva lançada | a transação continua; o detalhe da reserva continua com `cashTransactionId` |
