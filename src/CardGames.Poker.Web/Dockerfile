FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.2 AS build
WORKDIR /src

COPY global.json ./
COPY nuget.config ./
RUN sed -i 's/"rollForward": "disable"/"rollForward": "latestFeature"/' global.json

COPY src/CardGames.Poker.Web/CardGames.Poker.Web.csproj src/CardGames.Poker.Web/
COPY src/CardGames.Contracts/CardGames.Contracts.csproj src/CardGames.Contracts/
COPY src/CardGames.Core.French/CardGames.Core.French.csproj src/CardGames.Core.French/
COPY src/CardGames.Core/CardGames.Core.csproj src/CardGames.Core/
COPY src/CardGames.Poker.Events/CardGames.Poker.Events.csproj src/CardGames.Poker.Events/
COPY src/CardGames.Poker/CardGames.Poker.csproj src/CardGames.Poker/
COPY src/CardGames.ServiceDefaults/CardGames.ServiceDefaults.csproj src/CardGames.ServiceDefaults/
RUN dotnet restore src/CardGames.Poker.Web/CardGames.Poker.Web.csproj

COPY . .
# Publish must restore: blazor.web.js ships in the implicit Microsoft.AspNetCore.App.Internal.Assets
# pack, which the earlier csproj-only restore does not fetch.
RUN dotnet publish src/CardGames.Poker.Web/CardGames.Poker.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.2 AS runtime
WORKDIR /app
RUN apt-get update \
	&& apt-get install -y --no-install-recommends curl \
	&& rm -rf /var/lib/apt/lists/* \
	&& mkdir -p /keys \
	&& chown "$APP_UID:$APP_UID" /keys
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CardGames.Poker.Web.dll"]
