using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace StreamingApp
{
    public abstract class HttpServer
    {
        protected int Port;
        public TcpListener Listener;
        private const bool IsActive = true;

        protected HttpServer(int port)
        {
            this.Port = port;
        }

        public void Listen()
        {
#pragma warning disable 618
            Listener = new TcpListener(Port);
#pragma warning restore 618
            Listener.Start();
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            while (IsActive)
            {
                var s = Listener.AcceptTcpClient();
                var processor = new HttpProcessor(s, this);
                var thread = new Thread(new ThreadStart(processor.Process));
                thread.Start();
                Thread.Sleep(1);
            }
        }

        public abstract void HandleGetRequest(HttpProcessor p);
        public abstract void HandlePostRequest(HttpProcessor p, StreamReader inputData);
    }

    public class MyVideoStreamingServer : HttpServer
    {
        private static readonly IDictionary<string, string> MimeTypeMappings =
            new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                #region extension to MIME type list

                {".asf", "video/x-ms-asf"},
                {".asx", "video/x-ms-asf"},
                {".avi", "video/x-msvideo"},
                {".bin", "application/octet-stream"},
                {".cco", "application/x-cocoa"},
                {".crt", "application/x-x509-ca-cert"},
                {".css", "text/css"},
                {".deb", "application/octet-stream"},
                {".der", "application/x-x509-ca-cert"},
                {".dll", "application/octet-stream"},
                {".dmg", "application/octet-stream"},
                {".ear", "application/java-archive"},
                {".eot", "application/octet-stream"},
                {".exe", "application/octet-stream"},
                {".flv", "video/x-flv"},
                {".gif", "image/gif"},
                {".hqx", "application/mac-binhex40"},
                {".htc", "text/x-component"},
                {".htm", "text/html"},
                {".html", "text/html"},
                {".ico", "image/x-icon"},
                {".img", "application/octet-stream"},
                {".iso", "application/octet-stream"},
                {".jar", "application/java-archive"},
                {".jardiff", "application/x-java-archive-diff"},
                {".jng", "image/x-jng"},
                {".jnlp", "application/x-java-jnlp-file"},
                {".jpeg", "image/jpeg"},
                {".jpg", "image/jpeg"},
                {".js", "application/x-javascript"},
                {".mml", "text/mathml"},
                {".mng", "video/x-mng"},
                {".mov", "video/quicktime"},
                {".mp3", "audio/mpeg"},
                {".mpeg", "video/mpeg"},
                {".mp4", "video/mpeg"},
                {".mpg", "video/mpeg"},
                {".msi", "application/octet-stream"},
                {".msm", "application/octet-stream"},
                {".msp", "application/octet-stream"},
                {".pdb", "application/x-pilot"},
                {".pdf", "application/pdf"},
                {".pem", "application/x-x509-ca-cert"},
                {".pl", "application/x-perl"},
                {".pm", "application/x-perl"},
                {".png", "image/png"},
                {".prc", "application/x-pilot"},
                {".ra", "audio/x-realaudio"},
                {".rar", "application/x-rar-compressed"},
                {".rpm", "application/x-redhat-package-manager"},
                {".rss", "text/xml"},
                {".run", "application/x-makeself"},
                {".sea", "application/x-sea"},
                {".shtml", "text/html"},
                {".sit", "application/x-stuffit"},
                {".swf", "application/x-shockwave-flash"},
                {".tcl", "application/x-tcl"},
                {".tk", "application/x-tcl"},
                {".txt", "text/plain"},
                {".war", "application/java-archive"},
                {".wbmp", "image/vnd.wap.wbmp"},
                {".wmv", "video/x-ms-wmv"},
                {".xml", "text/xml"},
                {".xpi", "application/x-xpinstall"},
                {".zip", "application/zip"},

                #endregion
            };

        private readonly string _rootDirectory;
        protected internal string S;


        public MyVideoStreamingServer(int port, String path)
            : base(port)
        {
            _rootDirectory = path;
        }

        public override void HandleGetRequest(HttpProcessor p)
        {
            string filename = p.HttpUrl;
            filename = filename.Substring(1);
            Console.WriteLine(filename);
            filename = Path.Combine(_rootDirectory, filename);
            S = MimeTypeMappings.TryGetValue(Path.GetExtension(filename), out var mime)
                ? mime
                : "application/octet-stream";
            if (File.Exists(filename))
            {
                try
                {
                    Console.WriteLine("request: {0}", p.HttpUrl);
                    if (mime != null && (mime.Contains("video") || mime.Contains("audio")))
                    {
                        using (FileStream fs = new FileStream(filename, FileMode.Open))
                        {
                            int startByte;
                            var endByte = -1;
                            if (p.HttpHeaders.Contains("Range"))
                            {
                                var rangeHeader = p.HttpHeaders["Range"].ToString().Replace("bytes=", "");
                                var range = rangeHeader.Split('-');
                                startByte = int.Parse(range[0]);
                                if (range[1].Trim().Length > 0) int.TryParse(range[1], out endByte);
                                if (endByte == -1) endByte = (int) fs.Length;
                            }
                            else
                            {
                                startByte = 0;
                                endByte = (int) fs.Length;
                            }

                            byte[] buffer = new byte[endByte - startByte];
                            fs.Position = startByte;
                            fs.Read(buffer, 0, endByte - startByte);
                            fs.Flush();
                            fs.Close();
                            p.OutputStream.AutoFlush = true;
                            p.OutputStream.WriteLine("HTTP/1.0 206 Partial Content");
                            p.OutputStream.WriteLine("Content-Type: " + mime);
                            p.OutputStream.WriteLine("Accept-Ranges: bytes");
                            var totalCount = startByte + buffer.Length;
                            p.OutputStream.WriteLine($"Content-Range: bytes {startByte}-{totalCount - 1}/{totalCount}");
                            p.OutputStream.WriteLine("Content-Length: " + buffer.Length.ToString());
                            p.OutputStream.WriteLine("Connection: keep-alive");
                            p.OutputStream.WriteLine("");
                            p.OutputStream.AutoFlush = false;

                            p.OutputStream.BaseStream.Write(buffer, 0, buffer.Length);
                            p.OutputStream.BaseStream.Flush();
                        }
                    }
                    else
                    {
                        byte[] buffer = File.ReadAllBytes(filename);
                        p.OutputStream.AutoFlush = true;
                        p.OutputStream.WriteLine("HTTP/1.0 200 OK");
                        p.OutputStream.WriteLine("Content-Type: " + mime);
                        p.OutputStream.WriteLine("Connection: close");
                        p.OutputStream.WriteLine("Content-Length: " + buffer.Length.ToString());
                        p.OutputStream.WriteLine("");

                        p.OutputStream.AutoFlush = false;
                        p.OutputStream.BaseStream.Write(buffer, 0, buffer.Length);
                        p.OutputStream.BaseStream.Flush();
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        public override void HandlePostRequest(HttpProcessor p, StreamReader inputData)
        {
            Console.WriteLine("POST request: {0}", p.HttpUrl);
            var data = inputData.ReadToEnd();
            p.OutputStream.WriteLine("<html><body><h1>test server</h1>");
            p.OutputStream.WriteLine("<a href=/test>return</a><p>");
            p.OutputStream.WriteLine("post-body: <pre>{0}</pre>", data);
        }
    }
}