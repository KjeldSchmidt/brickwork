using System.Text.Json;
using InkarnateTools.Core.Models;

namespace InkarnateTools.Inkarnate.Parsing;

internal interface IInkEntityHandler
{
    string EntityType { get; }

    TransactionUnderstanding Understanding { get; }

    void Apply(MapDocument map, JsonElement entity);
}
