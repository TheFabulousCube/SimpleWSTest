using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Use HTTP only and bind to all network interfaces
builder.WebHost.UseUrls("http://0.0.0.0:5000");

var app = builder.Build();
app.UseWebSockets();

var webSocketConnections = new ConcurrentDictionary<string, WebSocket>();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var clientId = context.Request.Query["clientId"].ToString();
        if (string.IsNullOrEmpty(clientId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            return;
        }

        // Check for duplicate clientId before accepting the WebSocket connection
        if (webSocketConnections.ContainsKey(clientId))
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            Console.WriteLine($"Duplicate clientId: {clientId}, client refused");
            return;
        }

        var ws = await context.WebSockets.AcceptWebSocketAsync();
        if (webSocketConnections.TryAdd(clientId, ws))
        {
            await HandleWebSocketConnection(clientId, ws);
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.Conflict;
            Console.WriteLine($"Duplicate clientId: {clientId}, client refused");
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Duplicate clientId", CancellationToken.None);
        }
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

app.Run();

async Task HandleWebSocketConnection(string clientId, WebSocket ws)
{
    // Task to send messages to the client
    var sendTask = Task.Run(async () =>
    {
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var message = "The current time is : " + DateTime.Now.ToString("HH:mm:ss");
                await BroadcastMessage(message); // Broadcast to all clients
                await Task.Delay(10000); // Wait for 1 second before sending the next message
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
    });

    // Main loop to receive messages from the client
    try
    {
        while (true)
        {
            var buffer = new byte[1024 * 4];
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.WriteLine($"Received from {clientId}: {message}");
                await BroadcastMessage($"Echo from {clientId}: {message}"); // Broadcast received message to all clients
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
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
    finally
    {
        webSocketConnections.TryRemove(clientId, out _);
        ws.Dispose();
    }

    await sendTask; // Ensure the send task completes
}

async Task BroadcastMessage(string message)
{
    var clientIds = webSocketConnections.Keys.ToList();
    var clientIdsMessage = $"Connected clients: {string.Join(", ", clientIds)}";
    var fullMessage = $"{message}\n{clientIdsMessage}";
    var bytes = Encoding.UTF8.GetBytes(fullMessage);
    var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);

    foreach (var kvp in webSocketConnections)
    {
        var ws = kvp.Value;

        if (ws.State == WebSocketState.Open)
        {
            try
            {
                await ws.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"WebSocketException during broadcast: {ex.Message}");
            }
        }
    }
}
