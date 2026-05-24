    FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
    WORKDIR /src
    
    # Слой 1: только csproj для кэширования dotnet restore
    COPY Services/puzzle-service/PuzzleService.csproj Services/puzzle-service/
    RUN dotnet restore Services/puzzle-service/PuzzleService.csproj
    
    # Слой 2: исходники + протоколы gRPC
    COPY Services/puzzle-service/ Services/puzzle-service/
    COPY contracts/ contracts/
    
    RUN dotnet publish Services/puzzle-service/PuzzleService.csproj \
        -c Release -o /app /p:UseAppHost=false
    
    # --- runtime stage ---
    FROM mcr.microsoft.com/dotnet/aspnet:8.0
    WORKDIR /app
    COPY --from=build /app .
    
    # Указываем порт для gRPC сервера
    ENV ASPNETCORE_URLS=http://+:50052
    EXPOSE 50052
    
    ENTRYPOINT ["dotnet", "PuzzleService.dll"]