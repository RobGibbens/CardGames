FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.2 AS build
WORKDIR /src

COPY global.json ./
COPY nuget.config ./
RUN sed -i 's/"rollForward": "disable"/"rollForward": "latestFeature"/' global.json

COPY src/CardGames.Poker.Api/CardGames.Poker.Api.csproj src/CardGames.Poker.Api/
COPY src/CardGames.Poker.Events/CardGames.Poker.Events.csproj src/CardGames.Poker.Events/
COPY src/CardGames.ServiceDefaults/CardGames.ServiceDefaults.csproj src/CardGames.ServiceDefaults/
COPY src/CardGames.Poker/CardGames.Poker.csproj src/CardGames.Poker/
COPY src/CardGames.Core.French/CardGames.Core.French.csproj src/CardGames.Core.French/
COPY src/CardGames.Core/CardGames.Core.csproj src/CardGames.Core/
COPY src/CardGames.Contracts/CardGames.Contracts.csproj src/CardGames.Contracts/
RUN dotnet restore src/CardGames.Poker.Api/CardGames.Poker.Api.csproj

COPY . .
RUN dotnet publish src/CardGames.Poker.Api/CardGames.Poker.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.2 AS runtime
WORKDIR /app
RUN apt-get update \
	&& apt-get install -y --no-install-recommends curl \
	&& rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CardGames.Poker.Api.dll"]
