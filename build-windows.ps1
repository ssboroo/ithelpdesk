param([string]$Version = '2.0.0')
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be MAJOR.MINOR.PATCH' }
python -m pip install -r requirements-build.txt
if ($LASTEXITCODE -ne 0) { throw 'Build dependency installation failed' }
python -m unittest discover -s tests -v
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
python -m PyInstaller --noconfirm --clean --windowed --onedir --name RainyRemote app.py
if ($LASTEXITCODE -ne 0) { throw 'Executable build failed' }
python -m PyInstaller --noconfirm --clean --windowed --onefile --name RainyRemote-Portable app.py
if ($LASTEXITCODE -ne 0) { throw 'Portable build failed' }
$iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
if (!(Test-Path $iscc)) { throw 'Install Inno Setup 6 from https://jrsoftware.org/isdl.php then run this script again.' }
& $iscc "/DAppVersion=$Version" 'installer/remote.iss'
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
Copy-Item 'dist/RainyRemote-Portable.exe' 'release/RainyRemote-Portable.exe'
Get-ChildItem release/*.exe | Get-FileHash -Algorithm SHA256 | Format-List
