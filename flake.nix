{
  description = "ZFS Spectator — read-only ZFS monitoring dashboard";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    let
      # NixOS module — importable via `imports = [ zfs-spectator.nixosModules.default ]`
      nixosModule = { config, lib, pkgs, ... }:
        let
          cfg = config.services.zfs-spectator;
          pkg = self.packages.${pkgs.system}.default;
        in
        {
          options.services.zfs-spectator = {
            enable = lib.mkEnableOption "ZFS Spectator dashboard";

            port = lib.mkOption {
              type = lib.types.port;
              default = 5959;
              description = "HTTP port for the web UI.";
            };

            listenAddress = lib.mkOption {
              type = lib.types.str;
              default = "127.0.0.1";
              description = "Address to bind to. Use 0.0.0.0 to listen on all interfaces.";
            };

            openFirewall = lib.mkOption {
              type = lib.types.bool;
              default = false;
              description = "Whether to open the firewall port.";
            };

            user = lib.mkOption {
              type = lib.types.str;
              default = "zfs-spectator";
              description = "User account under which ZFS Spectator runs.";
            };

            group = lib.mkOption {
              type = lib.types.str;
              default = "zfs-spectator";
              description = "Group under which ZFS Spectator runs.";
            };
          };

          config = lib.mkIf cfg.enable {
            users.users.${cfg.user} = {
              isSystemUser = true;
              group = cfg.group;
              description = "ZFS Spectator service user";
            };
            users.groups.${cfg.group} = { };

            systemd.services.zfs-spectator = {
              description = "ZFS Spectator — read-only ZFS monitoring dashboard";
              wantedBy = [ "multi-user.target" ];
              after = [ "network.target" "zfs.target" ];

              environment = {
                ASPNETCORE_URLS = "http://${cfg.listenAddress}:${toString cfg.port}";
                DOTNET_ENVIRONMENT = "Production";
              };

              path = [ pkgs.zfs ];

              serviceConfig = {
                ExecStart = "${pkg}/bin/ZfsDashboard";
                Restart = "on-failure";
                RestartSec = 5;
                User = cfg.user;
                Group = cfg.group;

                # Read-only hardening
                ProtectSystem = "strict";
                ProtectHome = true;
                PrivateTmp = true;
                NoNewPrivileges = true;
                ReadOnlyPaths = [ "/proc" ];
              };
            };

            networking.firewall.allowedTCPPorts =
              lib.mkIf cfg.openFirewall [ cfg.port ];
          };
        };
    in
    (flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = nixpkgs.legacyPackages.${system};
      in
      {
        packages.default = pkgs.buildDotnetModule {
          pname = "zfs-spectator";
          version = "0.1.0";

          src = ./.;

          projectFile = "ZfsDashboard.csproj";
          nugetDeps = ./deps.json;

          dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
          dotnet-runtime = pkgs.dotnetCorePackages.aspnetcore_10_0;

          # Ensure zfs/zpool are available at runtime
          runtimeDeps = [ pkgs.zfs ];

          meta = {
            description = "Read-only ZFS monitoring dashboard";
            homepage = "https://github.com/DocBrown101/zfs-spectator";
            license = pkgs.lib.licenses.mit;
            platforms = pkgs.lib.platforms.linux;
          };
        };

        devShells.default = pkgs.mkShell {
          buildInputs = [
            pkgs.dotnetCorePackages.sdk_10_0
          ];
        };
      }
    )) // {
      nixosModules.default = nixosModule;
    };
}
