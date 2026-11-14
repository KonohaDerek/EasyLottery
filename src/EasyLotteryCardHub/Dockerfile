FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/EasyLotteryCardHub/EasyLotteryCardHub.csproj src/EasyLotteryCardHub/
RUN dotnet restore src/EasyLotteryCardHub/EasyLotteryCardHub.csproj
COPY src/EasyLotteryCardHub src/EasyLotteryCardHub
RUN dotnet publish src/EasyLotteryCardHub/EasyLotteryCardHub.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "EasyLotteryCardHub.dll"]
