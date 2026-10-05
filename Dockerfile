FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY C2C.Web/C2C.Web.csproj C2C.Web/
RUN dotnet restore C2C.Web/C2C.Web.csproj
COPY C2C.Web/ C2C.Web/
RUN dotnet publish C2C.Web/C2C.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    DataDir=/data
VOLUME /data
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "C2C.Web.dll"]
