FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
ARG TARGETARCH
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates unzip libsodium-dev libopus-dev ffmpeg && \
    case "$TARGETARCH" in \
      amd64) libdave_asset="libdave-Linux-X64-boringssl.zip"; libdave_sha256="7fdf10a7df894406291463130a322c04d7de58085b602500866a11874e8907b4" ;; \
      arm64) libdave_asset="libdave-Linux-ARM64-boringssl.zip"; libdave_sha256="8849d03f3b595d5de5147befc63795c1fdcee25f17637aa727b8109f3b08ac86" ;; \
      *) echo "Unsupported Docker target architecture: $TARGETARCH" >&2; exit 1 ;; \
    esac && \
    curl -fL --retry 3 --retry-all-errors "https://github.com/discord/libdave/releases/download/v1.2.1/cpp/$libdave_asset" -o /tmp/libdave.zip && \
    printf '%s  %s\n' "$libdave_sha256" /tmp/libdave.zip | sha256sum --check --status && \
    unzip -p /tmp/libdave.zip lib/libdave.so > /usr/local/lib/libdave.so && \
    chmod 0644 /usr/local/lib/libdave.so && \
    ldconfig && \
    curl -fsSL https://deno.land/install.sh | DENO_INSTALL=/usr/local sh -s && \
    curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp && \
    chmod a+rx /usr/local/bin/yt-dlp && \
    rm -rf /var/lib/apt/lists/* /tmp/libdave.zip
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY rexmit.csproj ./
RUN dotnet restore ./rexmit.csproj
COPY . .
RUN dotnet publish ./rexmit.csproj -c Release -o /app/publish --no-restore -p:TargetFrameworks=net8.0

FROM base AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "rexmit.dll"]
