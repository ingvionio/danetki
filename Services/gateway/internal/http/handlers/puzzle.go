package handlers

import (
	"context"
	"encoding/json"
	"net/http"
	"strings"
	"time"

	"github.com/ingvionio/danetki/internal/contracts/puzzle"
	"github.com/ingvionio/danetki/internal/discovery"

	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

type PuzzleHandler struct {
	reg discovery.Registry
}

func NewPuzzleHandler(reg discovery.Registry) *PuzzleHandler {
	return &PuzzleHandler{reg: reg}
}

func (h *PuzzleHandler) HandlePuzzle(w http.ResponseWriter, r *http.Request) {
	addr := "danetka-puzzle:50052"

	conn, error := grpc.Dial(addr, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if error != nil {
		http.Error(w, "Internal error", http.StatusInternalServerError)
		return
	}
	defer conn.Close()

	client := puzzle.NewPuzzleServiceClient(conn)
	ctx, cancel := context.WithTimeout(r.Context(), time.Second*5)
	defer cancel()

	if r.Method == http.MethodGet && r.URL.Path == "/puzzle/random" {
		grpcResp, error := client.GetRandomPuzzle(ctx, &puzzle.GetRandomPuzzleRequest{})
		if error != nil {
			http.Error(w, error.Error(), http.StatusInternalServerError)
			return
		}
		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(grpcResp)
		return
	}

	if r.Method == http.MethodGet && strings.HasPrefix(r.URL.Path, "/puzzle/") {
		id := strings.TrimPrefix(r.URL.Path, "/puzzle/")
		if id == "" {
			http.Error(w, "Missing puzzle ID", http.StatusBadRequest)
			return
		}

		grpcResp, error := client.GetPuzzleById(ctx, &puzzle.GetPuzzleByIdRequest{PuzzleId: id})
		if error != nil {
			http.Error(w, error.Error(), http.StatusNotFound)
			return
		}
		w.Header().Set("Content-Type", "application/json")
		json.NewEncoder(w).Encode(grpcResp)
		return
	}

	http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
}