package http

import (
	"net/http"

	"github.com/ingvionio/danetki/internal/config"
	"github.com/ingvionio/danetki/internal/discovery"
	"github.com/ingvionio/danetki/internal/http/handlers"
	"github.com/ingvionio/danetki/internal/http/middleware"
)

func NewRouter(reg discovery.Registry, cfg *config.Config) http.Handler {
	mux := http.NewServeMux()

	authHandler := handlers.NewAuthHandler(reg, cfg.AuthServiceAddr)
	puzzleHandler := handlers.NewPuzzleHandler(reg, cfg.PuzzleServiceAddr)
	parserHandler := handlers.NewParserHandler(reg, cfg.ParserServiceAddr)

	authMiddleware := middleware.AuthMiddleware(reg, cfg.AuthServiceAddr)

	mux.HandleFunc("/auth/register", authHandler.Register)
	mux.HandleFunc("/auth/login", authHandler.Login)

	mux.Handle("/puzzle/", authMiddleware(http.HandlerFunc(puzzleHandler.HandlePuzzle)))

	mux.Handle("/parser/start", authMiddleware(http.HandlerFunc(parserHandler.Start)))
	mux.Handle("/parser/status", authMiddleware(http.HandlerFunc(parserHandler.Status)))

	return middleware.CORSMiddleware(mux)
}
