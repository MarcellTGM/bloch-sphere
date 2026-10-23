// MiniHttpServer – parányi statikus webszerver a Bloch-gömb szimulátorhoz.
// Csak beépített .NET-összetevőket használ, így Windows-on telepítés nélkül fut.
// A szerver kizárólag a helyi gépről (127.0.0.1) érhető el.

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class MiniHttpServer
{
    private readonly string root;
    private readonly TcpListener listener;
    private volatile bool running = true;

    public MiniHttpServer(string rootPath, int port)
    {
        root = Path.GetFullPath(rootPath);
        listener = new TcpListener(IPAddress.Loopback, port);
    }

    public int Port
    {
        get { return ((IPEndPoint)listener.LocalEndpoint).Port; }
    }

    public void Start()
    {
        listener.Start();
        Thread thread = new Thread(AcceptLoop);
        thread.IsBackground = true;
        thread.Start();
    }

    public void Stop()
    {
        running = false;
        try { listener.Stop(); } catch (Exception) { }
    }

    private void AcceptLoop()
    {
        while (running)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(Handle, client);
            }
            catch (Exception)
            {
                if (!running) return;
            }
        }
    }

    private void Handle(object state)
    {
        TcpClient client = (TcpClient)state;
        try
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                string requestLine = ReadLine(stream);
                if (String.IsNullOrEmpty(requestLine)) return;

                string headerLine;
                while (!String.IsNullOrEmpty(headerLine = ReadLine(stream))) { }

                string[] parts = requestLine.Split(' ');
                if (parts.Length < 2) return;

                string method = parts[0];
                string rawPath = parts[1];
                int q = rawPath.IndexOf('?');
                if (q >= 0) rawPath = rawPath.Substring(0, q);

                string path = Uri.UnescapeDataString(rawPath);
                if (path.Length == 0 || path == "/") path = "/index.html";

                path = path.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
                string full = Path.GetFullPath(Path.Combine(root, path));

                if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    WriteResponse(stream, 403, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Forbidden"), null);
                    return;
                }
                if (Directory.Exists(full)) full = Path.Combine(full, "index.html");

                if (!File.Exists(full))
                {
                    WriteResponse(stream, 404, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("Not found"), null);
                    return;
                }

                byte[] body = File.ReadAllBytes(full);
                bool head = method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);
                WriteResponse(stream, 200, Mime(Path.GetExtension(full)), head ? null : body, body.Length);
            }
        }
        catch (Exception)
        {
            // egy hibás kérés nem állítja meg a szervert
        }
    }

    private static void WriteResponse(NetworkStream stream, int code, string mime, byte[] body, long? contentLength)
    {
        string status = code == 200 ? "200 OK" : (code == 404 ? "404 Not Found" : "403 Forbidden");
        long length = contentLength.HasValue ? contentLength.Value : (body != null ? body.Length : 0);

        string header = "HTTP/1.1 " + status + "\r\n" +
                        "Content-Type: " + mime + "\r\n" +
                        "Content-Length: " + length + "\r\n" +
                        "Cache-Control: no-store\r\n" +
                        "Connection: close\r\n\r\n";

        byte[] headerBytes = Encoding.ASCII.GetBytes(header);
        stream.Write(headerBytes, 0, headerBytes.Length);
        if (body != null && body.Length > 0)
        {
            stream.Write(body, 0, body.Length);
        }
        stream.Flush();
    }

    private static string ReadLine(Stream stream)
    {
        StringBuilder sb = new StringBuilder();
        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            if (b == '\n') break;
            if (b != '\r') sb.Append((char)b);
        }
        return sb.ToString();
    }

    private static string Mime(string ext)
    {
        switch (ext.ToLowerInvariant())
        {
            case ".html":
            case ".htm": return "text/html; charset=utf-8";
            case ".css": return "text/css; charset=utf-8";
            case ".js": return "text/javascript; charset=utf-8";
            case ".json": return "application/json; charset=utf-8";
            case ".wasm": return "application/wasm";
            case ".png": return "image/png";
            case ".ico": return "image/x-icon";
            case ".svg": return "image/svg+xml";
            case ".txt":
            case ".md": return "text/plain; charset=utf-8";
            default: return "application/octet-stream";
        }
    }
}
