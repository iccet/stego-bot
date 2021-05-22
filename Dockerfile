FROM mcr.microsoft.com/dotnet/sdk:3.1 AS build

ARG ICCET_NUGET_USER
ARG ICCET_NUGET_API_KEY
ARG ICCET_NUGET_SOURCE=https://nuget.pkg.github.com/iccet/index.json
ARG PUBLISH_PROJECT=src/Api/Api.csproj

WORKDIR /app

COPY . ./

RUN dotnet nuget locals all --clear

RUN rm -rf ~/.nuget

RUN dotnet nuget add source ${ICCET_NUGET_SOURCE} \
    -n iccet -u ${ICCET_NUGET_USER} -p ${ICCET_NUGET_API_KEY} \
    --store-password-in-clear-text

RUN dotnet restore

RUN dotnet publish ${PUBLISH_PROJECT} -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:3.1

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "./Api.dll"]
