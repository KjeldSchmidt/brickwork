using System.Text.Json;
using Brickwork.Core.Models;

namespace Brickwork.Inkarnate.Parsing;

internal interface IInkEntityHandler
{
    string EntityType { get; }

    TransactionUnderstanding Understanding { get; }

    void Apply(InkImportContext context, InkEntityItem item);
}
