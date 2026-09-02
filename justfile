set windows-shell := ["powershell.exe", "-NoLogo", "-Command"]

# Install the .NET SDK required by global.json (Windows: winget, then dotnet-install.ps1 fallback)
setup-repo:
    powershell -NoLogo -ExecutionPolicy Bypass -File scripts/setup-dotnet.ps1
