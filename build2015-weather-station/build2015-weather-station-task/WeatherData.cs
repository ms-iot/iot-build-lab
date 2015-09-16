using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace build2015_weather_station_task
{
    public sealed class WeatherData
    {
        public WeatherData()
        {
            TimeStamp = DateTimeOffset.Now.ToLocalTime().ToString();
        }

        public float Altitude { get; set; }
        public float BarometricPressure { get; set; }
        public float CelsiusTemperature { get; set; }
        public float FahrenheitTemperature { get; set; }
        public float Humidity { get; set; }
        public string TimeStamp { get; set; }

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
}
