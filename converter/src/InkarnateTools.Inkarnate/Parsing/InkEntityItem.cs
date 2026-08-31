using System.Text.Json;

namespace InkarnateTools.Inkarnate.Parsing;

internal sealed record InkEntityItem(string? LayerId, JsonElement Entity);
