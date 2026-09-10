using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using VRage.Utils;

namespace ClientPlugin.Speech;

// Minimal HTTP/1.1 server on the loopback interface for Handy, serving both
// paste methods: its Webhook, and the paste scripts shipped with this plugin.
// Both POST the same JSON body to the same endpoint, so the path is not looked
// at. Each POST carries a transcript; it is queued for the game thread
// (Plugin.Update) and answered with {"handled":true|false} once that thread
// decided, so the caller knows whether it still has to paste the text.
internal sealed class SpeechListener : IDisposable
{
    private const int SocketTimeoutMs = 2000;
    private const int MaxRequestBytes = 64 * 1024;

    private static readonly byte[] HeaderTerminator = Encoding.ASCII.GetBytes("\r\n\r\n");
    private static readonly byte[] Continue = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");

    private readonly ConcurrentQueue<SpeechRequest> requests = new ConcurrentQueue<SpeechRequest>();
    private TcpListener listener;
    private volatile bool disposed;

    public int Port { get; }

    public SpeechListener(int port)
    {
        Port = port;
    }

    public void Start()
    {
        // The port arrives from a textbox the config dialog applies on every
        // keystroke, so values on the way to the intended one land here too.
        // TcpListener throws for a port outside the range, which must not be
        // allowed to take the game down.
        if (Port < 1 || Port > IPEndPoint.MaxPort)
        {
            MyLog.Default.Warning($"{Plugin.Name}: Port {Port} is out of the 1-{IPEndPoint.MaxPort} range, speech input is unavailable");
            return;
        }

        // Never anything but the loopback interface: the transcript is local input
        listener = new TcpListener(IPAddress.Loopback, Port);

        try
        {
            listener.Start();
        }
        catch (SocketException e)
        {
            MyLog.Default.Warning($"{Plugin.Name}: Cannot listen on 127.0.0.1:{Port}, speech input is unavailable: {e.Message}");
            return;
        }

        new Thread(AcceptLoop) { IsBackground = true, Name = Plugin.Name + "Listener" }.Start();
        MyLog.Default.Info($"{Plugin.Name}: Listening for Handy on 127.0.0.1:{Port}");
    }

    public bool TryDequeue(out SpeechRequest request) => requests.TryDequeue(out request);

    public void Dispose()
    {
        disposed = true;
        try
        {
            // Null when the port was rejected, so Start never got that far
            listener?.Stop();
        }
        catch (Exception)
        {
            // Nothing to do about a socket that fails to close on exit
        }
    }

    private void AcceptLoop()
    {
        while (!disposed)
        {
            TcpClient client;
            try
            {
                client = listener.AcceptTcpClient();
            }
            catch (Exception)
            {
                // Stop() aborts the pending accept with an exception
                if (disposed)
                    return;

                continue;
            }

            ThreadPool.QueueUserWorkItem(_ => Serve(client));
        }
    }

    private void Serve(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = SocketTimeoutMs;
                client.SendTimeout = SocketTimeoutMs;

                using (var stream = client.GetStream())
                {
                    if (!TryReadRequest(stream, out var method, out var body))
                    {
                        Respond(stream, "400 Bad Request", "{\"error\":\"bad request\"}");
                        return;
                    }

                    if (method != "POST")
                    {
                        Respond(stream, "405 Method Not Allowed", "{\"error\":\"POST only\"}");
                        return;
                    }

                    var text = ParseText(body);
                    if (text == null)
                    {
                        Respond(stream, "400 Bad Request", "{\"error\":\"missing text\"}");
                        return;
                    }

                    // Must stay below the scripts' own request timeout, otherwise
                    // they give up and paste while the game thread handles the text too
                    var timeoutMs = Math.Max(0, Config.Current.VerdictTimeoutMs);

                    var request = new SpeechRequest(text);
                    requests.Enqueue(request);
                    var handled = request.WaitForVerdict(timeoutMs);
                    Respond(stream, "200 OK", handled ? "{\"handled\":true}" : "{\"handled\":false}");
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Warning($"{Plugin.Name}: Failed to serve a speech request: {e.Message}");
            }
        }
    }

    private static bool TryReadRequest(NetworkStream stream, out string method, out string body)
    {
        method = null;
        body = null;

        var buffer = new MemoryStream();
        var chunk = new byte[4096];

        int headerEnd;
        while ((headerEnd = IndexOf(buffer, HeaderTerminator)) < 0)
        {
            var read = stream.Read(chunk, 0, chunk.Length);
            if (read <= 0 || buffer.Length + read > MaxRequestBytes)
                return false;

            buffer.Write(chunk, 0, read);
        }

        var header = Encoding.ASCII.GetString(buffer.GetBuffer(), 0, headerEnd);
        var lines = header.Split(new[] { "\r\n" }, StringSplitOptions.None);
        var requestLine = lines[0].Split(' ');
        if (requestLine.Length < 2)
            return false;

        method = requestLine[0];

        var contentLength = 0;
        var expectContinue = false;
        for (var i = 1; i < lines.Length; i++)
        {
            var colon = lines[i].IndexOf(':');
            if (colon <= 0)
                continue;

            var name = lines[i].Substring(0, colon).Trim();
            var value = lines[i].Substring(colon + 1).Trim();
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                int.TryParse(value, out contentLength);
            else if (name.Equals("Expect", StringComparison.OrdinalIgnoreCase))
                expectContinue = value.Equals("100-continue", StringComparison.OrdinalIgnoreCase);
        }

        var bodyStart = headerEnd + HeaderTerminator.Length;
        if (contentLength < 0 || bodyStart + contentLength > MaxRequestBytes)
            return false;

        // curl and .NET send larger bodies only after this go-ahead
        if (expectContinue && buffer.Length < bodyStart + contentLength)
            stream.Write(Continue, 0, Continue.Length);

        while (buffer.Length < bodyStart + contentLength)
        {
            var read = stream.Read(chunk, 0, chunk.Length);
            if (read <= 0)
                return false;

            buffer.Write(chunk, 0, read);
        }

        body = Encoding.UTF8.GetString(buffer.GetBuffer(), bodyStart, contentLength);
        return true;
    }

    private static int IndexOf(MemoryStream buffer, byte[] pattern)
    {
        var data = buffer.GetBuffer();
        var length = (int)buffer.Length;
        for (var i = 0; i <= length - pattern.Length; i++)
        {
            var j = 0;
            while (j < pattern.Length && data[i + j] == pattern[j])
                j++;

            if (j == pattern.Length)
                return i;
        }

        return -1;
    }

    private static string ParseText(string body)
    {
        try
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(body)))
            {
                var message = (Message)new DataContractJsonSerializer(typeof(Message)).ReadObject(stream);
                return string.IsNullOrWhiteSpace(message?.Text) ? null : message.Text;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void Respond(Stream stream, string status, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var head = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n\r\n");
        stream.Write(head, 0, head.Length);
        stream.Write(body, 0, body.Length);
        stream.Flush();
    }

    // The relevant part of the JSON the paste scripts send; other members are ignored
    [DataContract]
    internal sealed class Message
    {
        [DataMember(Name = "text")]
        public string Text { get; set; }
    }
}
