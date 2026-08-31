using InkarnateTools.Core.Models;

namespace InkarnateTools.Core.Ports;

public interface IInkFileAnalyzer
{
    string FormatId { get; }

    Task<CompatibilityReport> AnalyzeAsync(Stream source, CancellationToken cancellationToken = default);
}
