FROM golang:1.22-alpine AS builder

RUN apk add --no-cache protobuf-dev git

RUN go install google.golang.org/protobuf/cmd/protoc-gen-go@v1.35.2 \
    && go install google.golang.org/grpc/cmd/protoc-gen-go-grpc@v1.5.1

WORKDIR /build

COPY Services/gateway/go.mod Services/gateway/go.sum* ./Services/gateway/
WORKDIR /build/Services/gateway
RUN go mod download 2>/dev/null || true

COPY contracts /build/contracts
COPY Services/gateway ./

RUN chmod +x generate.sh \
    && CONTRACTS_DIR=/build/contracts ./generate.sh \
    && go mod tidy \
    && CGO_ENABLED=0 GOOS=linux go build -o /gateway ./cmd/api

FROM alpine:3.20

RUN apk add --no-cache ca-certificates

COPY --from=builder /gateway /gateway

EXPOSE 8000

CMD ["/gateway"]
