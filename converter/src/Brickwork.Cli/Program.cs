using Brickwork.Composition;

namespace Brickwork.Cli;

internal static class Program
{
    private const string RootUsage = """
        Usage:
          Brickwork.Cli convert -i <input> -o <output> -f <format>
          Brickwork.Cli analyze -i <input> [--summary]

        convert:
          -i, --input    Path to Inkarnate export
          -o, --output   Path for converted output file
          -f, --format   Export format: uvtt1, uvtt2, foundry

        analyze:
          -i, --input    Path to Inkarnate export
          --summary      Show summary only (default is full detail)
        """;

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            await Console.Error.WriteLineAsync(RootUsage).ConfigureAwait(false);
            return 1;
        }

        return args[0].ToLowerInvariant() switch
        {
            "convert" => await RunConvertAsync(args[1..]).ConfigureAwait(false),
            "analyze" => await RunAnalyzeAsync(args[1..]).ConfigureAwait(false),
            _ => await PrintUsageAndFailAsync().ConfigureAwait(false),
        };
    }

    private static async Task<int> RunConvertAsync(string[] args)
    {
        if (!TryParseConvertArgs(args, out var inputPath, out var outputPath, out var format, out var error))
        {
            await WriteConvertUsageAsync(error).ConfigureAwait(false);
            return 1;
        }

        try
        {
            var service = ServiceFactory.CreateConvertMapService();

            await using var input = File.OpenRead(inputPath);
            await using var output = File.Create(outputPath);
            await service.ConvertAsync(input, output, format, Path.GetFileName(inputPath))
                .ConfigureAwait(false);

            await Console.Out.WriteLineAsync($"Converted '{inputPath}' to '{outputPath}' ({format}).").ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Conversion failed: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> RunAnalyzeAsync(string[] args)
    {
        if (!TryParseAnalyzeArgs(args, out var inputPath, out var summaryOnly, out var error))
        {
            await WriteAnalyzeUsageAsync(error).ConfigureAwait(false);
            return 1;
        }

        try
        {
            var analyzer = ServiceFactory.CreateInkFileAnalyzer();

            await using var input = File.OpenRead(inputPath);
            var report = await analyzer.AnalyzeAsync(input).ConfigureAwait(false);

            var output = summaryOnly
                ? report.FormatSummary()
                : report.FormatDetailed();

            await Console.Out.WriteLineAsync(output).ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Analysis failed: {ex.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static async Task<int> PrintUsageAndFailAsync()
    {
        await Console.Error.WriteLineAsync(RootUsage).ConfigureAwait(false);
        return 1;
    }

    private static async Task WriteConvertUsageAsync(string error)
    {
        await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
        await Console.Error.WriteLineAsync().ConfigureAwait(false);
        await Console.Error.WriteLineAsync("""
            Usage: Brickwork.Cli convert -i <input> -o <output> -f <format>
            """).ConfigureAwait(false);
    }

    private static async Task WriteAnalyzeUsageAsync(string error)
    {
        await Console.Error.WriteLineAsync(error).ConfigureAwait(false);
        await Console.Error.WriteLineAsync().ConfigureAwait(false);
        await Console.Error.WriteLineAsync("""
            Usage: Brickwork.Cli analyze -i <input> [--summary]
            """).ConfigureAwait(false);
    }

    private static bool TryParseConvertArgs(
        string[] args,
        out string inputPath,
        out string outputPath,
        out string format,
        out string error)
    {
        inputPath = string.Empty;
        outputPath = string.Empty;
        format = string.Empty;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (TryReadOption(arg, args, ref i, "-i", "--input", out var inputValue))
            {
                inputPath = inputValue;
                continue;
            }

            if (TryReadOption(arg, args, ref i, "-o", "--output", out var outputValue))
            {
                outputPath = outputValue;
                continue;
            }

            if (TryReadOption(arg, args, ref i, "-f", "--format", out var formatValue))
            {
                format = formatValue;
                continue;
            }

            error = $"Unknown or incomplete argument: {arg}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            error = "Missing required option: --input";
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            error = "Missing required option: --output";
            return false;
        }

        if (string.IsNullOrWhiteSpace(format))
        {
            error = "Missing required option: --format";
            return false;
        }

        if (!File.Exists(inputPath))
        {
            error = $"Input file not found: {inputPath}";
            return false;
        }

        return true;
    }

    private static bool TryParseAnalyzeArgs(
        string[] args,
        out string inputPath,
        out bool summaryOnly,
        out string error)
    {
        inputPath = string.Empty;
        summaryOnly = false;
        error = string.Empty;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (string.Equals(arg, "--summary", StringComparison.OrdinalIgnoreCase))
            {
                summaryOnly = true;
                continue;
            }

            if (TryReadOption(arg, args, ref i, "-i", "--input", out var inputValue))
            {
                inputPath = inputValue;
                continue;
            }

            error = $"Unknown or incomplete argument: {arg}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(inputPath))
        {
            error = "Missing required option: --input";
            return false;
        }

        if (!File.Exists(inputPath))
        {
            error = $"Input file not found: {inputPath}";
            return false;
        }

        return true;
    }

    private static bool TryReadOption(
        string arg,
        string[] args,
        ref int index,
        string shortName,
        string longName,
        out string value)
    {
        value = string.Empty;

        if (string.Equals(arg, shortName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, longName, StringComparison.OrdinalIgnoreCase))
        {
            if (index + 1 >= args.Length)
            {
                return true;
            }

            value = args[index + 1];
            index++;
            return true;
        }

        if (arg.StartsWith($"{shortName}=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg[(shortName.Length + 1)..];
            return true;
        }

        if (arg.StartsWith($"{longName}=", StringComparison.OrdinalIgnoreCase))
        {
            value = arg[(longName.Length + 1)..];
            return true;
        }

        return false;
    }
}
