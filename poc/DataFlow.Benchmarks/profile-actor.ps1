# Script to profile ActorBlock benchmarks with dotnet-counters
# This collects time-series metrics for steady-state vs rotation comparison
# Usage: .\profile-actor.ps1 -Mode [steady|rotation|memory] -ItemCount 10000 -RotateAfter 100

param(
    [ValidateSet("steady", "rotation", "memory")]
    [string]$Mode = "rotation",
    
    [int]$ItemCount = 10000,
    
    [int]$RotateAfter = 0
)

# Set defaults based on mode
if ($Mode -eq "steady") {
    $BenchmarkCommand = "actor-steady $ItemCount"
    $OutputPrefix = "actor_steady"
} elseif ($Mode -eq "memory") {
    if ($RotateAfter -eq 0) { $RotateAfter = 50 }
    $BenchmarkCommand = "actor-memory $ItemCount $RotateAfter"
    $OutputPrefix = "actor_memory"
} else {
    # Default to rotation mode
    if ($RotateAfter -eq 0) { $RotateAfter = 100 }
    $BenchmarkCommand = "actor-rotation $ItemCount $RotateAfter"
    $OutputPrefix = "actor_rotation"
}

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$OutputDir = "./benchmark-results"
$OutputFile = "$OutputDir/${OutputPrefix}_${Timestamp}.csv"

Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host "ActorBlock Profiling with dotnet-counters" -ForegroundColor Cyan
Write-Host "==============================================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration:"
Write-Host "  Mode:         $Mode"
Write-Host "  Items:        $ItemCount"
if ($Mode -ne "steady") {
    Write-Host "  Rotate After: $RotateAfter items"
}
Write-Host "  Output:       $OutputFile"
Write-Host ""

# Install dotnet-counters if not present
if (!(Get-Command dotnet-counters -ErrorAction SilentlyContinue)) {
    Write-Host "Installing dotnet-counters..." -ForegroundColor Yellow
    dotnet tool install -g dotnet-counters
}

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Build the benchmark project in release mode
Write-Host "Building benchmark project..." -ForegroundColor Yellow
$RepoRoot = (Get-Item $PSScriptRoot).Parent.Parent.FullName
Set-Location $RepoRoot
dotnet build -c Release poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj --no-restore

# Start the benchmark in the background
Write-Host ""
Write-Host "Starting benchmark..." -ForegroundColor Yellow
$BenchmarkProcess = Start-Process -FilePath "dotnet" `
    -ArgumentList "run --project poc/DataFlow.POC.Benchmarks/DataFlow.POC.Benchmarks.csproj --no-build -c Release -- $BenchmarkCommand" `
    -PassThru `
    -NoNewWindow

# Wait a moment for the process to start
Start-Sleep -Seconds 2

# Check if process is still running
if ($BenchmarkProcess.HasExited) {
    Write-Host "ERROR: Benchmark process failed to start or completed too quickly" -ForegroundColor Red
    exit 1
}

$BenchmarkPid = $BenchmarkProcess.Id
Write-Host "Benchmark started (PID: $BenchmarkPid)"
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
Write-Host "  python3 ../../.github/scripts/visualize-counters.py $OutputFile --output ${OutputDir}/charts_${OutputPrefix}_${Timestamp}" -ForegroundColor Yellow
Write-Host ""
