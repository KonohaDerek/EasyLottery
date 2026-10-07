FROM node:24-alpine AS vue-build
WORKDIR /repo
RUN corepack enable
COPY src/EasyLotteryVue/package.json src/EasyLotteryVue/pnpm-lock.yaml ./src/EasyLotteryVue/
WORKDIR /repo/src/EasyLotteryVue
RUN pnpm install --frozen-lockfile
COPY src/EasyLotteryVue/ ./
RUN pnpm build

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
COPY --from=vue-build /repo/src/EasyLotteryAPI/wwwroot/app/ ./src/EasyLotteryAPI/wwwroot/app/
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
