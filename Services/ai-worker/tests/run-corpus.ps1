# ============================================================
#  Test corpus runner для AI Worker
#
#  Что делает:
#  1. Сбрасывает offset группы ai-worker-group на latest
#  2. Чистит логи и dataset.jsonl
#  3. Запускает AI Worker
#  4. Загружает все .txt файлы из stories/, оборачивает в Kafka-сообщения
#     и публикует их в топик stories.raw
#  5. Ждёт обработки
#  6. Останавливает воркер и показывает результат в виде таблицы
#
#  Запуск (из корня репо):
#    pwsh services/ai-worker/tests/run-corpus.ps1
# ============================================================

$ErrorActionPreference = "Stop"
$repoRoot     = Resolve-Path (Join-Path $PSScriptRoot "..\..\..\")
$workerDll    = Join-Path $repoRoot "services\ai-worker\bin\Debug\net8.0\Danetka.AiWorker.dll"
$workerDir    = Join-Path $repoRoot "services\ai-worker"
$datasetFile  = Join-Path $workerDir "logs\dataset.jsonl"
$storiesDir   = $PSScriptRoot + "\stories"
$logFile      = Join-Path $env:TEMP "aiworker-corpus.log"

if (-not (Test-Path $workerDll)) {
    Write-Error "Worker dll not found at $workerDll. Run 'dotnet build' first."
}

# --- 1. Reset Kafka offsets ---
Write-Host "Resetting Kafka offset to latest..."
docker exec danetka-kafka kafka-consumer-groups `
    --bootstrap-server localhost:29092 `
    --group ai-worker-group --topic stories.raw `
    --reset-offsets --to-latest --execute 2>$null | Out-Null

# --- 2. Clean state ---
Remove-Item $logFile     -ErrorAction SilentlyContinue
Remove-Item $datasetFile -ErrorAction SilentlyContinue

# --- 3. Start worker ---
Write-Host "Starting AI Worker..."
$proc = Start-Process -FilePath dotnet `
    -ArgumentList $workerDll `
    -WorkingDirectory $workerDir `
    -NoNewWindow -PassThru -RedirectStandardOutput $logFile

Start-Sleep -Seconds 5

# --- 4. Publish stories ---
$stories = Get-ChildItem $storiesDir -Filter "*.txt" | Sort-Object Name
Write-Host "Publishing $($stories.Count) stories..."

$expected = @()
foreach ($file in $stories) {
    # Используем .NET напрямую — Get-Content -Raw в PS 5.1 возвращает
    # объект-обёртку, который ConvertTo-Json превращает в {"value": "..."}.
    $text = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    $storyId = [guid]::NewGuid().ToString()
    $jobId   = [guid]::NewGuid().ToString()

    $msg = @{
        story_id = $storyId
        job_id = $jobId
        text = $text
        source_url = "synthetic://test-corpus/$($file.BaseName)"
        parsed_at = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
        retry_count = 0
    }
    $json = $msg | ConvertTo-Json -Compress

    $tempPath = Join-Path $env:TEMP "corpus-msg.json"
    [System.IO.File]::WriteAllText($tempPath, $json, [System.Text.UTF8Encoding]::new($false))

    docker cp $tempPath danetka-kafka:/tmp/msg.json 2>$null | Out-Null
    docker exec danetka-kafka bash -c "cat /tmp/msg.json | kafka-console-producer --bootstrap-server localhost:29092 --topic stories.raw" 2>$null

    Write-Host "  -> published $($file.BaseName) (story_id=$storyId)"
    $expected += [pscustomobject]@{ Name = $file.BaseName; StoryId = $storyId }
}

# --- 5. Wait for all messages to be processed ---
# ~5 sec per story on GigaChat-Pro (free tier) + buffer
$waitSeconds = ($stories.Count * 8) + 15
Write-Host "Waiting $waitSeconds seconds for processing..."
Start-Sleep -Seconds $waitSeconds

# --- 6. Stop worker ---
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# --- 7. Read dataset and show results ---
Write-Host "`n========== RESULTS ==========`n"
if (-not (Test-Path $datasetFile)) {
    Write-Warning "No dataset produced. See $logFile"
    exit 1
}

$results = @()
Get-Content $datasetFile -Encoding UTF8 | ForEach-Object {
    $obj = $_ | ConvertFrom-Json
    $name = ($expected | Where-Object StoryId -eq $obj.story_id).Name
    $results += [pscustomobject]@{
        Name      = if ($name) { $name } else { $obj.story_id.Substring(0, 8) }
        Model     = $obj.model
        Ms        = $obj.duration_ms
        OpenLen   = if ($obj.open_part)   { $obj.open_part.Length }   else { 0 }
        HiddenLen = if ($obj.hidden_part) { $obj.hidden_part.Length } else { 0 }
        Error     = $obj.error
    }
}

$results | Format-Table -AutoSize

Write-Host "`n========== DETAILED ==========`n"
Get-Content $datasetFile -Encoding UTF8 | ForEach-Object {
    $obj = $_ | ConvertFrom-Json
    $name = ($expected | Where-Object StoryId -eq $obj.story_id).Name
    Write-Host "--- $name ---" -ForegroundColor Cyan
    if ($obj.error) {
        Write-Host "ERROR: $($obj.error)" -ForegroundColor Red
    } else {
        Write-Host "OPEN:   $($obj.open_part)" -ForegroundColor Yellow
        Write-Host "HIDDEN: $($obj.hidden_part)"
    }
    Write-Host ""
}
