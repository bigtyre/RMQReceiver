using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RMQReceiver
{
    class Program
    {
        static void Main(string[] args)
        {
            const string exchangeName = "default";

            // Set up the connection
            var factory = CreateConnectionFactory();

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.ExchangeDeclarePassive(exchangeName);


            // Define listener that outputs every message sent to every queue
            var messageQueue = channel.QueueDeclare(queue: "");
            channel.QueueBind(messageQueue.QueueName, exchangeName, "electricity.power-flow.readings");
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += Message_Received;
            channel.BasicConsume(messageQueue.QueueName, true, consumer);


            Console.WriteLine("Listening for messages. Press Ctrl+C to quit.");
            while (true) {
                Console.ReadKey();
            }
        }

        private static ConnectionFactory CreateConnectionFactory()
        {
            IConfiguration Configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            string hostName = Configuration["RabbitMq:Host"] ?? throw new Exception("Configuration value missing");
            string username = Configuration["RabbitMq:User"];
            string password = Configuration["RabbitMq:Password"];
            int port = int.Parse(Configuration["RabbitMq:Port"]);

            var factory = new ConnectionFactory()
            {
                ///Ssl = new SslOption(hostName, "", enabled: true),
                HostName = hostName,
                UserName = username,
                Password = password,
                Port = port
            };

            return factory;
        }

        private static void Message_Received(object sender, BasicDeliverEventArgs e)
        {
            var body = e.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Console.WriteLine($"Received message on {e.RoutingKey}: {message}");
        }
    }
}
