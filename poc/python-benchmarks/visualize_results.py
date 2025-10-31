"""
Visualization script to generate charts comparing benchmark results.
"""
import json
import csv
import argparse
from pathlib import Path
from typing import List, Dict
import matplotlib
matplotlib.use('Agg')  # Use non-interactive backend (required for headless/server environments and prevents display blocking)
import matplotlib.pyplot as plt
import numpy as np
import pandas as pd


def load_results(results_file: Path) -> pd.DataFrame:
    """Load benchmark results from CSV or JSON file"""
    if results_file.suffix == '.csv':
        df = pd.read_csv(results_file)
        # Normalize column names
        column_mapping = {
            'Library': 'library',
            'RecordCount': 'record_count',
            'Concurrency': 'concurrency',
            'ExecutionTime(s)': 'execution_time_sec',
            'Throughput(rec/s)': 'throughput_per_sec',
            'PeakMemory(MB)': 'peak_memory_mb',
            'GC_Gen0': 'gc_gen0',
            'GC_Gen1': 'gc_gen1',
            'GC_Gen2': 'gc_gen2',
            'CPU%': 'cpu_percent'
        }
        df.rename(columns=column_mapping, inplace=True)
    elif results_file.suffix == '.json':
        with open(results_file) as f:
            data = json.load(f)
        df = pd.DataFrame(data)
    else:
        raise ValueError(f"Unsupported file format: {results_file.suffix}")
    
    return df


def plot_throughput_comparison(df: pd.DataFrame, output_path: Path):
    """Generate throughput comparison chart"""
    fig, axes = plt.subplots(2, 2, figsize=(14, 10))
    fig.suptitle('Throughput Comparison (Records/Second)', fontsize=16, fontweight='bold')
    
    # Get unique configurations
    record_counts = sorted(df['record_count'].unique())
    concurrency_levels = sorted(df['concurrency'].unique())
    libraries = df['library'].unique()
    
    # Plot for each concurrency level
    for idx, concurrency in enumerate(concurrency_levels):
        if idx >= 4:  # Only 4 subplots
            break
        
        ax = axes[idx // 2, idx % 2]
        
        df_filtered = df[df['concurrency'] == concurrency]
        
        for library in libraries:
            lib_data = df_filtered[df_filtered['library'] == library]
            if not lib_data.empty:
                ax.plot(
                    lib_data['record_count'],
                    lib_data['throughput_per_sec'],
                    marker='o',
                    label=library,
                    linewidth=2
                )
        
        ax.set_xlabel('Record Count', fontsize=10)
        ax.set_ylabel('Throughput (rec/s)', fontsize=10)
        ax.set_title(f'Concurrency: {concurrency} workers', fontsize=12)
        ax.legend()
        ax.grid(True, alpha=0.3)
        ax.set_xscale('log')
    
    plt.tight_layout()
    plt.savefig(output_path, dpi=300, bbox_inches='tight')
    print(f"Saved throughput chart: {output_path}")
    plt.close()


def plot_execution_time_comparison(df: pd.DataFrame, output_path: Path):
    """Generate execution time comparison chart"""
    fig, axes = plt.subplots(2, 2, figsize=(14, 10))
    fig.suptitle('Execution Time Comparison', fontsize=16, fontweight='bold')
    
    concurrency_levels = sorted(df['concurrency'].unique())
    libraries = df['library'].unique()
    
    for idx, concurrency in enumerate(concurrency_levels):
        if idx >= 4:
            break
        
        ax = axes[idx // 2, idx % 2]
        
        df_filtered = df[df['concurrency'] == concurrency]
        
        for library in libraries:
            lib_data = df_filtered[df_filtered['library'] == library]
            if not lib_data.empty:
                ax.plot(
                    lib_data['record_count'],
                    lib_data['execution_time_sec'],
                    marker='s',
                    label=library,
                    linewidth=2
                )
        
        ax.set_xlabel('Record Count', fontsize=10)
        ax.set_ylabel('Execution Time (seconds)', fontsize=10)
        ax.set_title(f'Concurrency: {concurrency} workers', fontsize=12)
        ax.legend()
        ax.grid(True, alpha=0.3)
        ax.set_xscale('log')
    
    plt.tight_layout()
    plt.savefig(output_path, dpi=300, bbox_inches='tight')
    print(f"Saved execution time chart: {output_path}")
    plt.close()


def plot_memory_comparison(df: pd.DataFrame, output_path: Path):
    """Generate memory usage comparison chart"""
    fig, axes = plt.subplots(2, 2, figsize=(14, 10))
    fig.suptitle('Memory Usage Comparison', fontsize=16, fontweight='bold')
    
    concurrency_levels = sorted(df['concurrency'].unique())
    libraries = df['library'].unique()
    
    for idx, concurrency in enumerate(concurrency_levels):
        if idx >= 4:
            break
        
        ax = axes[idx // 2, idx % 2]
        
        df_filtered = df[df['concurrency'] == concurrency]
        
        for library in libraries:
            lib_data = df_filtered[df_filtered['library'] == library]
            if not lib_data.empty:
                ax.plot(
                    lib_data['record_count'],
                    lib_data['peak_memory_mb'],
                    marker='^',
                    label=library,
                    linewidth=2
                )
        
        ax.set_xlabel('Record Count', fontsize=10)
        ax.set_ylabel('Peak Memory (MB)', fontsize=10)
        ax.set_title(f'Concurrency: {concurrency} workers', fontsize=12)
        ax.legend()
        ax.grid(True, alpha=0.3)
        ax.set_xscale('log')
    
    plt.tight_layout()
    plt.savefig(output_path, dpi=300, bbox_inches='tight')
    print(f"Saved memory chart: {output_path}")
    plt.close()


def plot_concurrency_scaling(df: pd.DataFrame, output_path: Path):
    """Generate concurrency scaling chart"""
    record_counts = sorted(df['record_count'].unique())
    libraries = df['library'].unique()
    
    n_configs = len(record_counts)
    fig, axes = plt.subplots(1, min(n_configs, 3), figsize=(15, 5))
    if n_configs == 1:
        axes = [axes]
    fig.suptitle('Concurrency Scaling', fontsize=16, fontweight='bold')
    
    for idx, record_count in enumerate(record_counts[:3]):  # Max 3 charts
        ax = axes[idx] if n_configs > 1 else axes[0]
        
        df_filtered = df[df['record_count'] == record_count]
        
        for library in libraries:
            lib_data = df_filtered[df_filtered['library'] == library]
            if not lib_data.empty:
                lib_data_sorted = lib_data.sort_values('concurrency')
                ax.plot(
                    lib_data_sorted['concurrency'],
                    lib_data_sorted['throughput_per_sec'],
                    marker='o',
                    label=library,
                    linewidth=2
                )
        
        ax.set_xlabel('Concurrency (workers)', fontsize=10)
        ax.set_ylabel('Throughput (rec/s)', fontsize=10)
        ax.set_title(f'{record_count:,} records', fontsize=12)
        ax.legend()
        ax.grid(True, alpha=0.3)
    
    plt.tight_layout()
    plt.savefig(output_path, dpi=300, bbox_inches='tight')
    print(f"Saved concurrency scaling chart: {output_path}")
    plt.close()


def plot_efficiency_heatmap(df: pd.DataFrame, output_path: Path):
    """Generate efficiency heatmap (throughput per MB of memory)"""
    libraries = sorted(df['library'].unique())
    
    fig, axes = plt.subplots(1, len(libraries), figsize=(5 * len(libraries), 5))
    if len(libraries) == 1:
        axes = [axes]
    fig.suptitle('Efficiency: Throughput per MB Memory', fontsize=16, fontweight='bold')
    
    for idx, library in enumerate(libraries):
        ax = axes[idx]
        
        lib_data = df[df['library'] == library].copy()
        lib_data['efficiency'] = lib_data['throughput_per_sec'] / lib_data['peak_memory_mb']
        
        # Create pivot table
        pivot = lib_data.pivot(
            index='concurrency',
            columns='record_count',
            values='efficiency'
        )
        
        # Plot heatmap
        im = ax.imshow(pivot.values, cmap='YlOrRd', aspect='auto')
        
        # Set ticks
        ax.set_xticks(np.arange(len(pivot.columns)))
        ax.set_yticks(np.arange(len(pivot.index)))
        ax.set_xticklabels([f"{int(x):,}" for x in pivot.columns])
        ax.set_yticklabels(pivot.index)
        
        ax.set_xlabel('Record Count', fontsize=10)
        ax.set_ylabel('Concurrency', fontsize=10)
        ax.set_title(library, fontsize=12)
        
        # Add colorbar
        cbar = plt.colorbar(im, ax=ax)
        cbar.set_label('rec/s per MB', rotation=270, labelpad=15)
        
        # Add text annotations
        for i in range(len(pivot.index)):
            for j in range(len(pivot.columns)):
                if not np.isnan(pivot.values[i, j]):
                    text = ax.text(j, i, f'{pivot.values[i, j]:.0f}',
                                 ha="center", va="center", color="black", fontsize=8)
    
    plt.tight_layout()
    plt.savefig(output_path, dpi=300, bbox_inches='tight')
    print(f"Saved efficiency heatmap: {output_path}")
    plt.close()


def generate_all_charts(results_file: Path, output_dir: Path):
    """Generate all comparison charts"""
    print(f"Loading results from: {results_file}")
    df = load_results(results_file)
    
    print(f"\nGenerating charts...")
    output_dir.mkdir(exist_ok=True)
    
    # Generate charts
    plot_throughput_comparison(df, output_dir / "throughput_comparison.png")
    plot_execution_time_comparison(df, output_dir / "execution_time_comparison.png")
    plot_memory_comparison(df, output_dir / "memory_comparison.png")
    plot_concurrency_scaling(df, output_dir / "concurrency_scaling.png")
    plot_efficiency_heatmap(df, output_dir / "efficiency_heatmap.png")
    
    print(f"\nAll charts saved to: {output_dir}")


def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(description="Generate visualization charts from benchmark results")
    parser.add_argument(
        "results_file",
        type=Path,
        help="Path to results file (CSV or JSON)"
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=None,
        help="Output directory for charts (defaults to same directory as results)"
    )
    
    args = parser.parse_args()
    
    if not args.results_file.exists():
        print(f"Error: Results file not found: {args.results_file}")
        return
    
    output_dir = args.output_dir or args.results_file.parent / "charts"
    
    generate_all_charts(args.results_file, output_dir)


if __name__ == "__main__":
    main()
