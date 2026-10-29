# Run under Windows PowerShell -STA; keep clipboard setup outside the game's Mono runtime.
# Copy suite/scenarios/clipboard-images.json to a local set and set steps[0].fixtureDir to Directory's absolute path.
# Stop by creating Directory/stop. The helper restores the previous clipboard in its finally block.
param([Parameter(Mandatory=$true)][string]$Directory,[int]$TimeoutSec=480)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$Directory=[IO.Path]::GetFullPath($Directory)
New-Item -ItemType Directory -Force -Path $Directory | Out-Null
$previous=New-Object Windows.Forms.DataObject
$current=[Windows.Forms.Clipboard]::GetDataObject()
if($current){foreach($format in $current.GetFormats($false)){
    $value=$current.GetData($format,$false)
    if($value -is [IO.MemoryStream]){$value=[IO.MemoryStream]::new($value.ToArray())}
    if($value -is [Drawing.Image]){$value=[Drawing.Bitmap]::new($value)}
    if($null -ne $value){$previous.SetData($format,$false,$value)}
}}
$bitmap=[Drawing.Bitmap]::new(96,64)
$g=[Drawing.Graphics]::FromImage($bitmap)
$g.Clear([Drawing.Color]::Transparent)
$g.FillRectangle([Drawing.Brushes]::Red,0,0,48,32)
$g.FillRectangle([Drawing.Brushes]::Blue,48,0,48,32)
$g.Dispose()
$png=[IO.MemoryStream]::new();$bitmap.Save($png,[Drawing.Imaging.ImageFormat]::Png)
$path=Join-Path $Directory 'clipboard-fixture.png';$bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
$deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSec);$seen=''
try {
    Write-Output 'Clipboard fixture ready; previous clipboard retained in memory.'
    while([DateTime]::UtcNow -lt $deadline -and -not(Test-Path (Join-Path $Directory 'stop'))){
        $requestPath=Join-Path $Directory 'request.json'
        if(Test-Path $requestPath){
            try{$request=Get-Content $requestPath -Raw | ConvertFrom-Json}catch{$request=$null}
            if($request -and $request.id -ne $seen){
                switch($request.kind){
                    'png' {$png.Position=0;$data=New-Object Windows.Forms.DataObject;$data.SetData('PNG',$false,$png);[Windows.Forms.Clipboard]::SetDataObject($data,$true)}
                    'text' {[Windows.Forms.Clipboard]::SetText('FlowerPainter clipboard test')}
                    'bitmap' {$g=[Drawing.Graphics]::FromImage($bitmap);$g.Clear([Drawing.Color]::Yellow);$g.Dispose();[Windows.Forms.Clipboard]::SetImage($bitmap)}
                    'file' {$files=New-Object Collections.Specialized.StringCollection;[void]$files.Add($path);[Windows.Forms.Clipboard]::SetFileDropList($files)}
                    default {throw 'Unknown clipboard fixture'}
                }
                $seen=$request.id;[IO.File]::WriteAllText((Join-Path $Directory ($seen+'.ready')),'ready')
                Write-Output ('Clipboard fixture: '+$request.kind)
            }
        }
        Start-Sleep -Milliseconds 100
    }
} finally {
    if($previous.GetFormats($false).Length){[Windows.Forms.Clipboard]::SetDataObject($previous,$true)}else{[Windows.Forms.Clipboard]::Clear()}
    $bitmap.Dispose();$png.Dispose()
    Write-Output 'Previous clipboard restored.'
}
