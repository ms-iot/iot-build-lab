# //build: Hands-on-lab WeatherStation

## Complete and install the weather station application
1. Clone the repository
2. Open "WeatherStation\WeatherStation.sln" in Visual Studio 2015
3. Navigate to "WeatherShield.cs" in the "Solution Explorer" pane
4. Search for “//TODO:” and write the necessary code
5. Click the “Debug” menu item, and select “WeatherStation Properties…”
6. Under the “Debug” tab, in the “Start options” section
    1. Select “Remote Device” as “Target device:”
    2. Enter the IP address of your Windows IoT Core device in the “Remote machine:” field
7. Deploy to the Windows IoT Core device

## Interface with and/or debug the application
- Set a breakpoint in WeatherStationTask.cs, in the PopulateWeatherData function
- Step through the individual I2C transactions as they occur

#### OR
- Ping the IP address of your Windows IoT Core device on port 50001 in an internet browser window
