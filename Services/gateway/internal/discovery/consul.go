package discovery

import (
	"fmt"
	"math/rand"

	"github.com/hashicorp/consul/api"
)

type Registry interface {
	Register(serviceName, serviceID, host string, port int) error
	Deregister(serviceID string) error
	Discover(serviceName string) (string, error)
}

type ConsulRegistry struct {
	client *api.Client
}

func NewConsulRegistry(addr string) (*ConsulRegistry, error) {
	cfg := api.DefaultConfig()
	cfg.Address = addr
	client, err := api.NewClient(cfg)
	if err != nil {
		return nil, err
	}
	return &ConsulRegistry{client: client}, nil
}

func (r *ConsulRegistry) Register(serviceName, serviceID, host string, port int) error {
	registration := &api.AgentServiceRegistration{
		ID:      serviceID,
		Name:    serviceName,
		Address: host,
		Port:    port,
		Check: &api.AgentServiceCheck{
			TCP:                            fmt.Sprintf("%s:%d", host, port),
			Interval:                       "10s",
			Timeout:                        "5s",
			DeregisterCriticalServiceAfter: "1m",
		},
	}

	return r.client.Agent().ServiceRegister(registration)
}

func (r *ConsulRegistry) Deregister(serviceID string) error {
	return r.client.Agent().ServiceDeregister(serviceID)
}

func (r *ConsulRegistry) Discover(serviceName string) (string, error) {
	services, _, err := r.client.Health().Service(serviceName, "", true, nil)
	if err != nil {
		return "", err
	}

	if len(services) == 0 {
		return "", fmt.Errorf("service %s not found in Consul", serviceName)
	}

	idx := rand.Intn(len(services))
	service := services[idx].Service

	return fmt.Sprintf("%s:%d", service.Address, service.Port), nil
}