using System.Threading;

namespace StreamingApp
{
    class VideoStreamingService
    {
        private readonly string _path;
        private readonly int _port;
        public VideoStreamingService(string path, int port = 8090)
        {
            _path = path;
            _port = port;
        }
        private Thread _thread;

        public void Start()
        {
            HttpServer httpServer = new MyVideoStreamingServer(_port, _path);
            _thread = new Thread(httpServer.Listen);
            _thread.Start();
        }

        public void Stop()
        {
            _thread.Abort();

        }
    }
}