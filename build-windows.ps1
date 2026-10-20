param([string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be MAJOR.MINOR.PATCH' }
python -m pip install -r requirements-build.txt
if ($LASTEXITCODE -ne 0) { throw 'Build dependency installation failed' }
python -m unittest discover -s tests -v
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
python -m PyInstaller --noconfirm --clean --windowed --onedir --name ITHelpdesk app.py
if ($LASTEXITCODE -ne 0) { throw 'Executable build failed' }
$iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
if (!(Test-Path $iscc)) { throw 'Install Inno Setup 6 from https://jrsoftware.org/isdl.php then run this script again.' }
& $iscc "/DAppVersion=$Version" 'installer\helpdesk.iss'
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
Get-FileHash "release\ITHelpdesk-Setup-$Version.exe" -Algorithm SHA256 | Format-List
