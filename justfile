set windows-shell := ["powershell.exe", "-NoLogo", "-Command"]

# Install the .NET SDK required by global.json (Windows: winget, then dotnet-install.ps1 fallback)
setup-repo: setup-hooks setup-dotnet

setup-hooks:
    git config core.hooksPath git-hooks

[windows]
setup-dotnet:
    powershell -NoLogo -ExecutionPolicy Bypass -File scripts/setup-dotnet.ps1

[unix]
setup-dotnet:
    @echo "Skipping Windows-only .NET setup (scripts/setup-dotnet.ps1)"
