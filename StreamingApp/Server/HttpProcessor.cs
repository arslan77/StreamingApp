using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace StreamingApp
{

    public class HttpProcessor
    {
        public TcpClient Socket;
        public HttpServer Srv;
        private Stream _inputStream;
        public StreamWriter OutputStream;

        public string HttpMethod;

        public string HttpUrl;
        public string HttpProtocolVersionString;
        public Hashtable HttpHeaders = new Hashtable();


        private const int MaxPostSize = 10 * 1024 * 1024; // 10MB

        public HttpProcessor(TcpClient s, HttpServer srv)
        {
            Socket = s;
            Srv = srv;
        }


        private static string StreamReadLine(Stream inputStream)
        {
            string data = "";
            while (true)
            {
                var nextChar = inputStream.ReadByte();
                if (nextChar == '\n')
                {
                    break;
                }

                if (nextChar == '\r')
                {
                    continue;
                }

                if (nextChar == -1)
                {
                    Thread.Sleep(1);
                    continue;
                }

                data += Convert.ToChar(nextChar);
            }

            return data;
        }

        public void Process()
        {
            // we can't use a StreamReader for input, because it buffers up extra data on us inside it's
            // "processed" view of the world, and we want the data raw after the headers
            _inputStream = new BufferedStream(Socket.GetStream());

            // we probably shouldn't be using a stream writer for all output from handlers either
            OutputStream = new StreamWriter(new BufferedStream(Socket.GetStream()));
            try
            {
                ParseRequest();
                ReadHeaders();
                if (HttpMethod.Equals("GET"))
                {
                    HandleGetRequest();
                }
                else if (HttpMethod.Equals("POST"))
                {
                    HandlePostRequest();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e);
                WriteFailure();
            }

            OutputStream.Flush();
            // bs.Flush(); // flush any remaining output
            _inputStream = null;
            OutputStream = null; // bs = null;            
            Socket.Close();
        }

        public void ParseRequest()
        {
            var request = StreamReadLine(_inputStream);
            var tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }

            HttpMethod = tokens[0].ToUpper();
            HttpUrl = tokens[1];
            HttpProtocolVersionString = tokens[2];

            Console.WriteLine("starting: " + request);
        }

        public void ReadHeaders()
        {
            Console.WriteLine("readHeaders()");
            String line;
            while ((line = StreamReadLine(_inputStream)) != null)
            {
                if (line.Equals(""))
                {
                    Console.WriteLine("got headers");
                    return;
                }


                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }

                String name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++; // strip any spaces
                }


                string value = line.Substring(pos, line.Length - pos);
                Console.WriteLine("header: {0}:{1}", name, value);
                HttpHeaders[name] = value;
            }
        }

        public void HandleGetRequest()
        {
            Srv.HandleGetRequest(this);
        }

        private const int BufSize = 4096;

        public void HandlePostRequest()
        {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 

            Console.WriteLine("get post data start");
            var ms = new MemoryStream();
            if (HttpHeaders.ContainsKey("Content-Length"))
            {
                var contentLen = Convert.ToInt32(HttpHeaders["Content-Length"]);
                if (contentLen > MaxPostSize)
                {
                    throw new Exception(
                        $"POST Content-Length({contentLen}) too big for this simple server");
                }

                byte[] buf = new byte[BufSize];
                int toRead = contentLen;
                while (toRead > 0)
                {
                    Console.WriteLine("starting Read, to_read={0}", toRead);

                    int numRead = _inputStream.Read(buf, 0, Math.Min(BufSize, toRead));
                    Console.WriteLine("read finished, numRead={0}", numRead);
                    if (numRead == 0)
                    {
                        if (toRead == 0)
                        {
                            break;
                        }
                        else
                        {
                            throw new Exception("client disconnected during post");
                        }
                    }

                    toRead -= numRead;
                    ms.Write(buf, 0, numRead);
                }

                ms.Seek(0, SeekOrigin.Begin);
            }

            Console.WriteLine("get post data end");
            Srv.HandlePostRequest(this, new StreamReader(ms));
        }

        public void WriteSuccess()
        {
            OutputStream.Write("HTTP/1.0 200 OK\n");
            OutputStream.Write("Content-Type: text/html\n");
            OutputStream.Write("Connection: close\n");
            OutputStream.Write("\n");
        }

        public void WriteFailure()
        {
            OutputStream.Write("HTTP/1.0 404 File not found\n");
            OutputStream.Write("Connection: close\n");
            OutputStream.Write("\n");
        }
    }
}
