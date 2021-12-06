using System.Text;
using blogdeployments.power.Handler;
using MediatR;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace blogdeployments.power.Service
{
    public class CompleteDeploymentRabbitMQService : BackgroundService
    {
        private readonly ILogger<RegisterDeploymentRabbitMQService> _logger;
        private readonly IOptions<RabbitMqConfiguration> _rabbitMqOptions;
        private readonly IMediator _mediator;
        private IConnection _connection;
        private IModel _channel;
        private string _queueName;

        public CompleteDeploymentRabbitMQService(
            ILogger<RegisterDeploymentRabbitMQService> logger,
            IOptions<RabbitMqConfiguration> rabbitMqOptions,
            IMediator mediator)
        {
            _logger = logger;
            _rabbitMqOptions = rabbitMqOptions;
            _mediator = mediator;

            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqOptions.Value.Hostname
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(exchange: "deployments_exchange", type: "direct");
            _queueName = _channel.QueueDeclare().QueueName;

            // todo: the only difference is the routingkey, it seems that 
            // - this can all get to a templated baseclass
            // - and/or receive different Pocos in the Send() call
            _channel.QueueBind(queue: _queueName,
                                  exchange: "deployments_exchange",
                                  routingKey: "completeDeployment");
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine(" [x] Received {0}", message);
                // todo: deserialize to Domain Object Deployment
                HandleMessage(message);
            };
            _channel.BasicConsume(queue: _queueName, autoAck: true, consumer: consumer);

            return Task.CompletedTask;
        }

        private void HandleMessage(string message)
        {
            // note: return value swallowed
            _mediator.Send(new PowerOff());
        }

        public override void Dispose()
        {
            _channel.Close();
            _connection.Close();
            base.Dispose();
        }
    }
}