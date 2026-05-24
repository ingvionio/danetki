package http

import (
	"net/http"

	"github.com/ingvionio/danetki/internal/discovery"
	"github.com/ingvionio/danetki/internal/http/handlers"
	"github.com/ingvionio/danetki/internal/http/middleware"
)

func NewRouter(reg discovery.Registry) http.Handler {
	mux := http.NewServeMux()

	authHandler := handlers.NewAuthHandler(reg)
	puzzleHandler := handlers.NewPuzzleHandler(reg)
	parserHandler := handlers.NewParserHandler(reg)

	authMiddleware := middleware.AuthMiddleware(reg)

	mux.HandleFunc("/auth/register", authHandler.Register)
	mux.HandleFunc("/auth/login", authHandler.Login)

	mux.Handle("/puzzle/", authMiddleware(http.HandlerFunc(puzzleHandler.HandlePuzzle)))

	mux.Handle("/parser/start", authMiddleware(http.HandlerFunc(parserHandler.Start)))
	mux.Handle("/parser/status", authMiddleware(http.HandlerFunc(parserHandler.Status)))

	return mux
}