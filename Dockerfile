FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY PharmacyApiService/PharmacyApiService.csproj PharmacyApiService/
RUN dotnet restore PharmacyApiService/PharmacyApiService.csproj
COPY PharmacyApiService/ PharmacyApiService/
RUN dotnet publish PharmacyApiService/PharmacyApiService.csproj \
    -c Release -o /out --no-restore /p:UseAppHost=false

FROM build AS migrations
ENV DOTNET_ROLL_FORWARD=Major DOTNET_CLI_TELEMETRY_OPTOUT=1
RUN dotnet tool install dotnet-ef --version 9.0.5 --tool-path /tools
WORKDIR /src/PharmacyApiService
ENTRYPOINT ["/tools/dotnet-ef", "database", "update", "--no-build", "--configuration", "Release"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080
USER 1654:1654
EXPOSE 8080
ENTRYPOINT ["dotnet", "PharmacyApiService.dll"]
