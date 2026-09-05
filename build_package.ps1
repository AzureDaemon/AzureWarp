Add-Type -AssemblyName System.Drawing
$proj = 'C:\Users\awstu\Team Azure\AzureWarp'
$ts = Join-Path $proj 'thunderstore'
$iconPath = Join-Path $ts 'icon.png'

# --- placeholder "Chaos Gate" icon (256x256): dark azure bg + cyan ring + gold inner ring ---
# Only generate if no icon exists yet, so a hand-made/real icon is never clobbered.
if (-not (Test-Path $iconPath)) {
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(255, 10, 14, 26))
$pen1 = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 64, 196, 255)), 14
$g.DrawEllipse($pen1, 48, 48, 160, 160)
$pen2 = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 255, 210, 90)), 6
$g.DrawEllipse($pen2, 84, 84, 88, 88)
$g.Dispose()
$bmp.Save($iconPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output ("icon.png (generated placeholder) -> " + $iconPath)
} else {
Write-Output ("icon.png (kept existing) -> " + $iconPath)
}

# --- stage the package tree ---
$stage = Join-Path $ts '_stage'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$pluginDir = Join-Path $stage 'BepInEx\plugins\AzureWarp'
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item (Join-Path $ts 'manifest.json') $stage
Copy-Item (Join-Path $ts 'README.md') $stage
Copy-Item (Join-Path $ts 'CHANGELOG.md') $stage
Copy-Item $iconPath $stage
Copy-Item (Join-Path $proj 'bin\Release\AzureWarp.dll') $pluginDir

$zip = Join-Path $proj 'AzureCore-AzureWarp-0.3.0.zip'
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Output ("zip  -> " + $zip)
Get-Item $zip | ForEach-Object { Write-Output ("size -> " + $_.Length + " bytes") }
Write-Output "---- zip contents ----"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = [System.IO.Compression.ZipFile]::OpenRead($zip)
$z.Entries | ForEach-Object { Write-Output $_.FullName }
$z.Dispose()
