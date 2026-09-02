# Brickwork

Desktop and CLI tools for converting Inkarnate map backups into virtual tabletop formats.

## Beta status

The GUI currently supports **Foundry VTT JSON export**. UVTT1/UVTT2 exporters exist in the CLI only and are still placeholders.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (run `just setup-repo` on Windows to install automatically)

## Build

From `converter/` (requires [just](https://github.com/casey/just)):

```bash
just recompile
```

Or with `dotnet` directly:

```bash
dotnet build converter/Brickwork.sln
dotnet test converter/Brickwork.sln
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
    Brickwork.Core/         Domain model, ports, ConvertMapService
    Brickwork.Inkarnate/    Inkarnate JSON importer
    Brickwork.Exporters/    Foundry exporter (+ UVTT stubs)
    Brickwork.Composition/  Wires adapters for App and Cli hosts
    Brickwork.App/          Avalonia desktop shell
    Brickwork.Cli/          Console host
  tests/
    Brickwork.Core.Tests/
```

Importers read gzipped Inkarnate `.ink` backups and reconstruct walls, layers, portals, and compatibility metadata.
