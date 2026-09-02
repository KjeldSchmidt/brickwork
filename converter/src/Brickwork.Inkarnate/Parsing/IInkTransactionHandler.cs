using System.Text.Json;
using Brickwork.Core.Models;

namespace Brickwork.Inkarnate.Parsing;

internal interface IInkTransactionHandler
{
    string CommandType { get; }

    TransactionAnalysis Process(InkImportContext context, JsonElement transaction);
}
