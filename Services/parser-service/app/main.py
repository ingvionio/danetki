import asyncio
import logging
import sys
from pathlib import Path

import grpc

SERVICE_ROOT = Path(__file__).resolve().parent.parent
GRPC_GENERATED_ROOT = SERVICE_ROOT / "grpc_generated"

for path in (SERVICE_ROOT, GRPC_GENERATED_ROOT):
    if str(path) not in sys.path:
        sys.path.insert(0, str(path))

from app.config import get_settings
from app.db.mongo import MongoRepository
from app.grpc.handler import ParserServicer
from app.kafka.producer import KafkaStoryProducer
from app.scraper.service import ParseJobService
from grpc_generated import parser_pb2_grpc

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger(__name__)


async def serve() -> None:
    settings = get_settings()
    mongo = MongoRepository(settings)
    kafka = KafkaStoryProducer(settings)
    job_service = ParseJobService(mongo, kafka, settings)

    await mongo.connect()

    server = grpc.aio.server()
    parser_pb2_grpc.add_ParserServiceServicer_to_server(
        ParserServicer(job_service, settings),
        server,
    )
    listen_address = f"{settings.grpc_host}:{settings.grpc_port}"
    server.add_insecure_port(listen_address)

    logger.info("Parser Service listening on %s", listen_address)
    await server.start()

    try:
        await server.wait_for_termination()
    finally:
        kafka.close()
        await mongo.disconnect()


def main() -> None:
    asyncio.run(serve())


if __name__ == "__main__":
    main()
