using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ChaleAltoDoMorroAPI.Dtos;

namespace ChaleAltoDoMorroAPI.Tests.Dtos;

// Tabela "Hóspedes" de docs/REGRAS_RESERVA.md.
// O ASP.NET valida os objetos aninhados sozinho; o Validator usado aqui não, então cada DTO é validado direto.
public class PreReservationValidationTests
{
    // Devolve os nomes dos campos com erro (vazio = válido). Mesma ordem do ASP.NET: atributos, depois IValidatableObject.
    private static string[] InvalidFields(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames).Distinct().Order().ToArray();
    }

    private static GuestDto ValidGuest(string? email = null, string phone = "51989098631") =>
        new() { Name = "Ana Souza", Cpf = "52998224725", Phone = phone, Email = email, Notes = "" };

    [Fact]
    public void Hospede_valido()
    {
        Assert.Empty(InvalidFields(ValidGuest()));
    }

    [Fact]
    public void Hospede_com_nome_cpf_e_telefone_invalidos()
    {
        var guest = new GuestDto { Name = " ", Cpf = "123", Phone = "9890" };

        Assert.Equal(["Cpf", "Name", "Phone"], InvalidFields(guest));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("ana@exemplo.com", true)]
    [InlineData("ana@", false)]
    public void Email_eh_opcional_mas_precisa_ser_valido(string? email, bool valid)
    {
        Assert.Equal(valid, InvalidFields(ValidGuest(email: email)).Length == 0);
    }

    [Theory]
    [InlineData("5198909863", true)]           // 10 dígitos (fixo)
    [InlineData("51989098631", true)]          // 11 dígitos (celular)
    [InlineData("989098631", false)]           // 9 dígitos: faltou o DDD
    [InlineData("+55 (51) 98909-8631", false)] // o site manda só dígitos
    public void Telefone_tem_10_ou_11_digitos(string phone, bool valid)
    {
        Assert.Equal(valid, InvalidFields(ValidGuest(phone: phone)).Length == 0);
    }

    [Fact]
    public void Cpf_com_mascara_eh_recusado_porque_o_contrato_manda_so_digitos()
    {
        var guest = new GuestDto { Name = "Ana Souza", Cpf = "529.982.247-25", Phone = "51989098631" };

        Assert.Equal(["Cpf"], InvalidFields(guest));
    }

    [Theory]
    [InlineData("Bruno Lima", null)]
    [InlineData("Bruno Lima", "11144477735")]
    public void Segundo_hospede_valido(string name, string? cpf)
    {
        Assert.Empty(InvalidFields(new SecondGuestDto { Name = name, Cpf = cpf }));
    }

    [Fact]
    public void Segundo_hospede_com_cpf_invalido()
    {
        Assert.Equal(["Cpf"], InvalidFields(new SecondGuestDto { Name = "Bruno Lima", Cpf = "11144477700" }));
    }

    [Fact]
    public void Segundo_hospede_com_cpf_e_sem_nome()
    {
        Assert.Equal(["Name"], InvalidFields(new SecondGuestDto { Name = null, Cpf = "11144477735" }));
    }

    [Fact]
    public void Mais_de_um_kit_eh_recusado()
    {
        var request = new PostPreReservationRequest
        {
            Guest = ValidGuest(),
            ExtraIds = [Guid.NewGuid(), Guid.NewGuid()],
        };

        Assert.Equal(["ExtraIds"], InvalidFields(request));
    }

    // ---------- Origem (attribution): dado de marketing nunca derruba a pré-reserva ----------

    [Fact]
    public void Sem_attribution_continua_valido()
    {
        var request = new PostPreReservationRequest { Guest = ValidGuest(), Attribution = null };

        Assert.Empty(InvalidFields(request));
    }

    [Fact]
    public void Attribution_com_lixo_continua_valido()
    {
        var attribution = new AttributionDto
        {
            Source = "tiktok",
            UtmSource = "   ",
            UtmCampaign = new string('x', 5000),
            FirstVisitAt = "ontem à tarde",
        };
        var request = new PostPreReservationRequest { Guest = ValidGuest(), Attribution = attribution };

        Assert.Empty(InvalidFields(request));
        Assert.Empty(InvalidFields(attribution)); // o ASP.NET valida o objeto aninhado também
    }

    [Fact]
    public void Data_de_primeira_visita_invalida_nao_quebra_a_leitura_do_json()
    {
        // Mesmas opções do ASP.NET (camelCase). Se FirstVisitAt fosse DateTimeOffset, isto lançaria JsonException (400).
        const string json = """
            { "guest": { "name": "Ana Souza", "cpf": "52998224725", "phone": "51989098631" },
              "attribution": { "source": "meta_ads", "firstVisitAt": "não é data" } }
            """;

        var request = JsonSerializer.Deserialize<PostPreReservationRequest>(json, JsonSerializerOptions.Web);

        Assert.Equal("não é data", request!.Attribution!.FirstVisitAt);
    }
}
