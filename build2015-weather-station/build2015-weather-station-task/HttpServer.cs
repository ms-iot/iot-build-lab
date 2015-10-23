using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

using Microsoft.Maker.Sparkfun.WeatherShield;
using Windows.Devices.Gpio;

namespace build2015_weather_station_task
{
    public sealed class HttpServer : IDisposable
    {
        private const uint bufLen = 8192;
        private int defaultPort = 50001;
        private readonly StreamSocketListener sock;
        private WeatherData weatherData;
        private WeatherShield weatherShield;

        public HttpServer(int serverPort)
        {
            sock = new StreamSocketListener();
            defaultPort = serverPort;
            sock.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);
        }

        public async void StartServer(WeatherShield shield, WeatherData data)
        {
            await sock.BindServiceNameAsync(defaultPort.ToString());
            weatherShield = shield;
            weatherData = data;
        }

        private async void ProcessRequestAsync(StreamSocket socket)
        {
            weatherShield.GreenLedPin.Write(GpioPinValue.High);
            // Read in the HTTP request, we only care about type 'GET'
            StringBuilder request = new StringBuilder();
            using (IInputStream input = socket.InputStream)
            {
                byte[] data = new byte[bufLen];
                IBuffer buffer = data.AsBuffer();
                uint dataRead = bufLen;
                while (dataRead == bufLen)
                {
                    await input.ReadAsync(buffer, bufLen, InputStreamOptions.Partial);
                    request.Append(Encoding.UTF8.GetString(data, 0, data.Length));
                    dataRead = buffer.Length;
                }
            }

            using (IOutputStream output = socket.OutputStream)
            {
                string requestMethod = request.ToString().Split('\n')[0];
                string[] requestParts = requestMethod.Split(' ');
                await WriteResponseAsync(requestParts, output);
            }
            weatherShield.GreenLedPin.Write(GpioPinValue.Low);
        }

        private async Task WriteResponseAsync(string[] requestTokens, IOutputStream outstream)
        {
            // NOTE: If you change the respBody format, change the Content-Type (below) accordingly
            //string respBody = weatherData.HTML;
            //string respBody = weatherData.XML;
            string respBody = weatherData.JSON;

            string htmlCode = "200 OK";

            using (Stream resp = outstream.AsStreamForWrite())
            {
                byte[] bodyArray = Encoding.UTF8.GetBytes(respBody);
                MemoryStream stream = new MemoryStream(bodyArray);

                // NOTE: If you change the respBody format (above), change the Content-Type accordingly
                string header = string.Format("HTTP/1.1 {0}\r\n" +
                                              //"Content-Type: text/html\r\n" + // HTML only
                                              //"Content-Type: text/xml\r\n" +  // XML only
                                              "Content-Type: text/json\r\n" + // JSON only
                                              "Content-Length: {1}\r\n" +
                                              "Connection: close\r\n\r\n",
                                              htmlCode, stream.Length);

                byte[] headerArray = Encoding.UTF8.GetBytes(header);
                await resp.WriteAsync(headerArray, 0, headerArray.Length);
                await stream.CopyToAsync(resp);
                await resp.FlushAsync();
            }
        }

        public void Dispose()
        {
            sock.Dispose();
        }
    }
}
