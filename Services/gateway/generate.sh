#!/bin/sh
set -eu

CONTRACTS_DIR="${CONTRACTS_DIR:-/build/contracts}"

mkdir -p pkg/grpc/auth pkg/grpc/puzzle pkg/grpc/parser

protoc --proto_path="${CONTRACTS_DIR}" \
  --go_out=pkg/grpc/auth --go_opt=paths=source_relative \
  --go-grpc_out=pkg/grpc/auth --go-grpc_opt=paths=source_relative \
  --go_opt=Mauth.proto=github.com/danetka/gateway/pkg/grpc/auth \
  --go-grpc_opt=Mauth.proto=github.com/danetka/gateway/pkg/grpc/auth \
  "${CONTRACTS_DIR}/auth.proto"

protoc --proto_path="${CONTRACTS_DIR}" \
  --go_out=pkg/grpc/puzzle --go_opt=paths=source_relative \
  --go-grpc_out=pkg/grpc/puzzle --go-grpc_opt=paths=source_relative \
  --go_opt=Mpuzzle.proto=github.com/danetka/gateway/pkg/grpc/puzzle \
  --go-grpc_opt=Mpuzzle.proto=github.com/danetka/gateway/pkg/grpc/puzzle \
  "${CONTRACTS_DIR}/puzzle.proto"

protoc --proto_path="${CONTRACTS_DIR}" \
  --go_out=pkg/grpc/parser --go_opt=paths=source_relative \
  --go-grpc_out=pkg/grpc/parser --go-grpc_opt=paths=source_relative \
  --go_opt=Mparser.proto=github.com/danetka/gateway/pkg/grpc/parser \
  --go-grpc_opt=Mparser.proto=github.com/danetka/gateway/pkg/grpc/parser \
  "${CONTRACTS_DIR}/parser.proto"

echo "gRPC code generated in pkg/grpc/"
