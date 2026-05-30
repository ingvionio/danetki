FROM golang:1.22-alpine AS builder

RUN apk add --no-cache ca-certificates protobuf-dev git \
    && go install google.golang.org/protobuf/cmd/protoc-gen-go@v1.35.2 \
    && go install google.golang.org/grpc/cmd/protoc-gen-go-grpc@v1.5.1

ENV PATH="/go/bin:${PATH}"

WORKDIR /build/Services/gateway

COPY Services/gateway/go.mod Services/gateway/go.sum* ./
RUN go mod download

WORKDIR /build
COPY contracts /build/contracts
COPY Services/gateway /build/Services/gateway

WORKDIR /build/Services/gateway
RUN chmod +x generate.sh \
    && CONTRACTS_DIR=/build/contracts ./generate.sh \
    && go mod tidy \
    && CGO_ENABLED=0 GOOS=linux go build -o /gateway ./cmd/api \
    && test -x /gateway

FROM scratch

COPY --from=builder /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/
COPY --from=builder /gateway /gateway

EXPOSE 8000

ENTRYPOINT ["/gateway"]
