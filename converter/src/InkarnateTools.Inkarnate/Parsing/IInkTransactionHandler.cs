using System.Text.Json;
using InkarnateTools.Core.Models;

namespace InkarnateTools.Inkarnate.Parsing;

internal interface IInkTransactionHandler
{
    string CommandType { get; }

    TransactionAnalysis Process(InkImportContext context, JsonElement transaction);
}
