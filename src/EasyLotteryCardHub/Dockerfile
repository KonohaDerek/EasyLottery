FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json ./
COPY src/EasyLotteryCardHub/EasyLotteryCardHub.csproj src/EasyLotteryCardHub/
RUN dotnet restore src/EasyLotteryCardHub/EasyLotteryCardHub.csproj

COPY src/EasyLotteryCardHub/ src/EasyLotteryCardHub/
RUN dotnet publish src/EasyLotteryCardHub/EasyLotteryCardHub.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV APP_UID=1654
ENV ASPNETCORE_URLS=http://+:18940
EXPOSE 18940
COPY --from=build /app/publish .
RUN chown -R $APP_UID:$APP_UID /app
USER $APP_UID
ENTRYPOINT ["dotnet", "EasyLotteryCardHub.dll"]
