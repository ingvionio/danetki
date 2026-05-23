from __future__ import annotations

import asyncio
import json
import logging
from datetime import datetime, timezone
from typing import Any

from confluent_kafka import Producer

from app.config import Settings

logger = logging.getLogger(__name__)


class KafkaStoryProducer:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._producer = Producer(
            {
                "bootstrap.servers": settings.kafka_bootstrap_servers,
                "client.id": "parser-service",
            }
        )

    async def publish_raw_story(self, story: dict[str, Any]) -> None:
        payload = json.dumps(story, ensure_ascii=False)
        await asyncio.to_thread(self._produce, payload, story["story_id"])

    def _produce(self, payload: str, key: str) -> None:
        def delivery_callback(err: object, msg: object) -> None:
            if err is not None:
                logger.error("Kafka delivery failed for key=%s: %s", key, err)
                return
            logger.debug("Story published to Kafka: key=%s", key)

        self._producer.produce(
            topic=self._settings.kafka_topic_output,
            key=key.encode("utf-8"),
            value=payload.encode("utf-8"),
            callback=delivery_callback,
        )
        self._producer.flush(timeout=10)

    def close(self) -> None:
        self._producer.flush(timeout=5)
