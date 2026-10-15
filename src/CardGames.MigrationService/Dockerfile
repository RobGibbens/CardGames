FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.2 AS build
WORKDIR /src

COPY global.json ./
COPY nuget.config ./
RUN sed -i 's/"rollForward": "disable"/"rollForward": "latestFeature"/' global.json

COPY src/CardGames.MigrationService/CardGames.MigrationService.csproj src/CardGames.MigrationService/
COPY src/CardGames.Poker.Api/CardGames.Poker.Api.csproj src/CardGames.Poker.Api/
COPY src/CardGames.Poker.Events/CardGames.Poker.Events.csproj src/CardGames.Poker.Events/
COPY src/CardGames.ServiceDefaults/CardGames.ServiceDefaults.csproj src/CardGames.ServiceDefaults/
COPY src/CardGames.Poker/CardGames.Poker.csproj src/CardGames.Poker/
COPY src/CardGames.Core.French/CardGames.Core.French.csproj src/CardGames.Core.French/
COPY src/CardGames.Core/CardGames.Core.csproj src/CardGames.Core/
COPY src/CardGames.Contracts/CardGames.Contracts.csproj src/CardGames.Contracts/
RUN dotnet restore src/CardGames.MigrationService/CardGames.MigrationService.csproj

COPY . .
RUN dotnet publish src/CardGames.MigrationService/CardGames.MigrationService.csproj -c Release -o /app --no-restore -p:ErrorOnDuplicatePublishOutputFiles=false

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.2 AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "CardGames.MigrationService.dll"]
