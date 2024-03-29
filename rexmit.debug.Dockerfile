FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt update &&  \
    apt install -y curl libsodium-dev libopus-dev ffmpeg && \
    rm -rf /var/lib/apt/lists/*
RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp
RUN chmod a+rx /usr/local/bin/yt-dlp
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS rexmit-build-base
WORKDIR /src
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
	dotnet restore ./rexmit.sln

FROM rexmit-build-base AS rexmit-build
RUN --mount=type=cache,target=/root/.nuget/packages \
	dotnet build -c Debug --no-restore ./rexmit.sln

FROM rexmit-build AS rexmit-test
RUN dotnet test -c Debug --no-build --no-restore ./rexmit.sln

FROM rexmit-build AS publish-rexmit
RUN --mount=type=cache,target=/root/.nuget/packages \
	dotnet publish -c Debug --no-build --no-restore -o /app ./rexmit.csproj

FROM base AS final-rexmit
COPY --from=publish-rexmit /app .
ENTRYPOINT ["dotnet", "rexmit.dll"]