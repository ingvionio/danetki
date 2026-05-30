package handlers

import (
	"net/http"
	"strconv"

	"github.com/danetka/gateway/internal/clients"
	puzzlepb "github.com/danetka/gateway/pkg/grpc/puzzle"
	"github.com/gin-gonic/gin"
)

const (
	defaultPuzzlePage     = 1
	defaultPuzzlePageSize = 10
	maxPuzzlePageSize     = 50
)

type PuzzleHandler struct {
	clients *clients.Clients
}

func NewPuzzleHandler(grpcClients *clients.Clients) *PuzzleHandler {
	return &PuzzleHandler{clients: grpcClients}
}

func (h *PuzzleHandler) GetRandom(c *gin.Context) {
	response, err := h.clients.Puzzle.GetRandomPuzzle(c.Request.Context(), &puzzlepb.GetRandomPuzzleRequest{})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	c.JSON(http.StatusOK, puzzleToJSON(response))
}

func (h *PuzzleHandler) GetByID(c *gin.Context) {
	puzzleID := c.Param("id")
	if puzzleID == "" {
		c.JSON(http.StatusBadRequest, ErrorResponse{Error: "puzzle id is required"})
		return
	}

	response, err := h.clients.Puzzle.GetPuzzleById(c.Request.Context(), &puzzlepb.GetPuzzleByIdRequest{
		PuzzleId: puzzleID,
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	c.JSON(http.StatusOK, puzzleToJSON(response))
}

func (h *PuzzleHandler) RevealAnswer(c *gin.Context) {
	puzzleID := c.Param("id")
	if puzzleID == "" {
		c.JSON(http.StatusBadRequest, ErrorResponse{Error: "puzzle id is required"})
		return
	}

	response, err := h.clients.Puzzle.RevealPuzzleAnswer(c.Request.Context(), &puzzlepb.GetPuzzleByIdRequest{
		PuzzleId: puzzleID,
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	c.JSON(http.StatusOK, gin.H{
		"puzzle_id":   response.PuzzleId,
		"hidden_part": response.HiddenPart,
	})
}

func (h *PuzzleHandler) List(c *gin.Context) {
	page := parsePositiveInt(c.DefaultQuery("page", strconv.Itoa(defaultPuzzlePage)), defaultPuzzlePage)
	pageSize := parsePositiveInt(c.DefaultQuery("page_size", strconv.Itoa(defaultPuzzlePageSize)), defaultPuzzlePageSize)
	if pageSize > maxPuzzlePageSize {
		pageSize = maxPuzzlePageSize
	}

	response, err := h.clients.Puzzle.ListPuzzles(c.Request.Context(), &puzzlepb.ListPuzzlesRequest{
		Page:     int32(page),
		PageSize: int32(pageSize),
	})
	if err != nil {
		WriteGRPCError(c, err)
		return
	}

	puzzles := make([]gin.H, 0, len(response.Puzzles))
	for _, puzzle := range response.Puzzles {
		puzzles = append(puzzles, puzzleToJSON(puzzle))
	}

	c.JSON(http.StatusOK, gin.H{
		"puzzles": puzzles,
		"total":   response.Total,
		"page":    response.Page,
	})
}

func RegisterPuzzleRoutes(group *gin.RouterGroup, grpcClients *clients.Clients) {
	handler := NewPuzzleHandler(grpcClients)
	group.GET("/random", handler.GetRandom)
	group.GET("/:id/answer", handler.RevealAnswer)
	group.GET("/:id", handler.GetByID)
	group.GET("", handler.List)
}

func puzzleToJSON(puzzle *puzzlepb.PuzzleResponse) gin.H {
	return gin.H{
		"puzzle_id":  puzzle.PuzzleId,
		"open_part":  puzzle.OpenPart,
		"source_url": puzzle.SourceUrl,
		"created_at": puzzle.CreatedAt,
	}
}

func parsePositiveInt(raw string, fallback int) int {
	value, err := strconv.Atoi(raw)
	if err != nil || value < 1 {
		return fallback
	}
	return value
}
