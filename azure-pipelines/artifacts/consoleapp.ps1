$RepoRoot = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..")
$BuildConfiguration = $env:BUILDCONFIGURATION
if (!$BuildConfiguration) {
    $BuildConfiguration = 'Debug'
}

$AppRoot = "$RepoRoot/bin/MasterMind.Console/$BuildConfiguration"

if (!(Test-Path $AppRoot))  { return }

@{
    "$AppRoot" = (Get-ChildItem $AppRoot -Recurse)
}
