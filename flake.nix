{
  description = "ZFS Spectator — read-only ZFS monitoring dashboard";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  };

  outputs = { self, nixpkgs }:
    let
      supportedSystems = [ "x86_64-linux" "aarch64-linux" ];
      forAllSystems = nixpkgs.lib.genAttrs supportedSystems;
    in
    {
      packages = forAllSystems (system:
        let
          pkgs = nixpkgs.legacyPackages.${system};
        in
        {
          default = pkgs.buildDotnetModule {
            pname = "zfs-spectator";
            version = "0.0.0-unstable-2026-04-02";
            src = self;
            projectFile = "src/ZfsDashboard/ZfsDashboard.csproj";
            nugetDeps = null;
            dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
            dotnet-runtime = pkgs.dotnetCorePackages.aspnetcore_10_0;
            executables = [ "ZfsDashboard" ];
            meta = {
              description = "A lightweight, read-only web dashboard for monitoring ZFS storage systems";
              homepage = "https://github.com/DocBrown101/zfs-spectator";
              license = pkgs.lib.licenses.asl20;
              mainProgram = "ZfsDashboard";
              platforms = pkgs.lib.platforms.linux;
            };
          };
        }
      );

      devShells = forAllSystems (system:
        let
          pkgs = nixpkgs.legacyPackages.${system};
        in
        {
          default = pkgs.mkShell {
            buildInputs = [ pkgs.dotnetCorePackages.sdk_10_0 ];
          };
        }
      );

      nixosModules.default = { config, lib, pkgs, ... }:
        let
          cfg = config.services.zfs-spectator;
        in
        {
          options.services.zfs-spectator = {
            enable = lib.mkEnableOption "ZFS Spectator, a read-only ZFS monitoring dashboard";

            package = lib.mkOption {
              type = lib.types.package;
              default = self.packages.${pkgs.system}.default;
              description = "The zfs-spectator package to use.";
            };

            port = lib.mkOption {
              type = lib.types.port;
              default = 5959;
              description = "HTTP port for the web UI.";
            };

            listenAddress = lib.mkOption {
              type = lib.types.str;
              default = "127.0.0.1";
              description = "Address to bind to. Use `0.0.0.0` to listen on all interfaces.";
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
              after = [
                "network.target"
                "zfs.target"
              ];

              environment = {
                ASPNETCORE_URLS = "http://${cfg.listenAddress}:${toString cfg.port}";
                ASPNETCORE_CONTENTROOT = "${cfg.package}/lib/zfs-spectator";
                Kestrel__Endpoints__Http__Url = "http://${cfg.listenAddress}:${toString cfg.port}";
                DOTNET_ENVIRONMENT = "prod";
              };

              path = [ config.boot.zfs.package ];

              serviceConfig = {
                ExecStart = lib.getExe cfg.package;
                WorkingDirectory = "${cfg.package}/lib/zfs-spectator";
                Restart = "on-failure";
                RestartSec = 5;
                User = cfg.user;
                Group = cfg.group;

                ProtectSystem = "strict";
                ProtectHome = true;
                PrivateTmp = true;
                NoNewPrivileges = true;
                ReadOnlyPaths = [ "/proc" ];
              };
            };

            networking.firewall.allowedTCPPorts = lib.mkIf cfg.openFirewall [ cfg.port ];
          };
        };
    };
}