# Этап 1: Сборка приложения
FROM golang:1.26-alpine AS builder

WORKDIR /app

# Копируем файлы зависимостей
COPY go.mod go.sum ./
RUN go mod download

# Копируем весь исходный код гейтвея
COPY . .

# Собираем бинарник статически под Linux, указывая правильный путь к main.go
RUN CGO_ENABLED=0 GOOS=linux go build -o gateway ./cmd/danetki

# Этап 2: Финальный минимальный образ
FROM alpine:3.19

RUN apk add --no-cache ca-certificates

WORKDIR /root/

# Копируем скомпилированный бинарник из первого этапа
COPY --from=builder /app/gateway .

EXPOSE 8000

CMD ["./gateway"]