Write-Host "Publishing TechKingPOS..."

dotnet publish -c Release -r win-x64 --self-contained false

Write-Host "Building Installer..."

& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Installer.iss

Write-Host "Installer Build Complete!"