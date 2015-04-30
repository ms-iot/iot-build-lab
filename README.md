# Raspberry Pi 2 with Windows IoT Core setup #

This Wiki explains how to setup a Raspberry Pi 2 running Windows IoT Core to send temperature. Humidity, altitude, pressure data to Microsoft Azure for analytics, real time data display, and alerts.
It assumes that you have all the necessary hardware and tools installed (see below)

##Hardware requirements ##

- [Raspberry Pi 2]( http://www.raspberrypi.org/products/raspberry-pi-2-model-b/)
- [Sparkfun Weathershield](http://www.amazon.com/gp/product/B00H8OI1RU)
 

To setup the Raspberry Pi 2 to run Windows IoT Core, follow the instructions [here](http://ms-iot.github.io/content/win10/SetupRPI.htm)

You need to wire the Raspberry Pi 2 with the Weather Shield. Pinning diagram can be found on [Hackster.io](http://www.hackster.io/windows-iot-maker/build-hands-on-lab-iot-weather-station-using-windows-10)

##Software and tools requirements

You can find all the instructions and links to prepare your dev environment for Windows IoT Core on the Raspberry Pi 2 []here](http://ms-iot.github.io/content/win10/SetupPC.htm)
 
##Getting Started

* Open the solution (.sln file) in Visual Studio
* Open and edit the WeatherStationTask.cs file to change the connection to Event Hub settings using the information received from setting up the Azure services using the AzurePrep tool. For the 'displayname', 'organization' and 'location' pick one of your choosing. 

You can retrieve the Host, User, and Password strings by 
  
1. launching http://manage.windowsazure.com 
2. selecting Service Bus in the left nav menu 
3. picking your Namespace (used for `serviceBusNamespace`)
4. select Event Hubs from the top menu
5. select ehdevices (this value is used for `eventHubName` but should not need to be modified)
6. select Connection Information tab at the bottom (used for `keyName` and `key`)


```
            // Initialize ConnectTheDots Settings
            localSettings.ServicebusNamespace = "YOURSERVICEBUS-ns";
            localSettings.EventHubName = "ehdevices";
            localSettings.KeyName = "D1";
            localSettings.Key = "YOUR_KEY";
            localSettings.DisplayName = "YOUR_DEVICE_NAME";
            localSettings.Organization = "YOUR_ORGANIZATION_OR_SELF";
            localSettings.Location = "YOUR_LOCATION";
```

## Building and Deploying the app

At this point you can build and deploy the application. If you are not familiar with creating apps for Windows IoT Core, you can find detailed instructions on how to connect to the device from Visual Studio, deploy and debug your app [here](http://ms-iot.github.io/content/win10/StartCoding.htm)

In order to ensure you are talking to the right robot kit in the room, right click on the project in the project explorer and click on properties. in the project properties page, choose the Debug tab and ensure that the Target Device is set to Remote Machine and that the Remote Machine fields has the IP address written on the post it on the table at your station. It is as simple as that. A remote debugger client is running on the Windows image on the Pi that allows the deployment of the app and the remote debugging.

Ensure that in the Visual Studio Ribbon you have ARM as the configuration (NOT x64 or x86 as the Pi is an ARM device) and that the target device is set to Remote Machine. Press F5 to deploy and start the app.

## Make the app launch at boot

With Windows IoT Core you can set a Universal app as the default app, meaning that the app will be launched at boot and that a watchdog service will ensure the app is relaunched if crashed.
In order to set your app as default, you need to use a PowerShell remote session (see [here](http://ms-iot.github.io/content/win10/samples/PowerShell.htm) for instructions on how to establish remote PowerShell session).
To make your app the default one, follow the steps highlighted at the end of [this](http://ms-iot.github.io/content/win10/samples/HelloWorld.htm) sample.


