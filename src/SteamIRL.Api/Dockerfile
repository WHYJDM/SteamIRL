FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/SteamIRL.Api/SteamIRL.Api.csproj src/SteamIRL.Api/
RUN dotnet restore src/SteamIRL.Api/SteamIRL.Api.csproj
COPY src/SteamIRL.Api/ src/SteamIRL.Api/
RUN dotnet publish src/SteamIRL.Api/SteamIRL.Api.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /app/keys && chown $APP_UID /app/keys
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SteamIRL.Api.dll"]
