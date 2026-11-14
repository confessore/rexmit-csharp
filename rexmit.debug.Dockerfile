FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates libsodium-dev libopus-dev ffmpeg && \
    curl -fsSL https://deno.land/install.sh | DENO_INSTALL=/usr/local sh -s && \
    curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp && \
    chmod a+rx /usr/local/bin/yt-dlp && \
    rm -rf /var/lib/apt/lists/*
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY rexmit.csproj ./
RUN dotnet restore ./rexmit.csproj
COPY . .
RUN dotnet publish ./rexmit.csproj -c Release -o /app/publish --no-restore

FROM base AS final
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "rexmit.dll"]
