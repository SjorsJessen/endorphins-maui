using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Endorphins.Services;

/// <summary>
/// Translates short text between languages via the free MyMemory API
/// (https://mymemory.translated.net/doc/spec.php), which needs no API key.
/// Kept deliberately small: the ink editor only needs quick NL⇄EN lookups while writing.
/// </summary>
public sealed class TranslationService
{
    private readonly HttpClient _http;

    public TranslationService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Translates <paramref name="text"/> from <paramref name="sourceLang"/> to
    /// <paramref name="targetLang"/> (ISO codes, e.g. "nl", "en"). Returns the translated
    /// string, or throws <see cref="TranslationException"/> with a user-readable message.
    /// </summary>
    public async Task<string> TranslateAsync(
        string text, string sourceLang, string targetLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var url = "https://api.mymemory.translated.net/get" +
                  $"?q={Uri.EscapeDataString(text)}&langpair={sourceLang}|{targetLang}";

        MyMemoryResponse? result;
        try
        {
            result = await _http.GetFromJsonAsync<MyMemoryResponse>(url, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TranslationException("Couldn't reach the translation service.", ex);
        }

        if (result?.ResponseStatus is 200 or 0 &&
            !string.IsNullOrEmpty(result.ResponseData?.TranslatedText))
        {
            return WebUtility.HtmlDecode(result.ResponseData!.TranslatedText!);
        }

        throw new TranslationException(
            result?.ResponseDetails is { Length: > 0 } detail
                ? detail
                : "Translation failed.");
    }

    private sealed class MyMemoryResponse
    {
        [JsonPropertyName("responseData")]
        public MyMemoryData? ResponseData { get; set; }

        // MyMemory returns this as a number most of the time, but occasionally as a string.
        [JsonPropertyName("responseStatus")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int ResponseStatus { get; set; }

        [JsonPropertyName("responseDetails")]
        public string? ResponseDetails { get; set; }
    }

    private sealed class MyMemoryData
    {
        [JsonPropertyName("translatedText")]
        public string? TranslatedText { get; set; }
    }
}

public sealed class TranslationException : Exception
{
    public TranslationException(string message, Exception? inner = null) : base(message, inner) { }
}
