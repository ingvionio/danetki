FROM python:3.12-slim AS builder

WORKDIR /build

ARG PIP_INDEX_URL=https://pypi.org/simple
ARG PIP_TRUSTED_HOST=pypi.org files.pythonhosted.org
ENV PIP_INDEX_URL=${PIP_INDEX_URL} \
    PIP_TRUSTED_HOST=${PIP_TRUSTED_HOST} \
    PIP_DEFAULT_TIMEOUT=300 \
    PIP_RETRIES=10 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

COPY requirements-runtime.txt .
RUN set -e; \
    for attempt in 1 2 3 4 5; do \
      pip install --no-cache-dir --prefer-binary --prefix=/install -r requirements-runtime.txt && exit 0; \
      echo "pip install attempt ${attempt}/5 failed, retrying..."; \
      sleep $((attempt * 15)); \
    done; \
    exit 1

ENV PYTHONPATH=/install/lib/python3.12/site-packages
RUN python -c "\
import importlib; \
mods = ('grpc', 'google.protobuf', 'motor', 'motor.motor_asyncio', \
        'confluent_kafka', 'httpx', 'bs4', 'pydantic', 'pydantic_settings'); \
[importlib.import_module(m) for m in mods]; \
print('python runtime deps ok')"


FROM python:3.12-slim AS runtime

WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    librdkafka1 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=builder /install /usr/local
COPY grpc_generated/ /app/grpc_generated/
COPY app/ /app/app/

ENV PYTHONPATH=/app
ENV PYTHONUNBUFFERED=1

RUN python -c "\
from grpc_generated import parser_pb2, parser_pb2_grpc; \
from app.config import Settings; \
from app.grpc.handler import ParserServicer; \
print('parser-service image ok')"

EXPOSE 50053

CMD ["python", "-m", "app.main"]
