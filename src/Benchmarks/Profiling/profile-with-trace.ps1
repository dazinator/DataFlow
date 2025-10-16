# PowerShell script for profiling the ETL benchmark with dotnet-trace on Windows
# This demonstrates how to capture CPU profiling data for flame graph analysis
#
# Usage from repository root: .\src\Benchmarks\Profiling\profile-with-trace.ps1 [record_count] [iterations]
# Usage from Benchmarks dir:  .\Profiling\profile-with-trace.ps1 [record_count] [iterations]

param(
    [int]$RecordCount = 50000,
    [int]$Iterations = 1
)

$ErrorActionPreference = "Stop"

Write-Host "======================================"
Write-Host "DataFlow Profiling with dotnet-trace"
Write-Host "======================================"
Write-Host ""

# Check if dotnet-trace is installed
$traceInstalled = $false
try {
    dotnet tool list -g | Select-String "dotnet-trace" | Out-Null
    $traceInstalled = $true
}
catch {
    $traceInstalled = $false
}

if (-not $traceInstalled) {
    Write-Host "dotnet-trace not found. Installing..."
    dotnet tool install -g dotnet-trace
}

# Configuration
$OutputDir = "BenchmarkDotNet.Artifacts\profiling"

Write-Host "Configuration:"
Write-Host "  Record Count: $RecordCount"
Write-Host "  Iterations: $Iterations"
Write-Host "  Output Directory: $OutputDir"
Write-Host ""

# Determine the correct directory to run from
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BenchmarksDir = Join-Path $ScriptDir ".."

# Check if we're in the Benchmarks directory or need to navigate there
if (-not (Test-Path "Benchmarks.csproj")) {
    $BenchmarksProjectPath = Join-Path $BenchmarksDir "Benchmarks.csproj"
    if (Test-Path $BenchmarksProjectPath) {
        Write-Host "Navigating to Benchmarks directory: $BenchmarksDir"
        Set-Location $BenchmarksDir
    }
    else {
        Write-Host "Error: Could not find Benchmarks.csproj"
        Write-Host "Please run this script from the src\Benchmarks directory or repository root"
        exit 1
    }
}

# Build the benchmarks
Write-Host "Building benchmarks in Release mode..."
dotnet build Benchmarks.csproj -c Release --no-incremental

Write-Host ""
Write-Host "Starting profiling run..."
Write-Host "The benchmark will run in the background while dotnet-trace collects data."
Write-Host ""

# Create output directory
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Start the benchmark in the background
$job = Start-Job -ScriptBlock {
    param($RecordCount, $Iterations)
    Set-Location $using:PWD
    dotnet run -c Release --no-build -- etl-direct $RecordCount $Iterations
} -ArgumentList $RecordCount, $Iterations

# Give it a moment to start
Write-Host "Waiting for benchmark process to start..."
Start-Sleep -Seconds 2

# Find the dotnet process
$maxRetries = 10
$processFound = $false
$processId = 0

for ($i = 1; $i -le $maxRetries; $i++) {
    # Try finding by process name
    $dotnetProcesses = Get-Process -Name "Benchmarks" -ErrorAction SilentlyContinue
    if ($dotnetProcesses) {
        $process = $dotnetProcesses | Select-Object -First 1
        $processId = $process.Id
        Write-Host "Found benchmark process (PID: $processId)"
        $processFound = $true
        break
    }
    
    # Also try finding dotnet.exe with etl-direct argument
    $dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
    if ($dotnetProcesses) {
        foreach ($proc in $dotnetProcesses) {
            try {
                $cmdLine = (Get-CimInstance Win32_Process -Filter "ProcessId = $($proc.Id)").CommandLine
                if ($cmdLine -like "*etl-direct*") {
                    $processId = $proc.Id
                    Write-Host "Found benchmark process (PID: $processId)"
                    $processFound = $true
                    break
                }
            }
            catch {
                continue
            }
        }
    }
    
    if ($processFound) { break }
    
    Write-Host "Waiting for process to start (attempt $i/$maxRetries)..."
    Start-Sleep -Seconds 1
}

if (-not $processFound) {
    Write-Host "Error: Could not find benchmark process"
    Write-Host "The benchmark may have started too quickly or failed to start"
    Write-Host "Try running manually: dotnet run -c Release -- etl-direct $RecordCount $Iterations"
    Stop-Job -Job $job
    Remove-Job -Job $job
    exit 1
}

Write-Host "Collecting trace from PID: $processId"
Write-Host ""

# Collect trace
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$traceFile = Join-Path $OutputDir "etl_trace_$timestamp.nettrace"

try {
    dotnet-trace collect `
        --process-id $processId `
        --profile cpu-sampling `
        --output $traceFile `
        --format speedscope
}
catch {
    Write-Host "Warning: dotnet-trace collection ended. This is expected if the benchmark completed quickly."
}

# Wait for benchmark to complete
Write-Host "Waiting for benchmark to complete..."
Wait-Job -Job $job | Out-Null
Receive-Job -Job $job
Remove-Job -Job $job

Write-Host ""
Write-Host "======================================"
Write-Host "Profiling complete!"
Write-Host "======================================"
Write-Host ""

if (Test-Path $traceFile) {
    Write-Host "Trace file saved to: $traceFile"
    Write-Host ""
    Write-Host "To view the flame graph:"
    Write-Host "  1. Open https://www.speedscope.app in your browser"
    Write-Host "  2. Drag and drop the .speedscope.json file"
    Write-Host ""
    
    # Try to convert if we got .nettrace instead of .speedscope.json
    if ($traceFile -notlike "*.speedscope.json") {
        $speedscopeFile = $traceFile -replace '\.nettrace$', '.speedscope.json'
        Write-Host "Converting to speedscope format..."
        try {
            dotnet-trace convert $traceFile --format speedscope
            Write-Host "Converted trace saved to: $speedscopeFile"
        }
        catch {
            Write-Host "Note: Manual conversion may be needed:"
            Write-Host "  dotnet-trace convert $traceFile --format speedscope"
        }
    }
}
else {
    Write-Host "Warning: Trace file was not created. The benchmark may have completed too quickly."
    Write-Host "Try with a larger record count or more iterations."
}

Write-Host ""
