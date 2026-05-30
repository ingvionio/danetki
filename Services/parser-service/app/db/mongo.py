from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from motor.motor_asyncio import AsyncIOMotorClient, AsyncIOMotorCollection, AsyncIOMotorDatabase

from app.config import Settings


class MongoRepository:
    def __init__(self, settings: Settings) -> None:
        self._settings = settings
        self._client: AsyncIOMotorClient | None = None
        self._collection: AsyncIOMotorCollection | None = None

    async def connect(self) -> None:
        self._client = AsyncIOMotorClient(self._settings.mongo_uri)
        database: AsyncIOMotorDatabase = self._client[self._settings.mongo_db_name]
        self._collection = database[self._settings.mongo_collection]

    async def disconnect(self) -> None:
        if self._client is not None:
            self._client.close()
            self._client = None
            self._collection = None

    async def save_story(self, story: dict[str, Any]) -> None:
        if self._collection is None:
            raise RuntimeError("MongoDB is not connected")

        document = {
            **story,
            "created_at": datetime.now(timezone.utc),
        }
        await self._collection.insert_one(document)

    async def save_job(self, job: dict[str, Any]) -> None:
        if self._collection is None:
            raise RuntimeError("MongoDB is not connected")

        jobs_collection = self._collection.database["parse_jobs"]
        await jobs_collection.insert_one(job)

    async def update_job(self, job_id: str, update: dict[str, Any]) -> None:
        if self._collection is None:
            raise RuntimeError("MongoDB is not connected")

        jobs_collection = self._collection.database["parse_jobs"]
        await jobs_collection.update_one({"job_id": job_id}, {"$set": update})

    async def get_job(self, job_id: str) -> dict[str, Any] | None:
        if self._collection is None:
            raise RuntimeError("MongoDB is not connected")

        jobs_collection = self._collection.database["parse_jobs"]
        return await jobs_collection.find_one({"job_id": job_id})

    async def get_latest_job(self) -> dict[str, Any] | None:
        if self._collection is None:
            raise RuntimeError("MongoDB is not connected")

        jobs_collection = self._collection.database["parse_jobs"]
        cursor = jobs_collection.find().sort("started_at", -1).limit(1)
        jobs = await cursor.to_list(length=1)
        if not jobs:
            return None
        return jobs[0]

    async def list_jobs(self, page: int, page_size: int) -> tuple[list[dict[str, Any]], int]:
        if self._collection is None:
            raise RuntimeError("MongoDB is not connected")

        jobs_collection = self._collection.database["parse_jobs"]
        total = await jobs_collection.count_documents({})
        skip = max(page - 1, 0) * page_size
        cursor = (
            jobs_collection.find({}, {"_id": 0})
            .sort("started_at", -1)
            .skip(skip)
            .limit(page_size)
        )
        jobs = await cursor.to_list(length=page_size)
        return jobs, total
