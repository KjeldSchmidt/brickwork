using Brickwork.Core.Models;

namespace Brickwork.Core.Ports;

public interface IInkFileAnalyzer
{
    string FormatId { get; }

    Task<CompatibilityReport> AnalyzeAsync(Stream source, CancellationToken cancellationToken = default);
}
