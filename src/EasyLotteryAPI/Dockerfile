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
RUN dotnet publish src/EasyLotteryAPI/EasyLotteryApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:18930
EXPOSE 18930

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EasyLotteryApi.dll"]
