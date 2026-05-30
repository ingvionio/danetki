import logging

import grpc

from app.config import Settings
from app.scraper.service import ParseJobService
from grpc_generated import parser_pb2, parser_pb2_grpc

logger = logging.getLogger(__name__)


class ParserServicer(parser_pb2_grpc.ParserServiceServicer):
    def __init__(self, job_service: ParseJobService, settings: Settings) -> None:
        self._job_service = job_service
        self._settings = settings

    async def StartParsing(
        self,
        request: parser_pb2.StartParsingRequest,
        context: grpc.aio.ServicerContext,
    ) -> parser_pb2.StartParsingResponse:
        source_url = request.source_url.strip()
        limit = request.limit if request.limit > 0 else self._settings.default_parse_limit

        try:
            job_id = await self._job_service.start_job(
                source_url=source_url,
                limit=limit,
            )
        except RuntimeError as exc:
            await context.abort(grpc.StatusCode.ALREADY_EXISTS, str(exc))
            raise
        except Exception as exc:
            logger.exception("Failed to start parsing job")
            await context.abort(grpc.StatusCode.INTERNAL, str(exc))
            raise

        return parser_pb2.StartParsingResponse(
            job_id=job_id,
            message="parsing started",
        )

    async def GetStatus(
        self,
        request: parser_pb2.GetStatusRequest,
        context: grpc.aio.ServicerContext,
    ) -> parser_pb2.GetStatusResponse:
        job_id = request.job_id.strip()
        job = await self._job_service.get_job_status(job_id)

        if job is None:
            await context.abort(grpc.StatusCode.NOT_FOUND, "Job not found")
            raise grpc.RpcError()

        return parser_pb2.GetStatusResponse(
            job_id=job["job_id"],
            status=self._map_status(job.get("status", "")),
            total_found=job.get("total_found", 0),
            total_queued=job.get("total_queued", 0),
            total_skipped=job.get("total_skipped", 0),
            error=job.get("error", ""),
            started_at=job.get("started_at", 0),
            finished_at=job.get("finished_at", 0),
        )

    async def ListJobs(
        self,
        request: parser_pb2.ListJobsRequest,
        context: grpc.aio.ServicerContext,
    ) -> parser_pb2.ListJobsResponse:
        page_size = min(max(request.page_size, 1), 50)
        page = max(request.page, 1)

        jobs, total = await self._job_service.list_jobs(page, page_size)

        response = parser_pb2.ListJobsResponse(total=total, page=page)
        for job in jobs:
            response.jobs.append(
                parser_pb2.JobSummary(
                    job_id=job["job_id"],
                    status=self._map_status(job.get("status", "")),
                    source_url=job.get("source_url", ""),
                    limit=job.get("limit", 0),
                    total_found=job.get("total_found", 0),
                    total_queued=job.get("total_queued", 0),
                    total_skipped=job.get("total_skipped", 0),
                    error=job.get("error", ""),
                    started_at=job.get("started_at", 0),
                    finished_at=job.get("finished_at", 0),
                )
            )

        return response

    @staticmethod
    def _map_status(status: str) -> int:
        mapping = {
            "running": parser_pb2.STATUS_RUNNING,
            "done": parser_pb2.STATUS_DONE,
            "failed": parser_pb2.STATUS_FAILED,
        }
        return mapping.get(status, parser_pb2.STATUS_UNKNOWN)
