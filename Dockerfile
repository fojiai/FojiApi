FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY FojiApi.slnx .
COPY src/FojiApi.Core/FojiApi.Core.csproj src/FojiApi.Core/
COPY src/FojiApi.Infrastructure/FojiApi.Infrastructure.csproj src/FojiApi.Infrastructure/
COPY src/FojiApi.Web.API/FojiApi.Web.API.csproj src/FojiApi.Web.API/
RUN dotnet restore

COPY src/ src/
RUN dotnet publish src/FojiApi.Web.API/FojiApi.Web.API.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FojiApi.Web.API.dll"]
