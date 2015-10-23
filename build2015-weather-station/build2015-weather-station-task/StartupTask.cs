using System;
using System.Threading;
using Windows.ApplicationModel.Background;
using Windows.System.Threading;

using Microsoft.Maker.Sparkfun.WeatherShield;

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
        private WeatherShield shield = new WeatherShield("I2C1", 6, 5);
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
            var asyncAction = ThreadPool.RunAsync((w) => { server.StartServer(shield, weatherData); });

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

                    shield.BlueLedPin.Write(Windows.Devices.Gpio.GpioPinValue.High);

                    weatherData.Altitude = shield.Altitude;
                    weatherData.BarometricPressure = shield.Pressure;
                    weatherData.CelsiusTemperature = shield.Temperature;
                    weatherData.FahrenheitTemperature = (weatherData.CelsiusTemperature * 9 / 5) + 32;
                    weatherData.Humidity = shield.Humidity;

                    shield.BlueLedPin.Write(Windows.Devices.Gpio.GpioPinValue.Low);
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
    }
}
