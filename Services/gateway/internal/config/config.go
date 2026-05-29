package config

import (
	"os"
)

const (
	DefaultPort             = "8000"
	DefaultAuthServiceAddr  = "auth-service:50051"
	DefaultPuzzleServiceAddr = "puzzle-service:50052"
	DefaultParserServiceAddr = "parser-service:50053"
)

type Config struct {
	Port              string
	AuthServiceAddr   string
	PuzzleServiceAddr string
	ParserServiceAddr string
}

func Load() Config {
	return Config{
		Port:              envOrDefault("PORT", DefaultPort),
		AuthServiceAddr:   envOrDefault("AUTH_SERVICE_ADDR", DefaultAuthServiceAddr),
		PuzzleServiceAddr: envOrDefault("PUZZLE_SERVICE_ADDR", DefaultPuzzleServiceAddr),
		ParserServiceAddr: envOrDefault("PARSER_SERVICE_ADDR", DefaultParserServiceAddr),
	}
}

func envOrDefault(key, fallback string) string {
	value := os.Getenv(key)
	if value == "" {
		return fallback
	}
	return value
}
