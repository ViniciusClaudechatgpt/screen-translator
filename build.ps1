param([string]$Version = "1.0.0")
$ErrorActionPreference = "Stop"

dotnet publish src/ScreenTranslator/ScreenTranslator.csproj -c Release -r win-x64 --self-contained true "-p:Version=$Version" -o publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou" }

$iscc = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
& $iscc "/DAppVersion=$Version" installer\setup.iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup falhou" }

Write-Host "Gerado: installer\Output\Setup.exe"
