FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Services/auth-service/Danetka.AuthService.csproj", "Services/auth-service/"]
COPY ["contracts/", "contracts/"]

RUN dotnet restore "Services/auth-service/Danetka.AuthService.csproj"

COPY ["Services/auth-service/", "Services/auth-service/"]

WORKDIR "/src/Services/auth-service"
RUN dotnet build "Danetka.AuthService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish "Danetka.AuthService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Danetka.AuthService.dll"]