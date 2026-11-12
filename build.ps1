param([switch]$Test)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Compilador do .NET Framework x64 indisponível.' }
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object FullName)
$exe = Join-Path $dist 'ValeScore.exe'
$references = @('/r:System.Runtime.Serialization.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll')
& $compiler /nologo /codepage:65001 /platform:x64 /optimize+ /target:winexe "/out:$exe" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação do Vale Score.' }
if ($Test) {
    $tests = Join-Path $dist 'ValeScore.Tests.exe'
    & $compiler /nologo /codepage:65001 /platform:x64 /target:exe "/out:$tests" "/r:$exe" @references '/r:System.Xml.Linq.dll' (Join-Path $PSScriptRoot 'tests/Smoke.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação dos testes.' }
    & $tests $dist
    if ($LASTEXITCODE -ne 0) { throw 'Os testes falharam.' }
}
