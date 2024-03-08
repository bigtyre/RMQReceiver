using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
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

        Console.WriteLine("RabbitMQ Receiver");

        var multiValueParameters = new List<string>() { "topic" };
        bool activeParamIsMultivalue = false;
        string? activeParam = null;
        string virtualHost = "";
        List<string> topics = new();

        for (int i = 0;i < args.Length; i++)
        {
            var arg = args[i];

            if (arg.StartsWith("--"))
            {
                if (activeParam is not null)
                {
                    if (activeParamIsMultivalue is false)
                    {
                        Console.Write($"Error: Parameter value not provided for {activeParam}");
                        return;
                    }
                }

                activeParam = arg;
                activeParamIsMultivalue = multiValueParameters.Contains(activeParam.TrimStart('-'));
            }
            else
            {
                switch (activeParam?.TrimStart('-'))
                {
                    case "vhost": virtualHost = arg; break;
                    case "topic": topics.Add(arg); break;
                }
                if (activeParamIsMultivalue is false)
                {
                    activeParam = null;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(virtualHost)) { 
            Console.WriteLine("Enter virtual host:");
            Console.Write("/");
            virtualHost = Console.ReadLine() ?? "";
            if (string.IsNullOrEmpty(virtualHost))
            {
                virtualHost = "/";
            }
        }

        var uriBuilder = new UriBuilder(rabbitMqUri)
        {
            Path = virtualHost
        };
        rabbitMqUri = uriBuilder.Uri;


        uriBuilder = new UriBuilder(rabbitMqUri)
        {
            Password = "******"
        };
        var displayUri = uriBuilder.Uri;


        if (topics.Count < 1) { 
            Console.WriteLine("Enter one or more topics to listen to, separated by spaces (optional):");
            var topicString = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(topicString)) topicString = "#"; // Listen to all message topics
            topics.AddRange(topicString.Split(' '));
        }

        Console.WriteLine($"Connecting to {displayUri}");

        // Set up the RabbitMQ connection
        var factory = new ConnectionFactory() { Uri = rabbitMqUri };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclarePassive(exchangeName);

        // Define listener that outputs every message sent to every queue
        var messageQueue = channel.QueueDeclare();

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
        var numTopics = topics.Count;
        if (numTopics > 0)
        {
            string topicsText = ReplaceLastOccurrence(string.Join(", ", numTopics), ",", " &");
            Console.Write(" on topics " + string.Join(", ", topics));
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

        Console.Write($"{DateTime.Now:yyyy-MM-dd h:mm:ss tt}   ");
        var colour = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{routingKey.PadLeft(maxKeyLength)}   ");
        Console.ForegroundColor = colour;
        Console.WriteLine($"{message}");
    }
}
