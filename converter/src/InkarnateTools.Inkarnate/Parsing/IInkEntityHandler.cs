using System.Text.Json;
using InkarnateTools.Core.Models;

namespace InkarnateTools.Inkarnate.Parsing;

internal interface IInkEntityHandler
{
    string EntityType { get; }

    TransactionUnderstanding Understanding { get; }

    void Apply(InkImportContext context, InkEntityItem item);
}
