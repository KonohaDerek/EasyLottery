FROM node:22-bookworm-slim AS frontend
WORKDIR /src
COPY src/EasyLotteryWeb/package.json src/EasyLotteryWeb/package-lock.json src/EasyLotteryWeb/
RUN npm ci --prefix src/EasyLotteryWeb
COPY src/EasyLotteryWeb src/EasyLotteryWeb
RUN npm run build --prefix src/EasyLotteryWeb

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first to maximize layer cache reuse.
COPY EasyLottery.generated.sln ./
COPY global.json ./
COPY src/EasyLotteryAPI/EasyLotteryApi.csproj src/EasyLotteryAPI/
COPY src/EasyLotteryDomain/EasyLotteryDomain.csproj src/EasyLotteryDomain/
COPY src/EasyLotteryApplication/EasyLotteryApplication.csproj src/EasyLotteryApplication/
COPY src/EasyLotteryInfrastructure/EasyLotteryInfrastructure.csproj src/EasyLotteryInfrastructure/
COPY src/EasyLotteryWasm/EasyLotteryWasm.csproj src/EasyLotteryWasm/
RUN dotnet restore src/EasyLotteryAPI/EasyLotteryApi.csproj

COPY . .
COPY --from=frontend /src/src/EasyLotteryWasm/wwwroot/vue src/EasyLotteryWasm/wwwroot/vue
RUN dotnet publish src/EasyLotteryAPI/EasyLotteryApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV APP_UID=1654
ENV ASPNETCORE_URLS=http://+:18930
EXPOSE 18930

COPY --from=build /app/publish .
RUN mkdir -p /data \
    && chown -R $APP_UID:$APP_UID /app /data
USER $APP_UID

ENTRYPOINT ["dotnet", "EasyLotteryApi.dll"]
