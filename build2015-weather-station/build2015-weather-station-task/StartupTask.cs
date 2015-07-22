using System;
using System.IO;
using System.Threading;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.System.Threading;

using build2015_weather_station_task.Sparkfun;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace build2015_weather_station_task
{
    public sealed partial class StartupTask : IBackgroundTask
    {
        private readonly int i2cReadIntervalSeconds = 2;
        private ThreadPoolTimer i2cTimer;
        private Mutex mutex;
        private string mutexId = "WeatherStation";
        private readonly int port = 50001;
        private HttpServer server;
        private WeatherShield shield = new WeatherShield();
        private BackgroundTaskDeferral taskDeferral;
        private WeatherData weatherData = new WeatherData();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // Ensure our background task remains running
            taskDeferral = taskInstance.GetDeferral();

            // Mutex will be used to ensure only one thread at a time is talking to the shield / isolated storage
            mutex = new Mutex(false, mutexId);

            // Initialize WeatherShield
            await shield.BeginAsync();

            // Create a timer-initiated ThreadPool task to read data from I2C
            i2cTimer = ThreadPoolTimer.CreatePeriodicTimer(PopulateWeatherData, TimeSpan.FromSeconds(i2cReadIntervalSeconds));

            // Start the server
            server = new HttpServer(port);
            var asyncAction = ThreadPool.RunAsync((w) => { server.StartServer(weatherData); });

            // Task cancellation handler, release our deferral there 
            taskInstance.Canceled += OnCanceled;
        }

        private void OnCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            // Relinquish our task deferral
            taskDeferral.Complete();
        }

        private void PopulateWeatherData(ThreadPoolTimer timer)
        {
            bool hasMutex = false;

            try
            {
                hasMutex = mutex.WaitOne(1000);
                if (hasMutex)
                {
                    weatherData.TimeStamp = DateTime.Now.ToLocalTime().ToString();

                    shield.BlueLEDPin.Write(Windows.Devices.Gpio.GpioPinValue.High);

                    weatherData.Altitude = shield.Altitude;
                    weatherData.BarometricPressure = shield.Pressure;
                    weatherData.CelsiusTemperature = shield.Temperature;
                    weatherData.FahrenheitTemperature = (weatherData.CelsiusTemperature * 9 / 5) + 32;
                    weatherData.Humidity = shield.Humidity;

                    shield.BlueLEDPin.Write(Windows.Devices.Gpio.GpioPinValue.Low);

                    // Push the WeatherData local/cloud storage
                    WriteDataToIsolatedStorage();
                }
            }
            finally
            {
                if (hasMutex)
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        async private void WriteDataToIsolatedStorage()
        {
            // We have exlusive access to the mutex so can safely wipe the transfer file
            Windows.Globalization.DateTimeFormatting.DateTimeFormatter formatter = new Windows.Globalization.DateTimeFormatting.DateTimeFormatter("longtime");
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile transferFile = await localFolder.CreateFileAsync("DataFile.txt", CreationCollisionOption.ReplaceExisting);

            using (var stream = await transferFile.OpenStreamForWriteAsync())
            {
                StreamWriter writer = new StreamWriter(stream);

                writer.WriteLine(weatherData.TimeStamp);
                writer.WriteLine(weatherData.Altitude.ToString());
                writer.WriteLine(weatherData.BarometricPressure.ToString());
                writer.WriteLine(weatherData.CelsiusTemperature.ToString());
                writer.WriteLine(weatherData.FahrenheitTemperature.ToString());
                writer.WriteLine(weatherData.Humidity.ToString());
                writer.Flush();
            }
        }
    }
}
