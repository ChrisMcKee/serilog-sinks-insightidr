using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Benchmark;

public static class FakeRapid7
{
    public static void StartFakeLogEndpoint(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();

        try
        {
            while (true)
            {
                Console.WriteLine("Waiting for a connection...");
                var client = listener.AcceptTcpClient();
                Console.WriteLine("Client connected!");

                // Start handling the client in a separate thread
                var clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Exception: " + ex.Message);
        }
        finally
        {
            listener.Stop();
        }
    }
    static void HandleClient(TcpClient client)
    {
        var stream = client.GetStream();

        try
        {
            var buffer = new byte[1024];
            while (true)
            {
                var bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Console.WriteLine("Client disconnected.");
                    break;
                }
                Console.WriteLine("Received: " + Encoding.UTF8.GetString(buffer, 0, bytesRead));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Client handling exception: " + ex.Message);
        }
        finally
        {
            stream.Close();
            client.Close();
        }
    }
}
