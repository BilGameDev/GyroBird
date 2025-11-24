using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public class GoogleRedirectListener : MonoBehaviour
{
    public static int ListenPort = 3000;
    private static TcpListener listener;

    public static void StartListening()
    {
        Task.Run(() =>
        {
            try
            {
                listener = new TcpListener(IPAddress.Loopback, ListenPort);
                listener.Start();

                Debug.Log($"Listening on http://localhost:{ListenPort} for Google redirect...");

                while (true)
                {
                    using (var client = listener.AcceptTcpClient())
                    using (var stream = client.GetStream())
                    using (var reader = new StreamReader(stream))
                    using (var writer = new StreamWriter(stream))
                    {
                        var requestLine = reader.ReadLine();
                        if (requestLine == null) continue;

                        var parts = requestLine.Split(' ');
                        if (parts.Length < 2) continue;

                        var path = parts[1];
                        var query = path.Split('?');
                        if (query.Length < 2) continue;

                        var queryParams = query[1].Split('&');
                        foreach (var param in queryParams)
                        {
                            if (param.StartsWith("code="))
                            {
                                var code = Uri.UnescapeDataString(param.Substring(5));
                                Debug.Log("Received Google OAuth code: " + code);

                                // Send success response
                                string response = "<html><body><h2>Login successful! You can return to the app.</h2></body></html>";
                                string header = "HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: " + response.Length + "\r\n\r\n";
                                writer.Write(header + response);
                                writer.Flush();

                                // Pass the code back to the auth manager
                                UnityMainThreadHelper.Run(() =>
                                {
                                    NetworkEventHandler.GoogleAuthCodeReceived(code);
                                });

                                listener.Stop(); // Stop after receiving once
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("OAuth listener error: " + ex);
            }
        });
    }

    void OnApplicationQuit()
    {
        listener?.Stop();
    }
}