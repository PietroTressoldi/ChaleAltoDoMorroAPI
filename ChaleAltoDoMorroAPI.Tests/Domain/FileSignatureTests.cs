using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// O tipo do arquivo vem dos primeiros bytes, nunca do ContentType ou da extensão que o navegador mandou.
public class FileSignatureTests
{
    [Fact]
    public void Jpeg_e_reconhecido_pelos_3_primeiros_bytes() =>
        Assert.Equal(FileSignature.Jpeg, FileSignature.Detect([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01]));

    [Fact]
    public void Png_e_reconhecido() =>
        Assert.Equal(FileSignature.Png, FileSignature.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]));

    // "RIFF" + tamanho + "WEBP"
    [Fact]
    public void Webp_precisa_de_RIFF_e_WEBP() =>
        Assert.Equal(FileSignature.Webp, FileSignature.Detect([0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50]));

    // "RIFF" + tamanho + "WAVE" (áudio .wav)
    [Fact]
    public void Riff_que_nao_e_webp_e_recusado() =>
        Assert.Null(FileSignature.Detect([0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45]));

    // tamanho da caixa + "ftyp" + "isom"
    [Fact]
    public void Mp4_tem_ftyp_a_partir_do_byte_4() =>
        Assert.Equal(FileSignature.Mp4, FileSignature.Detect([0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D]));

    // "MZ": cabeçalho de executável do Windows
    [Fact]
    public void Exe_renomeado_para_jpg_e_recusado() =>
        Assert.Null(FileSignature.Detect([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00]));

    [Fact]
    public void Arquivo_vazio_e_recusado() =>
        Assert.Null(FileSignature.Detect([]));

    [Fact]
    public void Jpeg_cortado_e_recusado() =>
        Assert.Null(FileSignature.Detect([0xFF, 0xD8]));

    [Fact]
    public void Imagens_aceitas_sao_jpeg_png_e_webp_mas_nao_mp4()
    {
        Assert.Equal(new[] { FileSignature.Jpeg, FileSignature.Png, FileSignature.Webp }, FileSignature.Images);
        Assert.DoesNotContain(FileSignature.Mp4, FileSignature.Images);
    }
}
