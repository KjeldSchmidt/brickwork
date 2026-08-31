using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Handlers;

namespace InkarnateTools.Inkarnate.Parsing;

internal sealed class InkTransactionRegistry
{
    private readonly IReadOnlyDictionary<string, IInkTransactionHandler> _handlers;

    private InkTransactionRegistry(IEnumerable<IInkTransactionHandler> handlers)
    {
        _handlers = handlers.ToDictionary(handler => handler.CommandType, StringComparer.OrdinalIgnoreCase);
    }

    public static InkTransactionRegistry CreateDefault()
    {
        IInkEntityHandler[] entityHandlers =
        [
            new GridEntityHandler(),
            new WallEntityHandler(),
            new LightEntityHandler(),
        ];

        IInkTransactionHandler[] transactionHandlers =
        [
            new LayerAddTransactionHandler(),
            new BrushTransactionHandler(),
            new EntityAddTransactionHandler(entityHandlers),
        ];

        return new InkTransactionRegistry(transactionHandlers);
    }

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction)
    {
        var commandType = InkJsonReader.ReadString(transaction, "cmdType");
        if (commandType is null)
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                "(missing cmdType)",
                TransactionUnderstanding.Unknown,
                "missing cmdType");
        }

        if (!_handlers.TryGetValue(commandType, out var handler))
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                commandType,
                TransactionUnderstanding.Unknown);
        }

        return handler.Process(context, transaction);
    }
}
