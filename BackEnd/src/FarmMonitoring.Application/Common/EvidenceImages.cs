using FarmMonitoring.Domain.Entities;
using FluentValidation;

namespace FarmMonitoring.Application.Common;

public sealed record EvidenceImageData(string ContentType, string DataBase64);

public static class EvidenceImages
{
    public const int MaxBytes = 2 * 1024 * 1024;
    public static List<EvidenceImage> Decode(EvidenceImageData[]? images)
    {
        if (images is null) return [];
        if (images.Length > 5) throw new ValidationException("Attach at most 5 evidence images.");
        var result = new List<EvidenceImage>();
        foreach (var image in images)
        {
            if (image is null || image.ContentType is not ("image/png" or "image/jpeg") ||
                string.IsNullOrEmpty(image.DataBase64) || image.DataBase64.Length > 4 * ((MaxBytes + 2) / 3))
                throw new ValidationException("Evidence must be PNG or JPEG, at most 2 MiB per image.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(image.DataBase64); }
            catch (FormatException) { throw new ValidationException("Invalid base64 image data."); }
            var span = bytes.AsSpan();
            var matches = image.ContentType == "image/png"
                ? span.Length >= 45 && span.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
                  span.Slice(12, 4).SequenceEqual("IHDR"u8) && span[^12..].SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 })
                : span.Length >= 4 && span[0] == 255 && span[1] == 216 && span[2] == 255 && span[^2] == 255 && span[^1] == 217;
            if (bytes.Length > MaxBytes || !matches) throw new ValidationException("Image content does not match the declared PNG/JPEG format.");
            // Only returned as authenticated JSON and displayed in img elements; never served as executable content.
            result.Add(new() { ContentType = image.ContentType, Content = bytes });
        }
        return result;
    }

    public static EvidenceImageData[] Encode(IEnumerable<EvidenceImage> images) => images.OrderBy(x => x.Id)
        .Select(x => new EvidenceImageData(x.ContentType, Convert.ToBase64String(x.Content))).ToArray();
}
