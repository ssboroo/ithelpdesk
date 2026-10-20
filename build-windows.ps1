param([string]$Version='3.0.0')
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be MAJOR.MINOR.PATCH' }
dotnet publish native/Client/RainyRemote.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None "-p:Version=$Version" -o dist/native
if ($LASTEXITCODE -ne 0) { throw 'Native client build failed' }
$test=Start-Process -FilePath (Join-Path $PSScriptRoot 'dist/native/RainyRemote.exe') -ArgumentList '--self-test' -WorkingDirectory $PSScriptRoot -Wait -PassThru
if (Test-Path 'self-test-result.json') { Get-Content 'self-test-result.json' }
if ($test.ExitCode -ne 0 -or !(Test-Path 'self-test-result.json')) { throw 'Native integration tests failed' }
if (!((Get-Content 'self-test-result.json' -Raw | ConvertFrom-Json).ok)) { throw 'Native test results failed' }
$iscc=Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
if (!(Test-Path $iscc)) { throw 'Inno Setup 6 is required' }
& $iscc "/DAppVersion=$Version" 'installer/remote.iss'
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
Copy-Item 'dist/native/RainyRemote.exe' 'release/RainyRemote-Portable.exe'
