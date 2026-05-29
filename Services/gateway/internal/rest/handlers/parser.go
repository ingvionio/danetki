package handlers

import (
	"net/http"

	"github.com/danetka/gateway/internal/clients"
	parserpb "github.com/danetka/gateway/pkg/grpc/parser"
	"github.com/gin-gonic/gin"
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

	c.JSON(http.StatusOK, gin.H{
		"job_id":        response.JobId,
		"status":        response.Status.String(),
		"total_found":   response.TotalFound,
		"total_queued":  response.TotalQueued,
		"total_skipped": response.TotalSkipped,
		"error":         response.Error,
		"started_at":    response.StartedAt,
		"finished_at":   response.FinishedAt,
	})
}

func RegisterParserRoutes(group *gin.RouterGroup, grpcClients *clients.Clients) {
	handler := NewParserHandler(grpcClients)
	group.POST("/start", handler.Start)
	group.GET("/status", handler.Status)
}
