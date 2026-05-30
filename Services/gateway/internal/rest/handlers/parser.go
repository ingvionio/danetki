package handlers

import (
	"context"
	"net/http"
	"strconv"

	"github.com/danetka/gateway/internal/clients"
	parserpb "github.com/danetka/gateway/pkg/grpc/parser"
	puzzlepb "github.com/danetka/gateway/pkg/grpc/puzzle"
	"github.com/gin-gonic/gin"
)

const (
	defaultParserPage     = 1
	defaultParserPageSize = 20
	maxParserPageSize     = 50
)

type ParserHandler struct {
	clients *clients.Clients
}

func NewParserHandler(grpcClients *clients.Clients) *ParserHandler {
	return &ParserHandler{clients: grpcClients}
}

type startParserRequest struct {
	Limit     int32  `json:"limit"`
	SourceURL string `json:"source_url"`
}

func (h *ParserHandler) Start(c *gin.Context) {
	var body startParserRequest
	if err := c.ShouldBindJSON(&body); err != nil && c.Request.ContentLength > 0 {
		c.JSON(http.StatusBadRequest, ErrorResponse{Error: "invalid request body"})
		return
	}

	response, err := h.clients.Parser.StartParsing(c.Request.Context(), &parserpb.StartParsingRequest{
		Limit:     body.Limit,
		SourceUrl: body.SourceURL,
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	c.JSON(http.StatusAccepted, gin.H{
		"job_id":  response.JobId,
		"message": response.Message,
	})
}

func (h *ParserHandler) Status(c *gin.Context) {
	jobID := c.Query("job_id")

	response, err := h.clients.Parser.GetStatus(c.Request.Context(), &parserpb.GetStatusRequest{
		JobId: jobID,
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	c.JSON(http.StatusOK, jobToJSON(c.Request.Context(), h.clients, response))
}

func (h *ParserHandler) List(c *gin.Context) {
	page := parsePositiveInt(c.DefaultQuery("page", strconv.Itoa(defaultParserPage)), defaultParserPage)
	pageSize := parsePositiveInt(c.DefaultQuery("page_size", strconv.Itoa(defaultParserPageSize)), defaultParserPageSize)
	if pageSize > maxParserPageSize {
		pageSize = maxParserPageSize
	}

	response, err := h.clients.Parser.ListJobs(c.Request.Context(), &parserpb.ListJobsRequest{
		Page:     int32(page),
		PageSize: int32(pageSize),
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	jobs := make([]gin.H, 0, len(response.Jobs))
	for _, job := range response.Jobs {
		jobs = append(jobs, jobSummaryToJSON(c.Request.Context(), h.clients, job))
	}

	c.JSON(http.StatusOK, gin.H{
		"jobs":  jobs,
		"total": response.Total,
		"page":  response.Page,
	})
}

func RegisterParserRoutes(group *gin.RouterGroup, grpcClients *clients.Clients) {
	handler := NewParserHandler(grpcClients)
	group.POST("/start", handler.Start)
	group.GET("/jobs", handler.List)
	group.GET("/status", handler.Status)
}

func jobToJSON(ctx context.Context, grpcClients *clients.Clients, status *parserpb.GetStatusResponse) gin.H {
	return gin.H{
		"job_id":          status.JobId,
		"status":          status.Status.String(),
		"total_found":     status.TotalFound,
		"total_queued":    status.TotalQueued,
		"total_skipped":   status.TotalSkipped,
		"puzzles_created": countPuzzlesForJob(ctx, grpcClients, status.JobId),
		"error":           status.Error,
		"started_at":      status.StartedAt,
		"finished_at":     status.FinishedAt,
	}
}

func jobSummaryToJSON(ctx context.Context, grpcClients *clients.Clients, job *parserpb.JobSummary) gin.H {
	return gin.H{
		"job_id":          job.JobId,
		"status":          job.Status.String(),
		"source_url":      job.SourceUrl,
		"limit":           job.Limit,
		"total_found":     job.TotalFound,
		"total_queued":    job.TotalQueued,
		"total_skipped":   job.TotalSkipped,
		"puzzles_created": countPuzzlesForJob(ctx, grpcClients, job.JobId),
		"error":           job.Error,
		"started_at":      job.StartedAt,
		"finished_at":     job.FinishedAt,
	}
}

func countPuzzlesForJob(ctx context.Context, grpcClients *clients.Clients, jobID string) int32 {
	if jobID == "" {
		return 0
	}

	response, err := grpcClients.Puzzle.CountPuzzlesByJob(ctx, &puzzlepb.CountPuzzlesByJobRequest{
		JobId: jobID,
	})
	if err != nil {
		return 0
	}

	return response.Count
}
