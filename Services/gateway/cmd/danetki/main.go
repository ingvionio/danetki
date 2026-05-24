package main

import (
	"log"
	"net/http"

	"github.com/ingvionio/danetki/internal/config"
	"github.com/ingvionio/danetki/internal/discovery"
	gatewayHttp "github.com/ingvionio/danetki/internal/http"
)

func main() {
	cfg := config.Load()

	registry, err := discovery.NewConsulRegistry(cfg.ConsulAddr)
	if err != nil {
		log.Fatalf("Failed to connect to Consul: %v", err)
	}
	log.Printf("Connected to Consul at %s", cfg.ConsulAddr)

	err = registry.Register(cfg.ServiceName, cfg.ServiceID, cfg.ServiceHost, cfg.ServicePort)
	if err != nil {
		log.Printf("Warning: Failed to register in Consul: %v", err)
	} else {
		log.Printf("Successfully registered in Consul as '%s'", cfg.ServiceName)
	}

	router := gatewayHttp.NewRouter(registry)

	log.Printf("API Gateway is running on port :%s", cfg.Port)
	if err := http.ListenAndServe(":"+cfg.Port, router); err != nil {
		log.Fatalf("Failed to start server: %v", err)
	}
}