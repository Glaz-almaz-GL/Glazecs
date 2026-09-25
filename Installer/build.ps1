# Сборка установщика Glazecs: публикация приложения профилем Portable-win-x64, затем Inno Setup.
# Результат: Glazecs.App.Desktop\bin\Publish\Glazecs-<версия>-Setup.exe
#
#   powershell -ExecutionPolicy Bypass -File Installer\build.ps1
#   powershell -ExecutionPolicy Bypass -File Installer\build.ps1 -Iscc "D:\Tools\Inno Setup 7\ISCC.exe"

param(
    # Путь к ISCC.exe; если не задан — ищется в PATH, Program Files и по ярлыку Inno Setup в меню «Пуск»
    [string]$Iscc
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent

function Find-Iscc {
    $inPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($inPath) { return $inPath.Source }

    foreach ($dir in @("${env:ProgramFiles(x86)}\Inno Setup 7", "$env:ProgramFiles\Inno Setup 7",
                       "${env:ProgramFiles(x86)}\Inno Setup 6", "$env:ProgramFiles\Inno Setup 6")) {
        if (Test-Path "$dir\ISCC.exe") { return "$dir\ISCC.exe" }
    }

    # Inno Setup, установленный в произвольную папку, находится по ярлыку IDE в меню «Пуск»
    $startMenus = @("$env:APPDATA\Microsoft\Windows\Start Menu\Programs", "$env:ProgramData\Microsoft\Windows\Start Menu\Programs")
    $shortcut = Get-ChildItem $startMenus -Recurse -Filter 'Inno Setup*.lnk' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($shortcut) {
        $target = (New-Object -ComObject WScript.Shell).CreateShortcut($shortcut.FullName).TargetPath
        $candidate = Join-Path (Split-Path $target -Parent) 'ISCC.exe'
        if (Test-Path $candidate) { return $candidate }
    }

    return $null
}

if (-not $Iscc) { $Iscc = Find-Iscc }
if (-not $Iscc -or -not (Test-Path $Iscc)) {
    throw 'Не найден ISCC.exe (Inno Setup). Укажите путь: -Iscc "<папка Inno Setup>\ISCC.exe"'
}

Write-Host '== Публикация приложения' -ForegroundColor Cyan
$publishRoot = Join-Path $repoRoot 'Glazecs.App.Desktop\bin\Publish\Portable-win-x64'
# Чистая папка: иначе в установщик попадут файлы прежних публикаций
if (Test-Path $publishRoot) { Remove-Item $publishRoot -Recurse -Force }

dotnet publish (Join-Path $repoRoot 'Glazecs.App.Desktop\Glazecs.App.Desktop.csproj') `
    -f net10.0-windows10.0.19041.0 -p:PublishProfile=Portable-win-x64 -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish завершился с кодом $LASTEXITCODE" }

Write-Host "== Сборка установщика ($Iscc)" -ForegroundColor Cyan
& $Iscc (Join-Path $PSScriptRoot 'Glazecs.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC завершился с кодом $LASTEXITCODE" }
