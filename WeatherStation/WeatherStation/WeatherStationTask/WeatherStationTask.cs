using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Windows.ApplicationModel.Background;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.System.Threading;

namespace WeatherStationTask
{
    public sealed class WeatherData
    {
        public WeatherData()
        {
            TimeStamp = DateTimeOffset.Now.ToLocalTime().ToString();
        }

        public string TimeStamp { get; set; }
        public float Altitude { get; set; }
        public float CelsiusTemperature { get; set; }
        public float FahrenheitTemperature { get; set; }
        public float Humidity { get; set; }
        public float BarometricPressure { get; set; }

        public string JSON
        {
            get
            {
                var jsonSerializer = new DataContractJsonSerializer(typeof(WeatherData));
                using (MemoryStream strm = new MemoryStream())
                {
                    jsonSerializer.WriteObject(strm, this);
                    byte[] buf = strm.ToArray();
                    return Encoding.UTF8.GetString(buf, 0, buf.Length);
                }
            }
        }

        public string XML
        {
            get
            {
                var xmlserializer = new XmlSerializer(typeof(WeatherData));
                var stringWriter = new StringWriter();
                using (var writer = XmlWriter.Create(stringWriter))
                {
                    xmlserializer.Serialize(writer, this, new XmlSerializerNamespaces());
                    return stringWriter.ToString();
                }
            }
        }

        public string HTML
        {
            get
            {
                return string.Format(@"<html><head><title>My Weather Station</title></head><body>
                                    Time:{0}<br />
                                    Temperature (C/F): {1:N2}/{2:N2}<br />
                                    Barometric Pressure (kPa): {3:N4}<br />
                                    Relative Humidity (%): {4:N2}<br /></body></html>",
                                    TimeStamp, CelsiusTemperature, FahrenheitTemperature, (BarometricPressure / 1000), Humidity);
            }
        }
    }

    public sealed class ServerTask : IBackgroundTask
    {
        private BackgroundTaskDeferral taskDeferral;
        private ThreadPoolTimer i2cTimer;
        private HttpServer server;
        private readonly int port = 50001;
        private WeatherData weatherData = new WeatherData();
        private readonly int i2cReadIntervalSeconds = 2;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // Ensure our background task remains running
            taskDeferral = taskInstance.GetDeferral();

            // Create a timer-initiated ThreadPool task to read data from I2C
            i2cTimer = ThreadPoolTimer.CreatePeriodicTimer(PopulateWeatherData, TimeSpan.FromSeconds(i2cReadIntervalSeconds));

            // Start the server
            server = new HttpServer(port);
            var asyncAction = ThreadPool.RunAsync((w) => { server.StartServer(weatherData); });

            // Task cancellation handler, release our deferral there 
            taskInstance.Canceled += OnCanceled;
        }

        private void PopulateWeatherData(ThreadPoolTimer timer)
        {
            weatherData.TimeStamp = DateTime.Now.ToLocalTime().ToString();

            //weatherData.Altitude = 
            //weatherData.BarometricPressure = 
            //weatherData.CelsiusTemperature = 
            //weatherData.FahrenheitTemperature = 
            //weatherData.Humidity = 

            // Push the WeatherData local/cloud storage
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            // Relinquish our task deferral
            taskDeferral.Complete();
        }
    }

    public sealed class HttpServer : IDisposable
    {
        private const uint bufLen = 8192;
        private int defaultPort = 50001;
        private readonly StreamSocketListener sock;
        private WeatherData weatherData;

        public HttpServer(int serverPort)
        {
            sock = new StreamSocketListener();
            defaultPort = serverPort;
            sock.ConnectionReceived += (s, e) => ProcessRequestAsync(e.Socket);
        }

        public async void StartServer(WeatherData w)
        {
            await sock.BindServiceNameAsync(defaultPort.ToString());
            weatherData = w;
        }

        private async void ProcessRequestAsync(StreamSocket socket)
        {
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
