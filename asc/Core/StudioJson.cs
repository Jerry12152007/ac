using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ACLocalTrafficStudio;

/// <summary>統一的本地 JSON 序列化選項（人類可讀、枚舉以字串儲存、支援中文）。</summary>
public static class StudioJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };
}
