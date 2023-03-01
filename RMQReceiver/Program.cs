using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;

namespace RMQReceiver;

class Program
{
    static void Main(string[] args)
    {
        // Get application configuration
        const string exchangeName = "default";
        var settings = GetAppSettings(args);
        var rabbitMqUri = settings.RabbitMqUri ?? throw new ConfigurationException($"{nameof(AppSettings.RabbitMqUri)} not configured.");
        var topics = settings.Topics;

        // Set up the RabbitMQ connection
        var factory = new ConnectionFactory() { Uri = rabbitMqUri };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclarePassive(exchangeName);

        // Define listener that outputs every message sent to every queue
        var messageQueue = channel.QueueDeclare();
        if (topics.Count == 0)
        {
            topics.Add("#");
        }

        foreach (string topic in topics)
        {
            channel.QueueBind(messageQueue.QueueName, exchangeName, topic);
        }

        // Set up the RabbitMQ consumer to raise events when messages are received.
        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += Message_Received;
        channel.BasicConsume(messageQueue.QueueName, true, consumer);

        // Output a summary of what we're listening to and how to quit
        Console.Write($"Listening for messages on {factory.Endpoint} on host {factory.VirtualHost}");
        if (topics.Count > 0)
        {
            string topicsText = ReplaceLastOccurrence(string.Join(", ", topics.Count), ",", " &");
            Console.Write(" on topics " + topics);
        }
        Console.WriteLine();
        Console.WriteLine("Press CTRL+C to quit.");

        // Wait for a cancel signal from the console
        var quitEvent = new ManualResetEvent(false);

        Console.CancelKeyPress += (s, e) =>
        {
            Console.WriteLine("Quitting");
            quitEvent.Set();
        };

        quitEvent.WaitOne();
        Console.WriteLine("Application exited");
    }

    public static string ReplaceLastOccurrence(string Source, string Find, string Replace)
    {
        int place = Source.LastIndexOf(Find);

        if (place == -1)
            return Source;

        string result = Source.Remove(place, Find.Length).Insert(place, Replace);
        return result;
    }


    private static AppSettings GetAppSettings(string[] args)
    {
        IConfiguration Configuration = GetConfiguration(args);
        var settings = new AppSettings();
        Configuration.Bind(settings);
        return settings;
    }

    private static IConfiguration GetConfiguration(string[] args)
    {
        IConfiguration Configuration = new ConfigurationBuilder()
                        .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
                        .AddCommandLine(args)
                        .AddEnvironmentVariables()
#if DEBUG
                .AddUserSecrets<Program>()
#endif
                .Build();
        return Configuration;
    }

    private static int maxKeyLength = 0;

    private static void Message_Received(object? sender, BasicDeliverEventArgs e)
    {
        var body = e.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var routingKey = e.RoutingKey;

        int routingKeyLength = routingKey.Length;
        if (routingKeyLength > maxKeyLength)
        {
            maxKeyLength = routingKeyLength;
        }

        Console.Write($"{DateTime.Now:G}   ");
        var colour = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{routingKey.PadLeft(maxKeyLength)}   ");
        Console.ForegroundColor = colour;
        Console.WriteLine($"{message}");
    }
}
