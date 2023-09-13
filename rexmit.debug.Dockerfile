FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS rexmit-build-base
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