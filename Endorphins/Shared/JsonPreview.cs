using System.Net;
using System.Text;
using System.Text.Json;

namespace Endorphins.Shared;

/// <summary>
/// Pretty-prints JSON into span-wrapped HTML matching the asset-preview theme
/// (.json-key / .json-str / .json-num / .json-bool / .json-null / .json-bracket …).
/// Mirrors the reference Ink IDE's JSON preview renderer.
/// </summary>
public static class JsonPreview
{
    private const int MaxDepth = 3;

    public static string Highlight(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var sb = new StringBuilder();
            Write(doc.RootElement, sb, 0);
            return sb.ToString();
        }
        catch
        {
            return WebUtility.HtmlEncode(raw);
        }
    }

    private static void Write(JsonElement el, StringBuilder sb, int depth)
    {
        var indent = new string(' ', depth * 2);
        var indentClose = new string(' ', Math.Max(0, depth - 1) * 2);

        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var props = new List<JsonProperty>(el.EnumerateObject());
                if (props.Count == 0)
                {
                    sb.Append("<span class=\"json-bracket\">{}</span>");
                    return;
                }
                if (depth >= MaxDepth)
                {
                    sb.Append("<span class=\"json-bracket\">{</span>")
                      .Append($"<span class=\"json-ellipsis\">…{props.Count}</span>")
                      .Append("<span class=\"json-bracket\">}</span>");
                    return;
                }
                sb.Append("<span class=\"json-bracket\">{</span>\n");
                for (var i = 0; i < props.Count; i++)
                {
                    sb.Append(indent);
                    sb.Append($"<span class=\"json-key\">&quot;{Esc(props[i].Name)}&quot;</span>")
                      .Append("<span class=\"json-colon\">: </span>");
                    Write(props[i].Value, sb, depth + 1);
                    if (i < props.Count - 1) sb.Append("<span class=\"json-comma\">,</span>");
                    sb.Append('\n');
                }
                sb.Append(indentClose).Append("<span class=\"json-bracket\">}</span>");
                return;
            }
            case JsonValueKind.Array:
            {
                var items = new List<JsonElement>(el.EnumerateArray());
                if (items.Count == 0)
                {
                    sb.Append("<span class=\"json-bracket\">[]</span>");
                    return;
                }
                if (depth >= MaxDepth)
                {
                    sb.Append("<span class=\"json-bracket\">[</span>")
                      .Append($"<span class=\"json-ellipsis\">…{items.Count}</span>")
                      .Append("<span class=\"json-bracket\">]</span>");
                    return;
                }
                sb.Append("<span class=\"json-bracket\">[</span>\n");
                for (var i = 0; i < items.Count; i++)
                {
                    sb.Append(indent);
                    Write(items[i], sb, depth + 1);
                    if (i < items.Count - 1) sb.Append("<span class=\"json-comma\">,</span>");
                    sb.Append('\n');
                }
                sb.Append(indentClose).Append("<span class=\"json-bracket\">]</span>");
                return;
            }
            case JsonValueKind.String:
                sb.Append($"<span class=\"json-str\">&quot;{Esc(el.GetString() ?? string.Empty)}&quot;</span>");
                return;
            case JsonValueKind.Number:
                sb.Append($"<span class=\"json-num\">{Esc(el.GetRawText())}</span>");
                return;
            case JsonValueKind.True:
            case JsonValueKind.False:
                sb.Append($"<span class=\"json-bool\">{(el.ValueKind == JsonValueKind.True ? "true" : "false")}</span>");
                return;
            case JsonValueKind.Null:
            default:
                sb.Append("<span class=\"json-null\">null</span>");
                return;
        }
    }

    private static string Esc(string s) => WebUtility.HtmlEncode(s);
}
