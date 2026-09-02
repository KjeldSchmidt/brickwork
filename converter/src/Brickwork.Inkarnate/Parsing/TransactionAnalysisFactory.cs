using System.Text.Json;
using Brickwork.Core.Models;

namespace Brickwork.Inkarnate.Parsing;

internal static class TransactionAnalysisFactory
{
    public static TransactionAnalysis Create(
        JsonElement transaction,
        string commandType,
        TransactionUnderstanding understanding,
        string? detail = null,
        IReadOnlyList<TransactionAnalysis>? children = null,
        int? transactionIdOverride = null)
    {
        var analysis = new TransactionAnalysis
        {
            TransactionId = transactionIdOverride ?? InkJsonReader.ReadInt(transaction, "transactionId") ?? -1,
            CommandType = commandType,
            Understanding = understanding,
            Detail = detail,
            RawJson = understanding == TransactionUnderstanding.Unknown
                ? transaction.GetRawText()
                : null,
            Children = children ?? [],
        };

        return analysis;
    }
}
