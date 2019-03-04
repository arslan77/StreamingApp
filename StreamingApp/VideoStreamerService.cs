using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamingApp
{
    class VideoStreamingService
    {
        private Thread _thread;

        public void Start()
        {
            string path= @"H:\TestMe";
            HttpServer httpServer = new MyVideoStreamingServer(8010, path);
            _thread = new Thread(new ThreadStart(httpServer.Listen));
            _thread.Start();
        }

        public void Stop()
        {
            _thread.Abort();

        }
    }
}