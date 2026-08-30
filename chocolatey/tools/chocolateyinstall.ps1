$ErrorActionPreference = 'Stop'

# CutGeek ships an Inno Setup installer. The package downloads it from the GitHub release for the
# matching tag and verifies it against a SHA-256 checksum rather than embedding the binary. Because
# nothing is embedded, this package must NOT contain a tools\VERIFICATION.txt - that file is only
# for packages that ship a binary inside the nupkg, and including one is what the USP 8.0.0
# submission was rejected for.
$packageArgs = @{
  packageName    = 'cutgeek'
  fileType       = 'exe'
  url            = 'https://github.com/techygeekshome/CutGeek/releases/download/v1.0.1/CutGeekSetup.exe'
  checksum       = 'c9a6292add292f1dc6248a9e60a1a66e3ddb92662a6e00a058f5df0b6df67a99'
  checksumType   = 'sha256'
  silentArgs     = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-'
  validExitCodes = @(0, 3010, 1641)
}

Install-ChocolateyPackage @packageArgs
