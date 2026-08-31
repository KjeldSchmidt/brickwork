using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate.Handlers;

internal sealed class BrushTransactionHandler : IInkTransactionHandler
{
    public string CommandType => "cmd-brush";

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        var detail = InkJsonReader.ReadString(transaction, "layerId") is { } layerId
            ? $"brush ({layerId})"
            : "brush";

        return TransactionAnalysisFactory.Create(
            transaction,
            CommandType,
            TransactionUnderstanding.KnownIgnored,
            detail);
    }
}
