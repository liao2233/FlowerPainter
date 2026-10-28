param([Parameter(Mandatory=$true)][string]$RunsRoot)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publish=Join-Path $repo 'Properties'
$screens=Join-Path $publish 'Screenshots'
New-Item -ItemType Directory -Force -Path $screens | Out-Null
$scenes=@(
    @('01-curve-fill','queue-1027-124400','02-flowers-free-curve-clipped.10-built.png','Curves and background fill'),
    @('02-partitions','queue-1027-131421','01-circle-subdivisions.10-circle-filled.png','Independent region fills'),
    @('03-patterns','queue-1027-124400','01-flowers-circle-heart-regions.13-built.png','Circle and heart composition'),
    @('04-flower-detail','queue-1027-111849','01-flag-china.06-flag-close.png','Raised flower detail'),
    @('05-china','queue-1027-111849','01-flag-china.05-flag-top.png','China flag built from flowers'),
    @('06-rotation','queue-1028-010245','01-direct-flag.08-direct-built.png','Rotated Japan flag'),
    @('07-nepal','queue-1027-111849','06-catalog-np.08-flag-reloaded.png','Nepal after local reload'),
    @('08-usa','queue-1027-111849','05-catalog-us.06-flag-top.png','United States flag')
)
$jpeg=[Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
$quality=New-Object Drawing.Imaging.EncoderParameters 1
$quality.Param[0]=New-Object Drawing.Imaging.EncoderParameter ([Drawing.Imaging.Encoder]::Quality),90L
$records=@()
foreach($scene in $scenes) {
    $relative=Join-Path $scene[1] (Join-Path 'shots' $scene[2])
    $source=Join-Path $RunsRoot $relative
    if(-not(Test-Path -LiteralPath $source)){throw "Missing native game screenshot: $source"}
    $target=Join-Path $screens ($scene[0]+'.jpg')
    $photo=[Drawing.Image]::FromFile($source)
    try {$photo.Save($target,$jpeg,$quality)} finally {$photo.Dispose()}
    $records+=[ordered]@{file=('Properties/Screenshots/'+$scene[0]+'.jpg');sourceRun=$scene[1];sourceFile=$scene[2];sourceSha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash;description=$scene[3];kind='native-game-screenshot';changes='JPEG encoding only; no scene or UI edits'}
}
$records | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $publish 'media-sources.json') -Encoding utf8
# A new cover layout using the native screenshot. The title area is editorial;
# the game image is kept as a separate rectangle, never generated or painted over.
$cover=New-Object Drawing.Bitmap 1024,1024
$graphics=[Drawing.Graphics]::FromImage($cover)
$graphics.Clear([Drawing.Color]::FromArgb(18,35,28))
$graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$title=New-Object Drawing.Font 'Segoe UI',78,([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel)
$sub=New-Object Drawing.Font 'Microsoft YaHei UI',40,([Drawing.FontStyle]::Regular),([Drawing.GraphicsUnit]::Pixel)
$small=New-Object Drawing.Font 'Segoe UI',27,([Drawing.FontStyle]::Regular),([Drawing.GraphicsUnit]::Pixel)
$photo=[Drawing.Image]::FromFile((Join-Path $screens '05-china.jpg'))
try {
    $graphics.DrawString('FlowerPainter',$title,[Drawing.Brushes]::White,38,25)
    $graphics.DrawString('花卉 · 曲线 · 分区 · 旗帜',$sub,[Drawing.Brushes]::PaleGreen,42,125)
    $graphics.DrawImage($photo,0,226,1024,576)
    $graphics.DrawString('CITIES: SKYLINES II',$small,[Drawing.Brushes]::White,42,854)
    $graphics.DrawString('FLOWERS  /  CURVES  /  FLAGS',$small,[Drawing.Brushes]::PaleGreen,42,910)
    $cover.Save((Join-Path $publish 'Thumbnail.png'),[Drawing.Imaging.ImageFormat]::Png)
} finally {
    $photo.Dispose();$title.Dispose();$sub.Dispose();$small.Dispose();$graphics.Dispose();$cover.Dispose();$quality.Dispose()
}
Write-Host 'Prepared 8 native game screenshots and a 1024 x 1024 cover.'
