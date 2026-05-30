package main

import (
	"log"
	"time"

	"github.com/danetka/gateway/internal/clients"
	"github.com/danetka/gateway/internal/config"
	"github.com/danetka/gateway/internal/rest/handlers"
	"github.com/danetka/gateway/internal/rest/middleware"
	"github.com/gin-contrib/cors"
	"github.com/gin-gonic/gin"
)

func main() {
	cfg := config.Load()

	grpcClients, err := clients.New(cfg)
	if err != nil {
		log.Fatalf("failed to initialize gRPC clients: %v", err)
	}
	defer func() {
		if closeErr := grpcClients.Close(); closeErr != nil {
			log.Printf("failed to close gRPC clients: %v", closeErr)
		}
	}()

	router := gin.New()
	router.Use(cors.New(cors.Config{
		AllowOrigins:     []string{"*"},
		AllowMethods:     []string{"GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"},
		AllowHeaders:     []string{"Origin", "Content-Type", "Accept", "Authorization"},
		ExposeHeaders:    []string{"Content-Length"},
		AllowCredentials: false,
		MaxAge:           12 * time.Hour,
	}))
	router.Use(gin.Logger(), gin.Recovery())

	api := router.Group("/api/v1")
	registerRoutes(api, grpcClients)

	log.Printf("gateway listening on :%s", cfg.Port)
	if err := router.Run(":" + cfg.Port); err != nil {
		log.Fatalf("gateway stopped: %v", err)
	}
}

func registerRoutes(api *gin.RouterGroup, grpcClients *clients.Clients) {
	authGroup := api.Group("/auth")
	handlers.RegisterAuthRoutes(authGroup, grpcClients)

	authProtected := api.Group("/auth")
	authProtected.Use(middleware.Auth(grpcClients))
	handlers.RegisterProtectedAuthRoutes(authProtected, grpcClients)

	adminUsersGroup := api.Group("/admin/users")
	adminUsersGroup.Use(middleware.Auth(grpcClients))
	adminUsersGroup.Use(middleware.RequireAdmin())
	handlers.RegisterAdminUserRoutes(adminUsersGroup, grpcClients)

	puzzleGroup := api.Group("/puzzles")
	handlers.RegisterPuzzleRoutes(puzzleGroup, grpcClients)

	parserGroup := api.Group("/parser")
	parserGroup.Use(middleware.Auth(grpcClients))
	parserGroup.Use(middleware.RequireAdmin())
	handlers.RegisterParserRoutes(parserGroup, grpcClients)
}
