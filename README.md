# Inkarnate ↔ VTT Converter

Tools for converting Inkarnate map backups into virtual tabletop formats.

## Beta status

The GUI currently supports **Foundry VTT JSON export**. UVTT1/UVTT2 exporters exist in the CLI only and are still placeholders.

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

Open an Inkarnate `.ink` backup, preview wall overlays, edit walls, and use **Export to VTT** (Foundry JSON).

## Run (CLI)

```bash
cd converter && just cli convert -i ../resources/empty-backup.ink -o output.json -f foundry
cd converter && just cli analyze -i ../resources/empty-backup.ink
cd converter && just cli analyze -i ../resources/empty-backup.ink --summary
```

CLI export formats: `foundry` (supported), `uvtt1` / `uvtt2` (placeholders).

## Releases

Create a release from GitHub Actions → **Release** → Run workflow, and enter a version such as `0.1.0-beta.1`.

Builds are published for Windows, Linux, and macOS (x64 and arm64).

## Architecture

```
converter/
  src/
    InkarnateTools.Core/         Domain model, ports, ConvertMapService
    InkarnateTools.Inkarnate/    Inkarnate JSON importer
    InkarnateTools.Exporters/    Foundry exporter (+ UVTT stubs)
    InkarnateTools.Composition/  Wires adapters for App and Cli hosts
    InkarnateTools.App/          Avalonia desktop shell
    InkarnateTools.Cli/          Console host
  tests/
    InkarnateTools.Core.Tests/
```

Importers read gzipped Inkarnate `.ink` backups and reconstruct walls, layers, portals, and compatibility metadata.
