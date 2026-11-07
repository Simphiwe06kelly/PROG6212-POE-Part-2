namespace RaceDay.Api.Helpers;

public sealed record ImageInfo(string ContentType, string Extension);

/// <summary>
/// Checks an uploaded file really is a small JPG, PNG, GIF or WEBP image. The type is worked out from the
/// file's first bytes, not from the name or content type the client sent (those are easy to fake).
/// </summary>
public static class ImageFileValidator
{
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    public static async Task<(ImageInfo? Image, string? Error)> InspectAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return (null, "Please choose an image file to upload.");

        if (file.Length > MaxBytes)
            return (null, "The image must be 5 MB or smaller.");

        var header = new byte[12];
        int read;
        await using (var stream = file.OpenReadStream())
        {
            read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        }

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return (new ImageInfo("image/jpeg", ".jpg"), null);

        if (read >= 8 && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
            return (new ImageInfo("image/png", ".png"), null);

        if (read >= 4 && header[0] == 'G' && header[1] == 'I' && header[2] == 'F' && header[3] == '8')
            return (new ImageInfo("image/gif", ".gif"), null);

        if (read >= 12 && header[0] == 'R' && header[1] == 'I' && header[2] == 'F' && header[3] == 'F'
            && header[8] == 'W' && header[9] == 'E' && header[10] == 'B' && header[11] == 'P')
            return (new ImageInfo("image/webp", ".webp"), null);

        return (null, "Only JPG, PNG, GIF or WEBP images are allowed.");
    }
}