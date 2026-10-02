using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// PUT /reorder da galeria e dos vídeos: a lista tem que ter exatamente os ids do banco, cada um uma vez.
public class MediaOrderTests
{
    private static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), C = Guid.NewGuid();
    private static readonly List<Guid> Existing = [A, B, C];

    [Fact]
    public void Mesmos_ids_em_outra_ordem_e_valido() =>
        Assert.True(MediaOrder.IsCompleteList(Existing, [C, A, B]));

    [Fact]
    public void Mesma_ordem_de_antes_e_valido() =>
        Assert.True(MediaOrder.IsCompleteList(Existing, [A, B, C]));

    [Fact]
    public void Faltando_um_id_e_invalido() =>
        Assert.False(MediaOrder.IsCompleteList(Existing, [C, A]));

    [Fact]
    public void Id_repetido_e_invalido() =>
        Assert.False(MediaOrder.IsCompleteList(Existing, [A, A, B]));

    [Fact]
    public void Id_que_nao_existe_e_invalido() =>
        Assert.False(MediaOrder.IsCompleteList(Existing, [A, B, Guid.NewGuid()]));

    [Fact]
    public void Id_a_mais_e_invalido() =>
        Assert.False(MediaOrder.IsCompleteList(Existing, [A, B, C, Guid.NewGuid()]));

    [Fact]
    public void Lista_vazia_com_nada_cadastrado_e_valido() =>
        Assert.True(MediaOrder.IsCompleteList([], []));

    [Fact]
    public void Lista_vazia_com_itens_cadastrados_e_invalido() =>
        Assert.False(MediaOrder.IsCompleteList(Existing, []));
}
