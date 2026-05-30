FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY Services/auth-service/Danetka.AuthService.csproj Services/auth-service/
COPY contracts/ contracts/

RUN dotnet restore Services/auth-service/Danetka.AuthService.csproj

COPY Services/auth-service/ Services/auth-service/

RUN dotnet publish Services/auth-service/Danetka.AuthService.csproj \
    -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false \
    && test -f /app/publish/Danetka.AuthService.dll

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:50051
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Danetka.AuthService.dll"]
