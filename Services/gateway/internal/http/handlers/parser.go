package handlers

import (
	"context"
	"encoding/json"
	"net/http"
	"time"

	"github.com/ingvionio/danetki/internal/contracts/parser"
	"github.com/ingvionio/danetki/internal/discovery"

	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

type ParserHandler struct {
	reg discovery.Registry
}

func NewParserHandler(reg discovery.Registry) *ParserHandler {
	return &ParserHandler{reg: reg}
}

func (h *ParserHandler) Start(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req struct {
		Limit     int32  `json:"limit"`
		SourceUrl string `json:"source_url"`
	}

	if error := json.NewDecoder(r.Body).Decode(&req); error != nil {
		req.Limit = 0
		req.SourceUrl = ""
	}

	addr := "parser-service:50053"

	conn, error := grpc.Dial(addr, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if error != nil {
		http.Error(w, "Internal error", http.StatusInternalServerError)
		return
	}
	defer conn.Close()

	client := parser.NewParserServiceClient(conn)
	ctx, cancel := context.WithTimeout(r.Context(), time.Second*5)
	defer cancel()

	grpcResp, error := client.StartParsing(ctx, &parser.StartParsingRequest{
		Limit:     req.Limit,
		SourceUrl: req.SourceUrl,
	})
	if error != nil {
		http.Error(w, error.Error(), http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(grpcResp)
}

func (h *ParserHandler) Status(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	jobID := r.URL.Query().Get("job_id")

	addr := "parser-service:50053"

	conn, error := grpc.Dial(addr, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if error != nil {
		http.Error(w, "Internal error", http.StatusInternalServerError)
		return
	}
	defer conn.Close()

	client := parser.NewParserServiceClient(conn)
	ctx, cancel := context.WithTimeout(r.Context(), time.Second*5)
	defer cancel()

	grpcResp, error := client.GetStatus(ctx, &parser.GetStatusRequest{JobId: jobID})
	if error != nil {
		http.Error(w, error.Error(), http.StatusNotFound)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(grpcResp)
}