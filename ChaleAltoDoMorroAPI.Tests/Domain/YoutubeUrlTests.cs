using ChaleAltoDoMorroAPI.Domain;

namespace ChaleAltoDoMorroAPI.Tests.Domain;

// Só aceita os links que o site sabe tocar (landing: src/lib/video.ts).
public class YoutubeUrlTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=30s")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc123")]
    [InlineData("https://youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    public void Formatos_que_o_site_toca_sao_aceitos(string url) =>
        Assert.True(YoutubeUrl.IsValid(url));

    [Theory]
    [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ")]           // sem HTTPS
    [InlineData("https://vimeo.com/123456")]                             // outro site
    [InlineData("https://www.youtube.com/watch?v=curto")]                // id com menos de 11
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQX")]         // id com 12
    [InlineData("https://www.youtube.com/watch?list=PL123&v=dQw4w9WgXcQ")] // v fora do lugar: o site não acha o id
    [InlineData("https://www.youtube.com.evil.com/watch?v=dQw4w9WgXcQ")] // domínio disfarçado
    [InlineData("https://evil.com/youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/")]
    [InlineData("")]
    public void Links_que_o_site_nao_toca_sao_recusados(string url) =>
        Assert.False(YoutubeUrl.IsValid(url));
}
