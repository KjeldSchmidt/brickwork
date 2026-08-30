# Inkarnate ↔ VTT Converter

Tools for converting map exports between Inkarnate and virtual tabletop formats (Foundry VTT, UVTT1, UVTT2).

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## Build

From `converter/` (requires [just](https://github.com/casey/just)):

```bash
just recompile
```

Or with `dotnet` directly:

```bash
dotnet build converter/InkarnateTools.sln
dotnet test converter/InkarnateTools.sln
```

## Run (GUI)

```bash
cd converter && just gui
```

Minimal Avalonia shell: pick an Inkarnate `.ink` backup or JSON input, choose an export format, and convert.

## Run (CLI)

```bash
cd converter && just cli convert -i ../resources/empty-backup.ink -o output.uvtt -f uvtt2
```

Supported export formats: `uvtt1`, `uvtt2`, `foundry`.

## Architecture

```
converter/
  src/
    InkarnateTools.Core/         Domain model, ports, ConvertMapService (UI/CLI agnostic)
    InkarnateTools.Inkarnate/    Inkarnate JSON importer
    InkarnateTools.Exporters/    UVTT / Foundry exporter stubs
    InkarnateTools.Composition/  Wires adapters for App and Cli hosts
    InkarnateTools.App/          Avalonia desktop shell
    InkarnateTools.Cli/          Console host
  tests/
    InkarnateTools.Core.Tests/
```

Importers read gzipped Inkarnate `.ink` backups (JSON inside) and map title, scene size, preview dimensions, and grid metadata into the internal model. Exporters are still stubs.
