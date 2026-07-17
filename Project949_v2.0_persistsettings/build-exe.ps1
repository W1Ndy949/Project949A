$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$version = "Project949_v2.0_persistsettings"
$outDir = Join-Path $root "versions\$version"
$legacyBin = Join-Path $root "bin"
$exe = Join-Path $outDir "$version.exe"
$legacyExe = Join-Path $legacyBin "$version.exe"
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$speech = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Speech\v4.0_4.0.0.0__31bf3856ad364e35\System.Speech.dll"
$assetSource = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) "tietu"
$assetTarget = Join-Path $outDir "tietu"
$iconPng = Join-Path $assetSource "icon.png"
$iconIco = Join-Path $outDir "app.ico"

function Convert-PngToIco($pngPath, $icoPath) {
  Add-Type -AssemblyName System.Drawing
  $size = 256
  $source = [System.Drawing.Image]::FromFile($pngPath)
  try {
    $canvas = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
      $g = [System.Drawing.Graphics]::FromImage($canvas)
      try {
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $scale = [Math]::Min($size / [double]$source.Width, $size / [double]$source.Height)
        $w = [int][Math]::Round($source.Width * $scale)
        $h = [int][Math]::Round($source.Height * $scale)
        $x = [int][Math]::Round(($size - $w) / 2)
        $y = [int][Math]::Round(($size - $h) / 2)
        $g.DrawImage($source, $x, $y, $w, $h)
      }
      finally {
        if ($g -ne $null) { $g.Dispose() }
      }

      $pngStream = New-Object System.IO.MemoryStream
      try {
        $canvas.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $pngStream.ToArray()
        $outStream = [System.IO.File]::Create($icoPath)
        try {
          $writer = New-Object System.IO.BinaryWriter $outStream
          try {
            $writer.Write([UInt16]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]1)
            $writer.Write([Byte]0)
            $writer.Write([Byte]0)
            $writer.Write([Byte]0)
            $writer.Write([Byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$pngBytes.Length)
            $writer.Write([UInt32]22)
            $writer.Write($pngBytes)
          }
          finally {
            if ($writer -ne $null) { $writer.Dispose() }
          }
        }
        finally {
          if ($outStream -ne $null) { $outStream.Dispose() }
        }
      }
      finally {
        if ($pngStream -ne $null) { $pngStream.Dispose() }
      }
    }
    finally {
      if ($canvas -ne $null) { $canvas.Dispose() }
    }
  }
  finally {
    if ($source -ne $null) { $source.Dispose() }
  }
}

if (-not (Test-Path $compiler)) {
  $compiler = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $compiler)) {
  throw "找不到 .NET Framework C# 编译器 csc.exe。"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
New-Item -ItemType Directory -Force -Path $legacyBin | Out-Null

$compileArgs = @(
  "/target:winexe",
  "/optimize+",
  "/out:$exe",
  "/reference:System.dll",
  "/reference:System.Core.dll",
  "/reference:System.Drawing.dll",
  "/reference:System.Windows.Forms.dll",
  "/reference:System.Web.Extensions.dll",
  "/reference:$speech"
)

if (Test-Path $iconPng) {
  Convert-PngToIco $iconPng $iconIco
  $compileArgs += "/win32icon:$iconIco"
}

$compileArgs += (Join-Path $root "Program.cs")
& $compiler @compileArgs

if ($LASTEXITCODE -ne 0) { throw "C# 编译失败，退出码 $LASTEXITCODE。" }
Copy-Item -LiteralPath $exe -Destination $legacyExe -Force

if (Test-Path $assetSource) {
  New-Item -ItemType Directory -Force -Path $assetTarget | Out-Null
  Copy-Item -LiteralPath (Join-Path $assetSource "idle.png") -Destination (Join-Path $assetTarget "idle.png") -Force -ErrorAction SilentlyContinue
  Copy-Item -LiteralPath (Join-Path $assetSource "speaking.png") -Destination (Join-Path $assetTarget "speaking.png") -Force -ErrorAction SilentlyContinue
  Copy-Item -LiteralPath (Join-Path $assetSource "icon.png") -Destination (Join-Path $assetTarget "icon.png") -Force -ErrorAction SilentlyContinue
  $emotionSource = Join-Path $assetSource "emotions"
  $emotionTarget = Join-Path $assetTarget "emotions"
  if (Test-Path $emotionSource) {
    if (Test-Path $emotionTarget) { Remove-Item -LiteralPath $emotionTarget -Recurse -Force }
    Copy-Item -LiteralPath $emotionSource -Destination $assetTarget -Recurse -Force
  }
}

Copy-Item -LiteralPath (Join-Path $root "Program.cs") -Destination (Join-Path $outDir "Program.cs") -Force
Copy-Item -LiteralPath $MyInvocation.MyCommand.Path -Destination (Join-Path $outDir "build-exe.ps1") -Force
if (Test-Path (Join-Path $root "README.md")) {
  Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination (Join-Path $outDir "README.md") -Force
}

Write-Host "Built package: $outDir"










