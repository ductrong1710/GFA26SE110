using FarmMonitoring.Application.Common;
using FluentValidation;

namespace FarmMonitoring.UnitTests;

public class EvidenceImageTests
{
    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aS3sAAAAASUVORK5CYII=";

    [Fact]
    public void Evidence_preserves_bytes_and_rejects_untrusted_or_excessive_uploads()
    {
        var image = new EvidenceImageData("image/png", Png);
        var stored = Assert.Single(EvidenceImages.Decode([image]));
        Assert.Equal(Convert.FromBase64String(Png), stored.Content);
        Assert.Equal(image, Assert.Single(EvidenceImages.Encode([stored])));
        Assert.Empty(EvidenceImages.Decode(null));
        Assert.Throws<ValidationException>(() => EvidenceImages.Decode([image with { ContentType = "image/svg+xml" }]));
        Assert.Throws<ValidationException>(() => EvidenceImages.Decode([image with { ContentType = "image/jpeg" }]));
        Assert.Throws<ValidationException>(() => EvidenceImages.Decode([image with { DataBase64 = "invalid" }]));
        Assert.Throws<ValidationException>(() => EvidenceImages.Decode([null!]));
        Assert.Throws<ValidationException>(() => EvidenceImages.Decode(Enumerable.Repeat(image, 6).ToArray()));
        Assert.Throws<ValidationException>(() => EvidenceImages.Decode([image with { DataBase64 = Convert.ToBase64String(new byte[2 * 1024 * 1024 + 1]) }]));
        Assert.Throws<ValidationException>(() => EvidenceImages.Decode([image with { DataBase64 = Convert.ToBase64String("<script>alert(1)</script>"u8.ToArray()) }]));
    }
}
