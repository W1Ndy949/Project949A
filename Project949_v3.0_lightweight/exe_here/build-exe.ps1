$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $compiler)) { $compiler = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $compiler)) { throw "找不到 .NET Framework C# 编译器 csc.exe。" }

$speech = "C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Speech\v4.0_4.0.0.0__31bf3856ad364e35\System.Speech.dll"
$icon = Join-Path $root "app.ico"
$mainExe = Join-Path $root "Project949_v3.0_lightweight.exe"
$setupExe = Join-Path $root "setup_base.exe"

$mainArgs = @(
  "/codepage:65001",
  "/target:winexe",
  "/optimize+",
  "/out:$mainExe",
  "/reference:System.dll",
  "/reference:System.Core.dll",
  "/reference:System.Drawing.dll",
  "/reference:System.Windows.Forms.dll",
  "/reference:System.Web.Extensions.dll",
  "/reference:$speech"
)
if (Test-Path $icon) { $mainArgs += "/win32icon:$icon" }
$mainArgs += (Join-Path $root "Program.cs")
& $compiler @mainArgs
if ($LASTEXITCODE -ne 0) { throw "Project949_v3.0_lightweight 编译失败。" }

$setupArgs = @(
  "/codepage:65001",
  "/target:winexe",
  "/optimize+",
  "/out:$setupExe",
  "/reference:System.dll",
  "/reference:System.Core.dll",
  "/reference:System.Drawing.dll",
  "/reference:System.Windows.Forms.dll"
)
if (Test-Path $icon) { $setupArgs += "/win32icon:$icon" }
$setupArgs += (Join-Path $root "setup_base.cs")
& $compiler @setupArgs
if ($LASTEXITCODE -ne 0) { throw "setup_base 编译失败。" }

Write-Host "Built: $mainExe"
Write-Host "Built: $setupExe"