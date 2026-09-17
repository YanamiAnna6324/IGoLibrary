[CmdletBinding()]
param(
    [string]$Distribution,
    [string]$LinuxExecutablePath,
    [string]$IconSourcePath,
    [string]$Version = '0.0.0',
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$appName = 'IGoLibrary-Ex (WSL)'
$protocolName = 'igolibrary-ex'
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\IGoLibrary-Ex WSL'
$launcherPath = Join-Path $installRoot 'Launch-IGoLibraryExWsl.ps1'
$installedInstallerPath = Join-Path $installRoot 'Install-IGoLibraryExWsl.ps1'
$iconPath = Join-Path $installRoot 'IGoLibrary-Ex.ico'
$programsDirectory = [Environment]::GetFolderPath('Programs')
$shortcutPath = Join-Path $programsDirectory "$appName.lnk"
$protocolKey = "HKCU:\Software\Classes\$protocolName"
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\IGoLibrary-Ex-WSL'
$powershellPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

function Quote-WindowsArgument {
    param([Parameter(Mandatory)][string]$Value)

    return '"' + $Value.Replace('"', '\"') + '"'
}

function ConvertTo-PowerShellLiteral {
    param([Parameter(Mandatory)][string]$Value)

    return "'" + $Value.Replace("'", "''") + "'"
}

function Remove-LauncherRegistration {
    Remove-Item -LiteralPath $protocolKey -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue
}

if ($Uninstall) {
    Remove-LauncherRegistration
    Remove-Item -LiteralPath $installRoot -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "$appName was removed from Windows."
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Distribution)) {
    throw 'Distribution is required.'
}
if ([string]::IsNullOrWhiteSpace($LinuxExecutablePath) -or
    -not $LinuxExecutablePath.StartsWith('/', [StringComparison]::Ordinal)) {
    throw 'LinuxExecutablePath must be an absolute Linux path.'
}
if ($Distribution.Contains("`r") -or $Distribution.Contains("`n") -or
    $LinuxExecutablePath.Contains("`r") -or $LinuxExecutablePath.Contains("`n")) {
    throw 'Distribution and LinuxExecutablePath must not contain newlines.'
}

$wslPath = Join-Path $env:SystemRoot 'System32\wsl.exe'
& $wslPath -d $Distribution --exec test -x $LinuxExecutablePath
if ($LASTEXITCODE -ne 0) {
    throw "The Linux executable is missing or not executable: $LinuxExecutablePath"
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null

$distributionLiteral = ConvertTo-PowerShellLiteral $Distribution
$executableLiteral = ConvertTo-PowerShellLiteral $LinuxExecutablePath
$launcherContents = @'
param([string]$Uri)

$distribution = __DISTRIBUTION__
$linuxExecutable = __EXECUTABLE__
$wslPath = Join-Path $env:SystemRoot 'System32\wsl.exe'

& $wslPath -d $distribution --exec $linuxExecutable
exit $LASTEXITCODE
'@
$launcherContents = $launcherContents.Replace('__DISTRIBUTION__', $distributionLiteral)
$launcherContents = $launcherContents.Replace('__EXECUTABLE__', $executableLiteral)
[IO.File]::WriteAllText($launcherPath, $launcherContents, [Text.UTF8Encoding]::new($false))

if (-not [string]::Equals($PSCommandPath, $installedInstallerPath, [StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -LiteralPath $PSCommandPath -Destination $installedInstallerPath -Force
}
if (-not [string]::IsNullOrWhiteSpace($IconSourcePath) -and
    (Test-Path -LiteralPath $IconSourcePath -PathType Leaf)) {
    Copy-Item -LiteralPath $IconSourcePath -Destination $iconPath -Force
}

$launcherArguments = '-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File ' +
    (Quote-WindowsArgument $launcherPath)
$openCommand = (Quote-WindowsArgument $powershellPath) + ' ' + $launcherArguments + ' "%1"'
$uninstallArguments = '-NoProfile -ExecutionPolicy Bypass -File ' +
    (Quote-WindowsArgument $installedInstallerPath) + ' -Uninstall'
$uninstallCommand = (Quote-WindowsArgument $powershellPath) + ' ' + $uninstallArguments

New-Item -Path $protocolKey -Force | Out-Null
Set-Item -LiteralPath $protocolKey -Value "URL:$appName"
New-ItemProperty -LiteralPath $protocolKey -Name 'URL Protocol' -Value '' -PropertyType String -Force | Out-Null
$protocolIconKey = New-Item -Path (Join-Path $protocolKey 'DefaultIcon') -Force
Set-Item -LiteralPath $protocolIconKey.PSPath -Value ("$iconPath,0")
$protocolCommandKey = New-Item -Path (Join-Path $protocolKey 'shell\open\command') -Force
Set-Item -LiteralPath $protocolCommandKey.PSPath -Value $openCommand

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $powershellPath
$shortcut.Arguments = $launcherArguments
$shortcut.WorkingDirectory = $installRoot
$shortcut.Description = 'Start IGoLibrary-Ex in WSLg'
if (Test-Path -LiteralPath $iconPath -PathType Leaf) {
    $shortcut.IconLocation = "$iconPath,0"
}
$shortcut.Save()

New-Item -Path $uninstallKey -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name 'DisplayName' -Value $appName -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name 'DisplayVersion' -Value $Version -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name 'Publisher' -Value 'YanamiAnna6324' -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name 'InstallLocation' -Value $installRoot -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name 'UninstallString' -Value $uninstallCommand -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name 'QuietUninstallString' -Value $uninstallCommand -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name 'URLInfoAbout' -Value 'https://github.com/YanamiAnna6324/IGoLibrary' -PropertyType String -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name 'NoModify' -Value 1 -PropertyType DWord -Force | Out-Null
New-ItemProperty -LiteralPath $uninstallKey -Name 'NoRepair' -Value 1 -PropertyType DWord -Force | Out-Null
if (Test-Path -LiteralPath $iconPath -PathType Leaf) {
    New-ItemProperty -LiteralPath $uninstallKey -Name 'DisplayIcon' -Value $iconPath -PropertyType String -Force | Out-Null
}

Write-Host "$appName was registered for the current Windows user."
Write-Host "Start menu shortcut: $shortcutPath"
Write-Host "Windows Run command: $protocolName`:"
