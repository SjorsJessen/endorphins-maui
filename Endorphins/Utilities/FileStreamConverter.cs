using Microsoft.AspNetCore.Components.Forms;

namespace Endorphins.Utilities;

public static class FileStreamConverter
{
    public static async Task<string> ConvertToString(IBrowserFile file)
    {
        using var ms = new MemoryStream();
        var stream = file.OpenReadStream();
        await stream.CopyToAsync(ms);

        var bytes = ms.ToArray();
        var fileContent = System.Text.Encoding.UTF8.GetString(bytes);
        return fileContent;
    }
    
    public static async Task<byte[]> ConvertToBytes(IBrowserFile file)
    {
        using var ms = new MemoryStream();
        var stream = file.OpenReadStream(maxAllowedSize: 100_000_000);
        await stream.CopyToAsync(ms);
    
        var bytes = ms.ToArray();
        return bytes;
    }
}