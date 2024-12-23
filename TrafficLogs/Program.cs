using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;

public class ProxyServer
{
    private const int ProxyPort = 8888;

    public static void Main(string[] args)
    {
        Console.WriteLine("humanot @ 2024");
        TcpListener server = new TcpListener(IPAddress.Any, ProxyPort);
        server.Start();
        Console.WriteLine($"Proxy server started on port {ProxyPort}");
        Console.WriteLine($"Connect using your hotspot ipv4");
        Console.WriteLine($"Dont close this console or proxy will be down");

        try
        {
            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Console.WriteLine("Client connected.");

                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            server.Stop();
        }
    }

    static void HandleClient(TcpClient client)
    {
        NetworkStream clientStream = client.GetStream();

        try
        {
            byte[] buffer = new byte[4096];
            int bytesRead = clientStream.Read(buffer, 0, buffer.Length);

            if (bytesRead > 0)
            {
                string clientRequest = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Client request: \n{clientRequest}");

                string[] requestLines = clientRequest.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (requestLines.Length > 0)
                {
                    string firstLine = requestLines[0];
                    string[] firstLineParts = firstLine.Split(' ');

                    if (firstLineParts.Length > 2 && firstLineParts[0].ToUpper() == "CONNECT")
                    {
                        string[] hostAndPort = firstLineParts[1].Split(':');
                        string hostname = hostAndPort[0];
                        int port = int.Parse(hostAndPort[1]);

                        try
                        {
                            Console.WriteLine($"Connecting to {hostname}:{port}");
                            TcpClient serverClient = new TcpClient();
                            serverClient.Connect(hostname, port);

                            NetworkStream serverStream = serverClient.GetStream();

                            string response = "HTTP/1.0 200 Connection established\r\n\r\n";
                            byte[] responseBytes = Encoding.ASCII.GetBytes(response);
                            clientStream.Write(responseBytes, 0, responseBytes.Length);
                            clientStream.Flush();


                            Thread clientToServerThread = new Thread(() => TunnelTraffic(clientStream, serverStream));
                            Thread serverToClientThread = new Thread(() => TunnelTraffic(serverStream, clientStream));
                            clientToServerThread.Start();
                            serverToClientThread.Start();


                            clientToServerThread.Join();
                            serverToClientThread.Join();

                            serverStream.Close();
                            serverClient.Close();

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error during tunneling {ex.Message}");
                            string errorResponse = $"HTTP/1.0 502 Bad Gateway\r\n\r\n";
                            byte[] errorResponseBytes = Encoding.ASCII.GetBytes(errorResponse);
                            clientStream.Write(errorResponseBytes, 0, errorResponseBytes.Length);
                            clientStream.Flush();

                            Console.WriteLine(ex.Message);
                        }
                    }
                    else
                    {
                        if (firstLineParts.Length > 1)
                        {
                            string url = firstLineParts[1].Replace("http://", "").Replace("https://", "");
                            string[] urlParts = url.Split('/');
                            string hostname = urlParts[0];

                            TcpClient serverClient = new TcpClient();
                            try
                            {
                                serverClient.Connect(hostname, 80);
                                NetworkStream serverStream = serverClient.GetStream();

                                serverStream.Write(buffer, 0, bytesRead);
                                serverStream.Flush();

                                byte[] serverResponseBuffer = new byte[4096];
                                int serverBytesRead;
                                while ((serverBytesRead = serverStream.Read(serverResponseBuffer, 0, serverResponseBuffer.Length)) > 0)
                                {
                                    // Пересылаем ответ от сервера клиенту
                                    clientStream.Write(serverResponseBuffer, 0, serverBytesRead);
                                    clientStream.Flush();
                                }

                                serverStream.Close();
                                serverClient.Close();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error connecting or tunneling : {ex.Message}");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("Client disconnected.");
            clientStream.Close();
            client.Close();
        }
    }

    static void TunnelTraffic(NetworkStream source, NetworkStream destination)
    {
        try
        {
            byte[] buffer = new byte[4096];
            int bytesRead;
            while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                destination.Write(buffer, 0, bytesRead);
                destination.Flush();
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tunnel error: {ex.Message}");

        }
    }

}
