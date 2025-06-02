using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;



namespace TCPWebServer
{
    public class TCPWebServer
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _webRoot;
        private readonly HashSet<string> _allowedExtensions;
        private TcpListener _listener;
        private bool _isRunning;

        public TCPWebServer(string host = "localhost", int port = 8080, string webRoot = "webroot")
        {
            _host = host;
            _port = port;
            _webRoot = webRoot;
            _allowedExtensions = new HashSet<string> { ".html", ".css", ".js" };

            // Ensure webroot directory exists
            if (!Directory.Exists(_webRoot))
            {
                Directory.CreateDirectory(_webRoot);
                Console.WriteLine($"Created webroot directory: {Path.GetFullPath(_webRoot)}");
            }
        }

        public async Task StartServerAsync()
        {
            try
            {
                // Create TCP listener
                _listener = new TcpListener(IPAddress.Parse("127.0.0.1"), _port);
                _listener.Start();
                _isRunning = true;

                Console.WriteLine($"TCP Web Server started on http://{_host}:{_port}");
                Console.WriteLine($"Serving files from: {Path.GetFullPath(_webRoot)}");
                Console.WriteLine("Press 'q' and Enter to stop the server");

                // Start listening for connections
                var listenTask = ListenForClientsAsync();

                // Wait for user input to stop server
                await WaitForStopSignal();

                Console.WriteLine("Shutting down server...");
                _isRunning = false;
                _listener?.Stop();

                await listenTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        }

        private async Task ListenForClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    Console.WriteLine($"Connection from {tcpClient.Client.RemoteEndPoint}");

                    // Handle client in separate task (thread)
                    _ = Task.Run(async () => await HandleClientAsync(tcpClient));
                }
                catch (ObjectDisposedException)
                {
                    // Expected when server is stopping
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error accepting client: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            NetworkStream stream = null;
            try
            {
                stream = client.GetStream();

                // Read request data
                byte[] buffer = new byte[4096];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead == 0) return;

                string requestData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Request received: {requestData.Split('\n')[0]}");

                // Process the request
                string response = ProcessRequest(requestData);

                // Send response
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
                if (stream != null)
                {
                    try
                    {
                        string errorResponse = CreateErrorResponse(500, "Internal Server Error");
                        byte[] errorBytes = Encoding.UTF8.GetBytes(errorResponse);
                        await stream.WriteAsync(errorBytes, 0, errorBytes.Length);
                        await stream.FlushAsync();
                    }
                    catch { }
                }
            }
            finally
            {
                stream?.Close();
                client?.Close();
            }
        }

        private string ProcessRequest(string requestData)
        {
            var lines = requestData.Split('\n');
            if (lines.Length == 0)
                return CreateErrorResponse(400, "Bad Request");

            // Parse request line
            var requestLine = lines[0].Trim();
            var parts = requestLine.Split(' ');

            if (parts.Length < 3)
                return CreateErrorResponse(400, "Bad Request");

            string method = parts[0];
            string path = parts[1];
            string version = parts[2];

            // Check if method is GET
            if (method != "GET")
                return CreateErrorResponse(405, "Method Not Allowed");

            // Remove query parameters if present
            if (path.Contains("?"))
                path = path.Split('?')[0];

            // URL decode the path
            path = WebUtility.UrlDecode(path);


            // Handle root path
            if (path == "/")
                path = "/index.html";

            // Remove leading slash
            if (path.StartsWith("/"))
                path = path.Substring(1);

            // Security check: prevent directory traversal
            if (path.Contains("..") || Path.IsPathRooted(path))
                return CreateErrorResponse(403, "Forbidden");

            // Check file extension
            string extension = Path.GetExtension(path).ToLower();
            if (!_allowedExtensions.Contains(extension))
                return CreateErrorResponse(403, "Forbidden");

            // Construct full file path
            string filePath = Path.Combine(_webRoot, path);

            // Check if file exists and is a file (not directory)
            if (!File.Exists(filePath))
                return CreateErrorResponse(404, "Not Found");

            // Read and serve the file
            try
            {
                string content = File.ReadAllText(filePath, Encoding.UTF8);
                string contentType = GetContentType(extension);

                // Create successful response
                var response = new StringBuilder();
                response.AppendLine("HTTP/1.1 200 OK");
                response.AppendLine($"Content-Type: {contentType}");
                response.AppendLine($"Content-Length: {Encoding.UTF8.GetByteCount(content)}");
                response.AppendLine("Connection: close");
                response.AppendLine();
                response.Append(content);

                return response.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
                return CreateErrorResponse(500, "Internal Server Error");
            }
        }

        private string GetContentType(string extension)
        {
            var contentTypes = new Dictionary<string, string>
            {
                { ".html", "text/html" },
                { ".css", "text/css" },
                { ".js", "application/javascript" }
            };

            return contentTypes.TryGetValue(extension, out string contentType) ? contentType : "text/plain";
        }

        private string CreateErrorResponse(int statusCode, string statusText)
        {
            var errorPages = new Dictionary<int, string>
            {
                { 404, @"<html>
<head><title>404 Not Found</title></head>
<body><h1>Error 404: Page Not Found</h1></body>
</html>" },
                { 405, @"<html>
<head><title>405 Method Not Allowed</title></head>
<body><h1>Error 405: Method Not Allowed</h1></body>
</html>" },
                { 403, @"<html>
<head><title>403 Forbidden</title></head>
<body><h1>Error 403: Forbidden</h1></body>
</html>" },
                { 500, @"<html>
<head><title>500 Internal Server Error</title></head>
<body><h1>Error 500: Internal Server Error</h1></body>
</html>" }
            };

            string content = errorPages.TryGetValue(statusCode, out string errorContent)
                ? errorContent
                : "<html><body><h1>Error</h1></body></html>";

            var response = new StringBuilder();
            response.AppendLine($"HTTP/1.1 {statusCode} {statusText}");
            response.AppendLine("Content-Type: text/html");
            response.AppendLine($"Content-Length: {Encoding.UTF8.GetByteCount(content)}");
            response.AppendLine("Connection: close");
            response.AppendLine();
            response.Append(content);

            return response.ToString();
        }

        private async Task WaitForStopSignal()
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    var key = Console.ReadLine();
                    if (key?.ToLower() == "q")
                        break;
                }
            });
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            // Configure server settings
            const string HOST = "localhost";
            const int PORT = 8080;
            const string WEBROOT = "webroot";

            // Create and start server
            var server = new TCPWebServer(HOST, PORT, WEBROOT);
            await server.StartServerAsync();
        }
    }
}