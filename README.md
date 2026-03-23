# ZFS Spectator

A lightweight, read-only web dashboard for monitoring ZFS storage systems. ZFS Spectator gives you full visibility into your pools, datasets, snapshots, and system resources — without requiring elevated permissions or making any changes to your system.

**Look, don't touch.** ZFS Spectator has zero write operations. It only reads publicly available ZFS status information, making it safe to run alongside production workloads.

<p align="center">
  <a href="https://sonarcloud.io/summary/new_code?id=DocBrown101_zfs-spectator">
    <img src="https://sonarcloud.io/api/project_badges/quality_gate?project=DocBrown101_zfs-spectator" />
  </a>
</p>

## Features

- **System Dashboard** — CPU, memory, swap, and uptime at a glance
- **ZFS Pools** — Health status, capacity, encryption, error counts, and device layout
- **Pool Details** — Properties, scrub status, compression, dedup, fragmentation, and suggested maintenance commands
- **Datasets** — Quotas, compression, encryption, and mount status
- **Snapshots** — Grouped by dataset with creation time and space usage
- **ZVols** — Volume size, allocation, and block size
- **ARC Cache** — Hit rates (L1/L2), MRU/MFU breakdown, and size limits
- **Live I/O Monitoring** — Real-time disk and pool I/O charts with per-device bandwidth breakdown
- **Network Monitoring** — Per-interface upload/download rate charts
- **Dark Theme** — Clean, responsive Bootstrap 5 interface with automatic 1-second refresh

## Architecture

```
ZfsDashboard/          ASP.NET Core Razor Pages web UI
Zfs.Core/
  ├── Models/          Strongly-typed C# records (Pool, Dataset, Snapshot, etc.)
  ├── Services/        ZfsService, ZpoolService, SystemService
  └── Services/Parser/ JSON parsers for zfs/zpool command output
Zfs.Tests/             xUnit test suite with sample command output fixtures
```

All ZFS data is gathered by invoking `zfs` and `zpool` CLI commands in read-only mode and parsing their JSON output. System metrics are read from `/proc`. No database is required.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/) or later
- Linux with ZFS installed (`zfs` and `zpool` commands available on `$PATH`)
- No root or `sudo` required — standard user permissions are sufficient

## Getting Started

```bash
# Clone the repository
git clone https://github.com/DocBrown101/zfs-spectator.git
cd zfs-spectator

# Build and run
dotnet run --project ZfsDashboard
```

The dashboard will be available at `http://localhost:5959` by default.

## NixOS

ZFS Spectator is a natural fit for NixOS. Because the service is entirely read-only and needs no elevated permissions, it integrates cleanly into a NixOS system — no `security.wrappers`, no setuid, no capability grants. Just a minimal systemd service with strict sandboxing out of the box.

A Nix Flake is included with a ready-to-use NixOS module. (work in progress)

### Quick setup

Add the flake to your NixOS configuration inputs and import the module:

```nix
# flake.nix
{
  inputs.zfs-spectator.url = "github.com/DocBrown101/zfs-spectator";

  outputs = { self, nixpkgs, zfs-spectator, ... }: {
    nixosConfigurations.myhost = nixpkgs.lib.nixosSystem {
      modules = [
        zfs-spectator.nixosModules.default
        {
          services.zfs-spectator = {
            enable = true;
            port = 5959;
            listenAddress = "127.0.0.1";  # or "0.0.0.0" for all interfaces
            openFirewall = false;
          };
        }
      ];
    };
  };
}
```

This creates a dedicated `zfs-spectator` system user and a hardened systemd unit with `ProtectSystem=strict`, `NoNewPrivileges`, and read-only `/proc` access.

### Module options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `enable` | bool | `false` | Enable the ZFS Spectator service |
| `port` | port | `5959` | HTTP port for the web UI |
| `listenAddress` | string | `"127.0.0.1"` | Bind address (`0.0.0.0` for all interfaces) |
| `openFirewall` | bool | `false` | Automatically open the firewall port |
| `user` | string | `"zfs-spectator"` | Service user |
| `group` | string | `"zfs-spectator"` | Service group |

### Building manually with Nix

```bash
# Build the package
nix build

# Run directly
nix run

# Enter a dev shell with .NET SDK
nix develop
```

## Configuration

Standard ASP.NET Core configuration applies. You can set the listening URL via command line or environment variable:

```bash
# Custom port
dotnet run --project ZfsDashboard --urls "http://0.0.0.0:8080"

# Or via environment variable
ASPNETCORE_URLS="http://0.0.0.0:8080" dotnet run --project ZfsDashboard
```

## Running Tests

```bash
dotnet test Zfs.Tests
```

## Safety Guarantees

ZFS Spectator is designed to be safe by default:

- **No write endpoints** — the application exposes only `GET` handlers and a single read-only `/api/live` JSON endpoint
- **No destructive commands** — it never executes `zpool destroy`, `zfs set`, `zfs rollback`, or any modifying command
- **No elevated permissions** — runs as a standard user; does not require `root` or `sudo`
- **Command suggestions only** — maintenance commands (e.g., scrub) are displayed as copyable text, never executed

## Tech Stack

| Component | Technology |
|-----------|------------|
| Backend | ASP.NET Core 10 / C# with Razor Pages |
| Frontend | Bootstrap 5, Bootstrap Icons, Chart.js |
| ZFS Integration | CLI (`zfs`, `zpool`) with JSON output parsing |
| Live Updates | 1-second polling via `/api/live` endpoint |
| Tests | xUnit with sample command output fixtures |

## License

This project is provided as-is for personal and internal use. See [LICENSE](LICENSE) for details.
