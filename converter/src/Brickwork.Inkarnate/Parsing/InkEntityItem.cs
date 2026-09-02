using System.Text.Json;

namespace Brickwork.Inkarnate.Parsing;

internal sealed record InkEntityItem(string? LayerId, JsonElement Entity);
