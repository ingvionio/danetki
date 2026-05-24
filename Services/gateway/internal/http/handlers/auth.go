package handlers

import (
	"context"
	"encoding/json"
	"net/http"
	"time"

	"github.com/ingvionio/danetki/internal/contracts/auth"
	"github.com/ingvionio/danetki/internal/discovery"

	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

type AuthHandler struct {
	reg discovery.Registry
}

func NewAuthHandler(reg discovery.Registry) *AuthHandler {
	return &AuthHandler{reg: reg}
}

func (h *AuthHandler) Register(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req struct {
		Email    string `json:"email"`
		Password string `json:"password"`
		Username string `json:"username"`
	}

	if error := json.NewDecoder(r.Body).Decode(&req); error != nil {
		http.Error(w, "Bad request", http.StatusBadRequest)
		return
	}

	authServiceAddr := "danetka-auth:8080"

	conn, error := grpc.Dial(authServiceAddr, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if error != nil {
		http.Error(w, "Internal error", http.StatusInternalServerError)
		return
	}
	defer conn.Close()

	client := auth.NewAuthServiceClient(conn)
	ctx, cancel := context.WithTimeout(r.Context(), time.Second*5)
	defer cancel()

	grpcResp, error := client.Register(ctx, &auth.RegisterRequest{
		Email:    req.Email,
		Password: req.Password,
		Username: req.Username,
	})
	if error != nil {
		http.Error(w, error.Error(), http.StatusBadRequest)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(grpcResp)
}

func (h *AuthHandler) Login(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
		return
	}

	var req struct {
		Email    string `json:"email"`
		Password string `json:"password"`
	}

	if error := json.NewDecoder(r.Body).Decode(&req); error != nil {
		http.Error(w, "Bad request", http.StatusBadRequest)
		return
	}

	authServiceAddr := "danetka-auth:8080"

	conn, error := grpc.Dial(authServiceAddr, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if error != nil {
		http.Error(w, "Internal error", http.StatusInternalServerError)
		return
	}
	defer conn.Close()

	client := auth.NewAuthServiceClient(conn)
	ctx, cancel := context.WithTimeout(r.Context(), time.Second*5)
	defer cancel()

	grpcResp, error := client.Login(ctx, &auth.LoginRequest{
		Email:    req.Email,
		Password: req.Password,
	})
	if error != nil {
		http.Error(w, error.Error(), http.StatusUnauthorized)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(grpcResp)
}