using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using OpenLMStudio.Application.Interfaces;
using OpenLMStudio.Domain.Models;

namespace OpenLMStudio.Infrastructure.Services;

/// <summary>
/// Converts a string that may or may not be a valid GUID into a <see cref="Guid"/>.
/// Non-GUID strings (e.g. "a0000001") are turned into deterministic GUIDs via SHA-256.
/// Kept for backward compatibility with existing chat files that use non-standard GUIDs.
/// </summary>
internal sealed class StringGuidConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString() ?? string.Empty;
            if (Guid.TryParse(s, out var guid))
                return guid;
            using var sha = System.Security.Cryptography.SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s));
            return new Guid(hash);
        }
        if (reader.TokenType == JsonTokenType.Null)
            return default;
        return reader.TryGetGuid(out var g) ? g : Guid.Empty;
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("D"));
    }
}