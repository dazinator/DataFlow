# Script to profile POC comparison benchmark with dotnet-counters
# This collects time-series metrics for POC vs Non-POC comparison
# Usage: .\profile-comparison.ps1 [recordCount] [maxConcurrency] [iterations] [mode] [batchSize]
#   mode: "simple" (default) or "extended"

param(
    [int]$RecordCount = 10000,
    [int]$MaxConcurrency = 4,
    [int]$Iterations = 3,
    [string]$Mode = "simple",
    [int]$BatchSize = 100
)

$ErrorActionPreference = "Stop"

# Determine which command to run
if ($Mode -eq "simple") {
    $BenchmarkCommand = "direct-simple $RecordCount $MaxConcurrency $Iterations"
    $OutputPrefix = "simple_comparison"
} else {
    $BenchmarkCommand = "direct-extended $RecordCount $MaxConcurrency $BatchSize $Iterations"
    $OutputPrefix = "extended_comparison"
}

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$OutputDir = ".\BenchmarkDotNet.Artifacts\profiling"
$OutputFile = "$OutputDir\${OutputPrefix}_${Timestamp}.csv"

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "POC Comparison Profiling with dotnet-counters" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:"
Write-Host "  Mode:         $Mode"
Write-Host "  Records:      $RecordCount"
Write-Host "  Concurrency:  $MaxConcurrency"
if ($Mode -eq "extended") {
    Write-Host "  Batch Size:   $BatchSize"
}
Write-Host "  Iterations:   $Iterations"
Write-Host "  Output:       $OutputFile"
Write-Host ""

# Install dotnet-counters if not present
$dotnetCounters = Get-Command dotnet-counters -ErrorAction SilentlyContinue
if (-not $dotnetCounters) {
    Write-Host "Installing dotnet-counters..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-counters
}

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Navigate to repo root (script is in poc/DataFlow.POC.Benchmarks)
Push-Location (Join-Path $PSScriptRoot "../..")

try {
    # Build the benchmark project in release mode
    Write-Host "Building benchmark project..." -ForegroundColor Yellow
    dotnet build -c Release poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj --no-restore
    
    Write-Host ""
    Write-Host "Starting benchmark..." -ForegroundColor Yellow
    
    # Start the benchmark process
    $ProcessInfo = New-Object System.Diagnostics.ProcessStartInfo
    $ProcessInfo.FileName = "dotnet"
    $ProcessInfo.Arguments = "run --project poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj --no-build -c Release -- $BenchmarkCommand"
    $ProcessInfo.UseShellExecute = $false
    $ProcessInfo.RedirectStandardOutput = $true
    $ProcessInfo.RedirectStandardError = $true
    
    $Process = New-Object System.Diagnostics.Process
    $Process.StartInfo = $ProcessInfo
    $Process.Start() | Out-Null
    
    $BenchmarkPid = $Process.Id
    
    # Wait a moment for the process to fully start
    Start-Sleep -Seconds 2
    
    # Check if process is still running
    if ($Process.HasExited) {
        Write-Host "ERROR: Benchmark process failed to start or completed too quickly" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Benchmark started (PID: $BenchmarkPid)" -ForegroundColor Green
    Write-Host ""
    Write-Host "Collecting counters with dotnet-counters..." -ForegroundColor Yellow
    Write-Host "This will run until the benchmark completes..." -ForegroundColor Yellow
    Write-Host ""
    
    # Collect counters
    dotnet-counters collect `
        --process-id $BenchmarkPid `
        --output $OutputFile `
        --format csv `
        --counters System.Runtime
    
    Write-Host ""
    Write-Host "==============================================================================" -ForegroundColor Cyan
    Write-Host "Profiling completed" -ForegroundColor Cyan
    Write-Host "==============================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Output file: $OutputFile" -ForegroundColor Green
    Write-Host ""
    Write-Host "You can analyze the CSV file with Python, Excel, or other tools."
    Write-Host "To visualize the results, use the visualization script:"
    Write-Host "  python .github\scripts\visualize-counters.py $OutputFile" -ForegroundColor Yellow
    Write-Host ""
    
} finally {
    Pop-Location
}
