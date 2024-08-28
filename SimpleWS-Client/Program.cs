using Microsoft.Extensions.Configuration;
using System.Net.WebSockets;
using System.Text;

class Program
{
    static async Task Main(string[] args)
    {
        // Load configuration from appsettings.json
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Get clientId from command line arguments or fallback to appsettings.json
        var clientId = args.Length > 0 ? args[0] : config["ClientId"];
        if (string.IsNullOrEmpty(clientId))
        {
            Console.WriteLine("ClientId is required. Please provide it via command line or appsettings.json.");
            return;
        }

        var serverUrl = config["WebSocketServerUrl"];
        var serverUri = new Uri($"{serverUrl}?clientId={clientId}");

        while (true)
        {
            using (var client = new ClientWebSocket())
            {
                try
                {
                    await client.ConnectAsync(serverUri, CancellationToken.None);
                    Console.WriteLine("Connected to the WebSocket server.");

                    var receiveTask = ReceiveMessages(client);
                    var heartbeatTask = SendHeartbeat(client, clientId);

                    // Send a test message
                    var message = "Hello, server!";
                    var bytes = Encoding.UTF8.GetBytes(message);
                    await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);

                    await Task.WhenAll(receiveTask, heartbeatTask);
                }
                catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                {
                    Console.WriteLine("Connection closed prematurely. Attempting to reconnect...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                    if (ex.InnerException is WebSocketException wsEx && wsEx.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                    {
                        Console.WriteLine("Server returned 409 Conflict. Exiting...");
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Unexpected error occurred. Attempting to reconnect...");
                    }
                }
            }

            // Wait before attempting to reconnect
            await Task.Delay(TimeSpan.FromSeconds(10));
        }
    }

    static async Task ReceiveMessages(ClientWebSocket client)
    {
        var buffer = new byte[1024 * 4];
        try
        {
            while (client.State == WebSocketState.Open)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine("Received: " + message);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    Console.WriteLine("Disconnected from the WebSocket server.");
                }
            }
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"WebSocketException: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }

    static async Task SendHeartbeat(ClientWebSocket client, string clientId)
    {
        try
        {
            while (client.State == WebSocketState.Open)
            {
                var heartbeatMessage = $"Heartbeat from {clientId}";
                var bytes = Encoding.UTF8.GetBytes(heartbeatMessage);
                var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);

                try
                {
                    await client.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
                    Console.WriteLine($"Sent heartbeat message from {clientId}.");
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"WebSocketException during heartbeat: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"WebSocketException: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception: {ex.Message}");
        }
    }
}
