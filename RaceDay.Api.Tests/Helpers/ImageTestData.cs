using System.Net.Http.Headers;

namespace RaceDay.Api.Tests.Helpers;

/// <summary>Tiny fake files and the multipart body used by the upload tests.</summary>
public static class ImageTestData
{
    /// <summary>The first bytes of a PNG file (enough for the API's file-type check).</summary>
    public static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00 };

    public static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01 };

    /// <summary>Plain text pretending to be an image.</summary>
    public static readonly byte[] NotAnImage = System.Text.Encoding.UTF8.GetBytes("this is not an image at all");

    /// <summary>A multipart/form-data body with one file in the field called "file".</summary>
    public static MultipartFormDataContent Content(byte[] bytes, string fileName = "photo.png", string contentType = "image/png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var body = new MultipartFormDataContent();
        body.Add(file, "file", fileName);
        return body;
    }

    /// <summary>A PNG that is bigger than the 5 MB limit.</summary>
    public static byte[] OversizedPng()
    {
        var bytes = new byte[5 * 1024 * 1024 + 1];
        Array.Copy(Png, bytes, Png.Length);
        return bytes;
    }
}
