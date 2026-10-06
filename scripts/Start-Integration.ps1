[CmdletBinding()]
param([string]$ConnectionString)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$previousConnection = $env:ConnectionStrings__MemoryDatabase
$previousMigration = $env:Database__MigrateOnStartup
try {
    if ($ConnectionString) { $env:ConnectionStrings__MemoryDatabase = $ConnectionString }
    $env:Database__MigrateOnStartup = 'true'
    dotnet run --project (Join-Path $repoRoot 'src/BattleHub.Memory.Api') --launch-profile http
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo iniciar Memory. Revisar SQL Server y la configuración.' }
} finally {
    $env:ConnectionStrings__MemoryDatabase = $previousConnection
    $env:Database__MigrateOnStartup = $previousMigration
}
