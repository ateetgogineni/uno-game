# Creates the Android upload key used to sign release builds (once per app, keep it forever).
#   powershell -ExecutionPolicy Bypass -File Tools\make-keystore.ps1
# Writes Keystore\unogame-upload.keystore and Keystore\keystore.properties (git-ignored).
# BACK BOTH FILES UP: Google Play only accepts updates signed with the same upload key.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $root 'Keystore'
$store = Join-Path $dir 'unogame-upload.keystore'
if (Test-Path $store) { Write-Host "Keystore already exists: $store"; exit 0 }
New-Item -ItemType Directory -Force $dir | Out-Null

$keytool = 'C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe'
$chars = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
$bytes = New-Object byte[] 24
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$pass = -join ($bytes | ForEach-Object { $chars[$_ % $chars.Length] })

& $keytool -genkeypair -keystore $store -storetype PKCS12 -alias unogame -keyalg RSA -keysize 2048 -validity 10000 `
    -storepass $pass -keypass $pass -dname 'CN=UNO Game, O=UNO Game' | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'keytool failed' }

@"
storeFile=unogame-upload.keystore
storePassword=$pass
keyAlias=unogame
keyPassword=$pass
"@ | Set-Content -Path (Join-Path $dir 'keystore.properties') -Encoding ascii
Write-Host "Created $store (alias unogame). Back up the Keystore folder somewhere safe."
