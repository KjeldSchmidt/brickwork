using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate.Parsing;

internal static class TransactionAnalysisFactory
{
    public static TransactionAnalysis Create(
        JsonElement transaction,
        string commandType,
        TransactionUnderstanding understanding,
        string? detail = null) =>
        new()
        {
            TransactionId = InkJsonReader.ReadInt(transaction, "transactionId") ?? -1,
            CommandType = commandType,
            Understanding = understanding,
            Detail = detail,
        };
}
