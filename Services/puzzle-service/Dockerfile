FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Services/puzzle-service/PuzzleService.csproj Services/puzzle-service/
RUN dotnet restore Services/puzzle-service/PuzzleService.csproj

COPY Services/puzzle-service/ Services/puzzle-service/
COPY contracts/ contracts/

RUN dotnet publish Services/puzzle-service/PuzzleService.csproj \
    -c Release -o /app /p:UseAppHost=false \
    && test -f /app/PuzzleService.dll

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:50052
EXPOSE 50052

ENTRYPOINT ["dotnet", "PuzzleService.dll"]
