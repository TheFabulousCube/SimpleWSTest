using System.Net.WebSockets;
using System.Text;

namespace ClientWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly string _clientId;
        private readonly string _serverUrl;
        private readonly int _heartbeatDelay;

        public Worker(ILogger<Worker> logger, string clientId, string serverUrl, int heartbeatDelay)
        {
            _logger = logger;
            _clientId = clientId;
            _serverUrl = serverUrl;
            _heartbeatDelay = heartbeatDelay;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var serverUri = new Uri($"{_serverUrl}?clientId={_clientId}");

            while (!stoppingToken.IsCancellationRequested)
            {
                using (var client = new ClientWebSocket())
                {
                    try
                    {
                        await client.ConnectAsync(serverUri, stoppingToken);
                        _logger.LogInformation("Connected to the WebSocket server.");

                        var receiveTask = ReceiveMessages(client, stoppingToken);
                        var heartbeatTask = SendHeartbeat(client, _clientId, _heartbeatDelay, stoppingToken);

                        // Send a test message
                        var message = "Hello, server!";
                        var bytes = Encoding.UTF8.GetBytes(message);
                        await client.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, stoppingToken);

                        await Task.WhenAll(receiveTask, heartbeatTask);
                    }
                    catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                    {
                        _logger.LogWarning("Connection closed prematurely. Attempting to reconnect...");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error occurred. Attempting to reconnect...");
                    }
                }

                // Wait before attempting to reconnect
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task ReceiveMessages(ClientWebSocket client, CancellationToken stoppingToken)
        {
            var buffer = new byte[1024 * 4];
            try
            {
                while (client.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
                {
                    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), stoppingToken);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        _logger.LogInformation("Received: " + message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", stoppingToken);
                        _logger.LogInformation("Disconnected from the WebSocket server.");
                    }
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocketException: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception: {Message}", ex.Message);
            }
        }

        private async Task SendHeartbeat(ClientWebSocket client, string clientId, int heartbeatDelay, CancellationToken stoppingToken)
        {
            try
            {
                while (client.State == WebSocketState.Open && !stoppingToken.IsCancellationRequested)
                {
                    var heartbeatMessage = $"Heartbeat from {clientId}";
                    var bytes = Encoding.UTF8.GetBytes(heartbeatMessage);
                    var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);

                    try
                    {
                        await client.SendAsync(arraySegment, WebSocketMessageType.Text, true, stoppingToken);
                        _logger.LogInformation($"Sent heartbeat message from {clientId}.");
                    }
                    catch (WebSocketException ex)
                    {
                        _logger.LogError(ex, "WebSocketException during heartbeat: {Message}", ex.Message);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(heartbeatDelay), stoppingToken);
                }
            }
            catch (WebSocketException ex)
            {
                _logger.LogError(ex, "WebSocketException: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception: {Message}", ex.Message);
            }
        }
    }
}
