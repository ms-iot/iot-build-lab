using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.System.Threading;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

using build2015_weather_station_task;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace build2015_weather_station
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private WeatherData data = new WeatherData();
        private HttpClient weatherClient;
        private HttpBaseProtocolFilter weatherFilter = new HttpBaseProtocolFilter();
        //TODO: On the following line, replace "minwinpc" with the computer name of your IoT device (i.e. "http://<iot_device_name>:50001").
        private Uri weatherUri = new Uri("http://minwinpc:50001");

        public MainPage()
        {
            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.FullScreen;
            InitializeComponent();

            // Setup client to read from weather server
            weatherFilter.CacheControl.ReadBehavior = HttpCacheReadBehavior.MostRecent;
            weatherClient = new HttpClient(weatherFilter);

            LogToScreen("Attempting to read from endpoint: " + weatherUri);
            LogToScreen("");

            // Create a timer-initiated ThreadPool task to read data from I2C
            ThreadPoolTimer readerTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
            {
                await ClearLogScreen();
                QueryWeatherData();

                // Notify the UI to do an update.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () => UpdateScreen());

            }, TimeSpan.FromSeconds(2));

        }

        private async Task ClearLogScreen(CoreDispatcherPriority priority = CoreDispatcherPriority.Low)
        {
                await Dispatcher.RunAsync(priority, () => { Status.Text = ""; });
        }

        private async void LogToScreen (string text, CoreDispatcherPriority priority = CoreDispatcherPriority.Low)
        {
            await Dispatcher.RunAsync(priority, () => { Status.Text += text + "\n"; });
        }

        async void QueryWeatherData()
        {
            // Query weather data
            try {
                using (HttpResponseMessage response = await weatherClient.GetAsync(weatherUri))
                {
                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();

                        // Parse JSON response
                        JsonObject jWeatherData = JsonObject.Parse(responseString);
                        JsonValue jAltitude = jWeatherData.GetNamedValue("Altitude");
                        JsonValue jBarometricPressure = jWeatherData.GetNamedValue("BarometricPressure");
                        JsonValue jCelsiusTemperature = jWeatherData.GetNamedValue("CelsiusTemperature");
                        JsonValue jFahrenheitTemperature = jWeatherData.GetNamedValue("FahrenheitTemperature");
                        JsonValue jHumidity = jWeatherData.GetNamedValue("Humidity");
                        JsonValue jTimeStamp = jWeatherData.GetNamedValue("TimeStamp");

                        // Update screen with parsed data
                        data.TimeStamp = jTimeStamp.GetString();
                        LogToScreen("Parsed time stamp value: " + data.TimeStamp);

                        data.Altitude = (float)jAltitude.GetNumber();
                        LogToScreen("Parsed altitude value: " + data.Altitude);

                        data.BarometricPressure = (float)jBarometricPressure.GetNumber();
                        LogToScreen("Parsed barometric pressure value: " + data.BarometricPressure);

                        data.CelsiusTemperature = (float)jCelsiusTemperature.GetNumber();
                        LogToScreen("Parsed celsius temperature value: " + data.CelsiusTemperature);

                        data.FahrenheitTemperature = (float)jFahrenheitTemperature.GetNumber();
                        LogToScreen("Parsed Fahrenheit temperature value: " + data.FahrenheitTemperature);

                        data.Humidity = (float)jHumidity.GetNumber();
                        LogToScreen("Parsed humidity value: " + data.Humidity);
                    }
                    else
                    {
                        LogToScreen("ERROR: Unable to successfully reach " + weatherUri + "!");
                        LogToScreen("Status Code: " + response.StatusCode);
                        LogToScreen("Message: " + response.ReasonPhrase);
                    }
                }
            }
            catch (Exception e)
            {
                LogToScreen("ERROR: " + e.HResult + " {\r\n" + e.Message + "}");
            }
            finally
            {
                LogToScreen("");
            }
        }

        private void Status_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Automatically scroll screen down, when new data is entered
            var grid = (Grid)VisualTreeHelper.GetChild(Status, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }

        private void UpdateScreen()
        {
            TimeStamp.Text = data.TimeStamp;

            Altimeter.Value = data.Altitude;
            Altitude.Text = string.Format("{0:N2}m", Altimeter.Value);

            Hygrometer.Value = data.Humidity;
            Humidity.Text = string.Format("{0:N2}%RH", Hygrometer.Value);

            Barometer.Value = data.BarometricPressure / 1000;
            Pressure.Text = string.Format("{0:N4}kPa", Barometer.Value);

            Thermometer.Value = data.CelsiusTemperature;
            Temperature.Text = string.Format("{0:N2}C", Thermometer.Value);
        }
    }
}
