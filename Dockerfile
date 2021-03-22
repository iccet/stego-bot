FROM dotnet/core/sdk:3.1 AS build
WORKDIR /app

COPY . ./

RUN dotnet nuget locals all --clear

RUN rm -rf ~/.nuget

RUN dotnet restore

RUN dotnet publish -c Release -o /app/publish

FROM dotnet/core/aspnet:3.1

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "./Api.dll"]
