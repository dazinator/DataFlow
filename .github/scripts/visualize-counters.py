#!/usr/bin/env python3
"""
Visualize dotnet-counters CSV output for POC vs Non-POC comparison.
This script parses the CSV file and generates comparison charts.

Usage:
    python3 visualize-counters.py <csv_file> [--output <output_dir>]
"""

import argparse
import csv
import os
import sys
from datetime import datetime
from pathlib import Path

try:
    import matplotlib.pyplot as plt
    import matplotlib.dates as mdates
    HAS_MATPLOTLIB = True
except ImportError:
    HAS_MATPLOTLIB = False
    print("Warning: matplotlib not installed. Install with: pip install matplotlib")

try:
    import pandas as pd
    HAS_PANDAS = True
except ImportError:
    HAS_PANDAS = False
    print("Warning: pandas not installed. Install with: pip install pandas")


def parse_dotnet_counters_csv(csv_file):
    """Parse dotnet-counters CSV output into a structured format."""
    if not HAS_PANDAS:
        print("ERROR: pandas is required for parsing. Install with: pip install pandas")
        sys.exit(1)
    
    # Read the CSV file
    df = pd.read_csv(csv_file)
    
    # The CSV format from dotnet-counters has columns:
    # Timestamp, Name, Tags, Value
    # We need to pivot this into a time-series format
    
    # Parse timestamp to datetime
    df['Timestamp'] = pd.to_datetime(df['Timestamp'])
    
    # Create a pivot table with metrics as columns
    pivot = df.pivot_table(
        index='Timestamp',
        columns='Name',
        values='Value',
        aggfunc='first'
    )
    
    # Forward fill missing values for time-series continuity
    # For counter metrics, forward-fill maintains the last known value until updated
    pivot = pivot.ffill()
    
    return pivot


def create_comparison_charts(data, output_dir):
    """Create comparison charts from the parsed data."""
    if not HAS_MATPLOTLIB:
        print("ERROR: matplotlib is required for visualization. Install with: pip install matplotlib")
        sys.exit(1)
    
    # Ensure output directory exists
    os.makedirs(output_dir, exist_ok=True)
    
    # Calculate elapsed time in seconds from start
    start_time = data.index[0]
    data['Elapsed_Seconds'] = (data.index - start_time).total_seconds()
    
    # Create figure with subplots
    fig, axes = plt.subplots(3, 2, figsize=(16, 12))
    fig.suptitle('POC vs Non-POC Performance Comparison', fontsize=16, fontweight='bold')
    
    # Plot 1: GC Heap Size
    ax = axes[0, 0]
    if 'gc-heap-size' in data.columns:
        ax.plot(data['Elapsed_Seconds'], data['gc-heap-size'] / (1024*1024), 
                color='#2E86AB', linewidth=2)
        ax.set_ylabel('Heap Size (MB)', fontsize=10)
        ax.set_xlabel('Elapsed Time (seconds)', fontsize=10)
        ax.set_title('GC Heap Size Over Time', fontsize=12, fontweight='bold')
        ax.grid(True, alpha=0.3)
    
    # Plot 2: Allocation Rate
    ax = axes[0, 1]
    if 'alloc-rate' in data.columns:
        ax.plot(data['Elapsed_Seconds'], data['alloc-rate'] / (1024*1024), 
                color='#A23B72', linewidth=2)
        ax.set_ylabel('Allocation Rate (MB/sec)', fontsize=10)
        ax.set_xlabel('Elapsed Time (seconds)', fontsize=10)
        ax.set_title('Memory Allocation Rate', fontsize=12, fontweight='bold')
        ax.grid(True, alpha=0.3)
    
    # Plot 3: GC Collections
    ax = axes[1, 0]
    gc_cols = ['gen-0-gc-count', 'gen-1-gc-count', 'gen-2-gc-count']
    gc_labels = ['Gen 0', 'Gen 1', 'Gen 2']
    colors = ['#06A77D', '#F77E21', '#D72638']
    
    for col, label, color in zip(gc_cols, gc_labels, colors):
        if col in data.columns:
            ax.plot(data['Elapsed_Seconds'], data[col], 
                    label=label, linewidth=2, color=color)
    
    ax.set_ylabel('Collection Count', fontsize=10)
    ax.set_xlabel('Elapsed Time (seconds)', fontsize=10)
    ax.set_title('GC Collections Over Time', fontsize=12, fontweight='bold')
    ax.legend(loc='upper left')
    ax.grid(True, alpha=0.3)
    
    # Plot 4: Working Set
    ax = axes[1, 1]
    if 'working-set' in data.columns:
        ax.plot(data['Elapsed_Seconds'], data['working-set'] / (1024*1024), 
                color='#5E4C5A', linewidth=2)
        ax.set_ylabel('Working Set (MB)', fontsize=10)
        ax.set_xlabel('Elapsed Time (seconds)', fontsize=10)
        ax.set_title('Working Set Memory', fontsize=12, fontweight='bold')
        ax.grid(True, alpha=0.3)
    
    # Plot 5: ThreadPool Thread Count
    ax = axes[2, 0]
    if 'threadpool-thread-count' in data.columns:
        ax.plot(data['Elapsed_Seconds'], data['threadpool-thread-count'], 
                color='#E07A5F', linewidth=2)
        ax.set_ylabel('Thread Count', fontsize=10)
        ax.set_xlabel('Elapsed Time (seconds)', fontsize=10)
        ax.set_title('ThreadPool Thread Count', fontsize=12, fontweight='bold')
        ax.grid(True, alpha=0.3)
    
    # Plot 6: CPU Usage (if available)
    ax = axes[2, 1]
    if 'cpu-usage' in data.columns:
        ax.plot(data['Elapsed_Seconds'], data['cpu-usage'], 
                color='#3D5A80', linewidth=2)
        ax.set_ylabel('CPU Usage (%)', fontsize=10)
        ax.set_xlabel('Elapsed Time (seconds)', fontsize=10)
        ax.set_title('CPU Usage', fontsize=12, fontweight='bold')
        ax.grid(True, alpha=0.3)
    else:
        ax.text(0.5, 0.5, 'CPU metrics not available', 
                ha='center', va='center', transform=ax.transAxes)
        ax.set_xlabel('Elapsed Time (seconds)', fontsize=10)
        ax.set_title('CPU Usage', fontsize=12, fontweight='bold')
    
    plt.tight_layout(rect=[0, 0.03, 1, 0.96])
    
    # Save the figure
    output_file = os.path.join(output_dir, 'comparison_metrics.png')
    plt.savefig(output_file, dpi=150, bbox_inches='tight')
    print(f"Chart saved to: {output_file}")
    
    plt.close()
    
    # Create a summary statistics file
    create_summary_stats(data, output_dir)


def create_summary_stats(data, output_dir):
    """Create a summary statistics markdown file."""
    output_file = os.path.join(output_dir, 'summary_stats.md')
    
    with open(output_file, 'w') as f:
        f.write("# Performance Comparison Summary Statistics\n\n")
        f.write(f"**Generated:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n\n")
        
        f.write("## Key Metrics\n\n")
        f.write("| Metric | Min | Max | Mean | Median |\n")
        f.write("|--------|-----|-----|------|--------|\n")
        
        # GC Heap Size
        if 'gc-heap-size' in data.columns:
            heap_mb = data['gc-heap-size'] / (1024*1024)
            f.write(f"| GC Heap Size (MB) | {heap_mb.min():.2f} | {heap_mb.max():.2f} | "
                   f"{heap_mb.mean():.2f} | {heap_mb.median():.2f} |\n")
        
        # Working Set
        if 'working-set' in data.columns:
            ws_mb = data['working-set'] / (1024*1024)
            f.write(f"| Working Set (MB) | {ws_mb.min():.2f} | {ws_mb.max():.2f} | "
                   f"{ws_mb.mean():.2f} | {ws_mb.median():.2f} |\n")
        
        # Allocation Rate
        if 'alloc-rate' in data.columns:
            alloc_mb = data['alloc-rate'] / (1024*1024)
            f.write(f"| Allocation Rate (MB/s) | {alloc_mb.min():.2f} | {alloc_mb.max():.2f} | "
                   f"{alloc_mb.mean():.2f} | {alloc_mb.median():.2f} |\n")
        
        # ThreadPool
        if 'threadpool-thread-count' in data.columns:
            threads = data['threadpool-thread-count']
            f.write(f"| ThreadPool Threads | {threads.min():.0f} | {threads.max():.0f} | "
                   f"{threads.mean():.1f} | {threads.median():.0f} |\n")
        
        f.write("\n## GC Statistics\n\n")
        f.write("| Generation | Total Collections |\n")
        f.write("|------------|-------------------|\n")
        
        for gen in range(3):
            col = f'gen-{gen}-gc-count'
            if col in data.columns:
                total = data[col].max() - data[col].min()
                f.write(f"| Gen {gen} | {total:.0f} |\n")
        
        f.write("\n## Execution Duration\n\n")
        if 'Elapsed_Seconds' in data.columns:
            duration = data['Elapsed_Seconds'].max()
            f.write(f"**Total Duration:** {duration:.2f} seconds\n\n")
    
    print(f"Summary statistics saved to: {output_file}")


def main():
    parser = argparse.ArgumentParser(
        description='Visualize dotnet-counters CSV output for POC vs Non-POC comparison'
    )
    parser.add_argument('csv_file', help='Path to the dotnet-counters CSV file')
    parser.add_argument('--output', '-o', default='./charts',
                       help='Output directory for charts (default: ./charts)')
    
    args = parser.parse_args()
    
    if not os.path.exists(args.csv_file):
        print(f"ERROR: File not found: {args.csv_file}")
        sys.exit(1)
    
    print(f"Parsing CSV file: {args.csv_file}")
    data = parse_dotnet_counters_csv(args.csv_file)
    
    print(f"Creating comparison charts...")
    create_comparison_charts(data, args.output)
    
    print("\nVisualization complete!")
    print(f"Output directory: {args.output}")


if __name__ == '__main__':
    main()
