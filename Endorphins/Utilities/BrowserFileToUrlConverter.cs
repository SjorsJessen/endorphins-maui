using Microsoft.AspNetCore.Components.Forms;

namespace Endorphins.Utilities;

public static class BrowserFileToUrlConverter
{

    public static async Task<string> Convert(IBrowserFile file, int maxWidth = 200, int maxHeight = 200)
    {
        var resized = await file.RequestImageFileAsync(file.ContentType, maxWidth, maxHeight);
        return await CreateUrl(resized, maxAllowedSize: 100_000_000);
    }
    
    public static async Task<string> Convert(IBrowserFile file)
    {
        return await CreateUrl(file, maxAllowedSize: 50_000_000);
    }

    private static async Task<string> CreateUrl(IBrowserFile file, int maxAllowedSize)
    {
        await using var stream = file.OpenReadStream(maxAllowedSize);
        var bytes = new byte[file.Size];
        await stream.ReadExactlyAsync(bytes);
        return $"data:{file.ContentType};base64,{System.Convert.ToBase64String(bytes)}";
    }
}