# Verifies the app: scripts/verify.ps1 <fast|smoke|full>. Exit code 0 = passed, 1 = failed, 2 = couldn't run.
# The logic lives in verify.cs (shared with verify.sh); see the top of that file. Works on Windows PowerShell 5.1.
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet run --file "$PSScriptRoot/verify.cs" -- @args
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
}
exit $code
