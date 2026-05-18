# Генератор Данеток

Система автоматически парсит реальные истории из интернета, с помощью ИИ разбивает их на открытую и закрытую части и выдаёт пользователю готовые данетки.

## Сервисы

| Сервис | Язык | Порт gRPC | Описание |
|---|---|---|---|
| API Gateway | Go | 8000 (HTTP) | Единая точка входа |
| Auth Service | C# | 50051 | Регистрация, логин, JWT |
| Puzzle Service | C# | 50052 | Хранение и выдача данеток |
| AI Worker | C# | — | Обработка историй через LLM |
| Parser Service | Python | 50053 | Парсинг сайтов |

## Быстрый старт

### 1. Клонировать репозиторий
```bash
git clone https://github.com/your-org/danetka.git
cd danetka
```

### 2. Создать .env файл
```bash
cp .env.example .env
# Открыть .env и вставить OPENAI_API_KEY
```

### 3. Сгенерировать gRPC код из контрактов
```bash
# Go (Gateway)
cd services/gateway
protoc --go_out=. --go-grpc_out=. ../../contracts/*.proto

# C# (Auth, Puzzle, AI Worker)
# dotnet-grpc автоматически читает .proto при сборке

# Python (Parser)
cd services/parser-service
python -m grpc_tools.protoc -I../../contracts --python_out=. --grpc_python_out=. ../../contracts/*.proto
```

### 4. Запустить всё
```bash
docker-compose up --build
```

## Адреса после запуска

| Сервис | Адрес |
|---|---|
| API | http://localhost:8000 |
| Consul UI | http://localhost:8500 |

## API

### Авторизация
```
POST /auth/register   { "email": "...", "password": "...", "username": "..." }
POST /auth/login      { "email": "...", "password": "..." }
```

### Данетки
```
GET /puzzle/random
GET /puzzle/:id
GET /puzzle/list?page=1&page_size=10
```

### Парсинг
```
POST /parser/start    { "limit": 10 }
GET  /parser/status?job_id=...
```

## Структура репозитория

```
danetka/
├── contracts/              # gRPC .proto и Kafka JSON-схемы
│   ├── auth.proto
│   ├── puzzle.proto
│   ├── parser.proto
│   └── kafka/
│       └── story.raw.json
├── services/
│   ├── gateway/            # Go
│   ├── auth-service/       # C#
│   ├── puzzle-service/     # C#
│   ├── ai-worker/          # C#
│   └── parser-service/     # Python
├── infra/
│   └── postgres/
│       └── init.sql
├── docs/
│   └── architecture.txt
├── docker-compose.yml
├── .env.example
└── README.md
```

## Переменные окружения

Все переменные описаны в `docker-compose.yml`. Для локальной разработки создайте `.env`:

```env
OPENAI_API_KEY=sk-...
```
