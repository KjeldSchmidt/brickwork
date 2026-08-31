using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Handlers;

namespace InkarnateTools.Inkarnate.Parsing;

internal sealed class InkTransactionRegistry
{
    private readonly IReadOnlyDictionary<string, IInkTransactionHandler> _handlers;

    private InkTransactionRegistry(IReadOnlyDictionary<string, IInkTransactionHandler> handlers)
    {
        _handlers = handlers;
    }

    public static InkTransactionRegistry CreateDefault()
    {
        IInkEntityHandler[] entityHandlers =
        [
            new GridEntityHandler(),
            new PathV2EntityHandler(),
            new LightEntityHandler(),
        ];

        var handlers = new Dictionary<string, IInkTransactionHandler>(StringComparer.OrdinalIgnoreCase);
        void Register(IInkTransactionHandler handler) => handlers[handler.CommandType] = handler;

        Register(new LayerAddTransactionHandler());
        Register(new BrushTransactionHandler());
        Register(new EntityAddTransactionHandler(entityHandlers));
        Register(new EntityUpdateTransactionHandler());
        Register(new EntityRemoveTransactionHandler());
        Register(new CompositeTransactionHandler((context, nested) => Process(handlers, context, nested)));

        return new InkTransactionRegistry(handlers);
    }

    public TransactionAnalysis Process(InkImportContext context, JsonElement transaction) =>
        Process(_handlers, context, transaction);

    private static TransactionAnalysis Process(
        IReadOnlyDictionary<string, IInkTransactionHandler> handlers,
        InkImportContext context,
        JsonElement transaction)
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

        if (!handlers.TryGetValue(commandType, out var handler))
        {
            return TransactionAnalysisFactory.Create(
                transaction,
                commandType,
                TransactionUnderstanding.Unknown);
        }

        return handler.Process(context, transaction);
    }
}
