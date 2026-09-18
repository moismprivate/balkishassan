[CmdletBinding()]
param(
  [ValidateSet('linux-x64', 'linux-arm64')]
  [string]$Runtime = 'linux-x64'
)

$ErrorActionPreference = 'Stop'
$solutionRoot = Split-Path -Parent $PSScriptRoot
$publishPath = Join-Path $solutionRoot 'artifacts/publish'

Push-Location $solutionRoot
try {
  dotnet tool restore
  dotnet restore BalkisHassan.sln
  dotnet test BalkisHassan.sln --configuration Release --no-restore
  dotnet publish src/BalkisHassan.Web/BalkisHassan.Web.csproj `
    --configuration Release `
    --runtime $Runtime `
    --self-contained false `
    --output $publishPath

  Write-Host "Publicatie gereed in $publishPath"
}
finally {
  Pop-Location
}
