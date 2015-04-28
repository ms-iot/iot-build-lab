using System;
using System.IO;
using System.Threading;
using Windows.Storage;
using Windows.System.Threading;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

using WeatherStationTask;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace WeatherStation
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private WeatherData data = new WeatherData();

        string mutexId = "WeatherStation";
        Mutex mutex;
        private bool dataReady = false;

        public MainPage()
        {

            InitializeComponent();
            InitScreen();

            // Mutex will be used to ensure only one thread at a time is talking to the shield / isolated storage
            mutex = new Mutex(false, mutexId);

            // Create a timer-initiated ThreadPool task to read data from I2C
            ThreadPoolTimer readerTimer = ThreadPoolTimer.CreatePeriodicTimer(async (source) =>
            {
                // Read the updated data
                if (mutex.WaitOne(1000))
                {
                    // We have exlusive access to the mutex so can safely read the transfer file
                    ReadData();
                    mutex.ReleaseMutex();
                }

                // Notify the UI to do an update.
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High,
                    () =>
                    {
                        // UI can be accessed here
                        UpdateScreen();
                    });

            }, TimeSpan.FromSeconds(2));

        }

        private void InitScreen()
        {
            Status.Text = "Initialize Shield Components...";
            Status.Text += "\nShield Ready!";

            UpdateScreen();
        }

        private void Status_TextChanged(object sender, TextChangedEventArgs e)
        {
            var grid = (Grid)VisualTreeHelper.GetChild(Status, 0);
            for (var i = 0; i <= VisualTreeHelper.GetChildrenCount(grid) - 1; i++)
            {
                object obj = VisualTreeHelper.GetChild(grid, i);
                if (!(obj is ScrollViewer)) continue;
                ((ScrollViewer)obj).ChangeView(0.0f, ((ScrollViewer)obj).ExtentHeight, 1.0f);
                break;
            }
        }

        async void ReadData()
        {
            try
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                StorageFile transferFile = await localFolder.GetFileAsync("DataFile.txt");
                using (var stream = await transferFile.OpenStreamForReadAsync())
                {
                    StreamReader reader = new StreamReader(stream);
                    string temp = "";

                    temp = await reader.ReadLineAsync();
                    data.TimeStamp = temp;
                    temp = await reader.ReadLineAsync();
                    data.Altitude = float.Parse(temp);
                    temp = reader.ReadLine();
                    data.BarometricPressure = float.Parse(temp);
                    temp = reader.ReadLine();
                    data.CelsiusTemperature = float.Parse(temp);
                    temp = reader.ReadLine();
                    data.FahrenheitTemperature = float.Parse(temp);
                    temp = reader.ReadLine();
                    data.Humidity = float.Parse(temp);

                    dataReady = true;
                }
            }
            catch (Exception)
            {
            }
        }


        private void UpdateScreen()
        {
            if (dataReady)
            {
                TimeStamp.Text = data.TimeStamp;
                Altimeter.Value = data.Altitude;
                Altitude.Text = string.Format("{0:N2}m", Altimeter.Value);

                Hygrometer.Value = data.Humidity;
                Humidty.Text = string.Format("{0:N2}%RH", Hygrometer.Value);

                Barometer.Value = data.BarometricPressure / 1000;
                Pressure.Text = string.Format("{0:N4}kPa", Barometer.Value);

                Thermometer.Value = data.CelsiusTemperature;
                Temperature.Text = string.Format("{0:N2}C", Thermometer.Value);
            }
        }
        
    }
}
