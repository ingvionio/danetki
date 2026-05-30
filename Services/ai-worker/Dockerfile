FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Services/ai-worker/AiWorker.csproj Services/ai-worker/
RUN dotnet restore Services/ai-worker/AiWorker.csproj

COPY Services/ai-worker/ Services/ai-worker/
COPY contracts/ contracts/

RUN dotnet publish Services/ai-worker/AiWorker.csproj \
    -c Release -o /app /p:UseAppHost=false \
    && test -f /app/Danetka.AiWorker.dll

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Danetka.AiWorker.dll"]
