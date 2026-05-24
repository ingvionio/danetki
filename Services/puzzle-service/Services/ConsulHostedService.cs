using System;
using System.Threading;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PuzzleService.Services
{
    public class ConsulHostedService : IHostedService
    {
        private readonly IConsulClient _client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConsulHostedService> _logger;
        private string _serviceId;

        public ConsulHostedService(IConsulClient client, IConfiguration configuration, ILogger<ConsulHostedService> logger)
        {
            _client = client;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Берем настройки из окружения Docker-контейнера
            var consulAddr = _configuration["CONSUL_ADDR"] ?? "consul:8500";
            var grpcPort = int.Parse(_configuration["GRPC_PORT"] ?? "50052");
            
            // Уникальный ID сервиса внутри Consul (имя контейнера + порт)
            _serviceId = $"puzzle-service-danetka-puzzle-{grpcPort}";

            var registration = new AgentServiceRegistration
            {
                ID = _serviceId,
                Name = "puzzle-service", // Имя группы сервисов (по нему Go-шлюз будет искать бэкенд)
                Address = "danetka-puzzle", // Имя хоста/контейнера в Docker-сети
                Port = grpcPort,
                Tags = new[] { "grpc", "danetki" }
            };

            _logger.LogInformation($"Registering service '{registration.Name}' in Consul at {consulAddr}...");
            
            try
            {
                await _client.Agent.ServiceDeregister(_serviceId, cancellationToken);
                await _client.Agent.ServiceRegister(registration, cancellationToken);
                _logger.LogInformation("Service successfully registered in Consul.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register service in Consul.");
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Deregistering service '{_serviceId}' from Consul...");
            try
            {
                await _client.Agent.ServiceDeregister(_serviceId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to deregister service from Consul.");
            }
        }
    }
}