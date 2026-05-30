from __future__ import annotations

import asyncio
import logging
import time
import uuid
from datetime import datetime, timezone
from typing import Any

from app.config import Settings
from app.db.mongo import MongoRepository
from app.kafka.producer import KafkaStoryProducer
from app.scraper.factroom import ScraperRegistry
from app.scraper.validator import ParsedStory

logger = logging.getLogger(__name__)

JOB_STATUS_RUNNING = "running"
JOB_STATUS_DONE = "done"
JOB_STATUS_FAILED = "failed"


class ParseJobService:
    def __init__(
        self,
        mongo: MongoRepository,
        kafka: KafkaStoryProducer,
        settings: Settings,
    ) -> None:
        self._mongo = mongo
        self._kafka = kafka
        self._settings = settings
        self._tasks: dict[str, asyncio.Task[None]] = {}

    async def start_job(self, source_url: str, limit: int) -> str:
        if self._has_running_job():
            raise RuntimeError("parsing already in progress")

        job_id = str(uuid.uuid4())
        started_at = int(time.time())
        job_document: dict[str, Any] = {
            "job_id": job_id,
            "source_url": source_url,
            "limit": limit,
            "status": JOB_STATUS_RUNNING,
            "total_found": 0,
            "total_queued": 0,
            "total_skipped": 0,
            "error": "",
            "started_at": started_at,
            "finished_at": 0,
        }
        await self._mongo.save_job(job_document)

        task = asyncio.create_task(self._run_job(job_id, source_url, limit, started_at))
        self._tasks[job_id] = task
        task.add_done_callback(lambda _: self._tasks.pop(job_id, None))

        return job_id

    async def get_job_status(self, job_id: str) -> dict[str, Any] | None:
        if job_id:
            return await self._mongo.get_job(job_id)
        return await self._mongo.get_latest_job()

    async def list_jobs(self, page: int, page_size: int) -> tuple[list[dict[str, Any]], int]:
        return await self._mongo.list_jobs(page, page_size)

    def _has_running_job(self) -> bool:
        return bool(self._tasks)

    async def _run_job(
        self,
        job_id: str,
        source_url: str,
        limit: int,
        started_at: int,
    ) -> None:
        try:
            stories = await ScraperRegistry.fetch_stories(
                source_url=source_url,
                limit=limit,
                settings=self._settings,
            )
            queued_count = 0
            skipped_count = 0

            for story in stories:
                saved = await self._persist_story(job_id, story)
                if saved:
                    queued_count += 1
                    continue
                skipped_count += 1

            await self._mongo.update_job(
                job_id,
                {
                    "status": JOB_STATUS_DONE,
                    "total_found": len(stories),
                    "total_queued": queued_count,
                    "total_skipped": skipped_count,
                    "error": "",
                    "finished_at": int(time.time()),
                },
            )
        except Exception as exc:
            logger.exception("Parse job failed: job_id=%s", job_id)
            await self._mongo.update_job(
                job_id,
                {
                    "status": JOB_STATUS_FAILED,
                    "error": str(exc),
                    "finished_at": int(time.time()),
                },
            )

    async def _persist_story(self, job_id: str, story: ParsedStory) -> bool:
        story_id = str(uuid.uuid4())
        parsed_at = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")
        payload: dict[str, Any] = {
            "story_id": story_id,
            "job_id": job_id,
            "text": story.text,
            "source_url": story.source_url,
            "source_title": story.source_title,
            "parsed_at": parsed_at,
            "retry_count": 0,
        }

        try:
            await self._mongo.save_story(payload)
            await self._kafka.publish_raw_story(payload)
        except Exception:
            logger.exception("Failed to persist story: story_id=%s", story_id)
            return False

        return True
