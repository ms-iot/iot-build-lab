using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.UI;
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
        private WeatherData data;

        public MainPage()
        {
            //InitializeComponent();
            //InitScreen();
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

        private void UpdateScreen()
        {
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
