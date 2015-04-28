using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

namespace WeatherStationTask
{
    namespace Sparkfun
    {
        public sealed class WeatherShield
        {
            // LED Control Pins
            private const int STATUS_LED_BLUE_PIN = 6;
            private const int STATUS_LED_GREEN_PIN = 5;

            // I2C Addresses
            private const ushort HTDU21D_I2C_ADDRESS = 0x0040;
            private const ushort MPL3115A2_I2C_ADDRESS = 0x0060;

            // HTDU21D I2C Commands
            private const byte SAMPLE_TEMPERATURE_HOLD = 0xE3;
            private const byte SAMPLE_HUMIDITY_HOLD = 0xE5;

            // MPL3115A2 Registers
            private const byte CTRL_REG1 = 0x26;
            private const byte OUT_P_MSB = 0x01;

            // I2C Devices
            private I2cDevice htdu21d;  // Humidity and temperature sensor
            private I2cDevice mpl3115a2;  // Altitue, pressure and temperature sensor

            /// <summary>
            /// Blue status LED on shield
            /// </summary>
            /// <remarks>
            /// This object will be created in InitAsync(). The set method will
            /// be marked private, because the object itself will not change, only
            /// the value it drives to the pin.
            /// </remarks>
            public GpioPin BlueLEDPin { get; private set; }

            /// <summary>
            /// Green status LED on shield
            /// </summary>
            /// <remarks>
            /// This object will be created in InitAsync(). The set method will
            /// be marked private, because the object itself will not change, only
            /// the value it drives to the pin.
            /// </remarks>
            public GpioPin GreenLEDPin { get; private set; }

            /// <summary>
            /// Initialize the Sparkfun Weather Shield
            /// </summary>
            /// <remarks>
            /// Setup and instantiate the I2C device objects for the HTDU21D and the MPL3115A2
            /// and initialize the blue and green status LEDs.
            /// </remarks>
            internal async Task BeginAsync()
            {
                /*
                 * Acquire the GPIO controller
                 * MSDN GPIO Reference: https://msdn.microsoft.com/en-us/library/windows/apps/windows.devices.gpio.aspx
                 * 
                 * Get the default GpioController
                 */
                GpioController gpio = GpioController.GetDefault();

                /*
                 * Initialize the blue LED and set to "off"
                 *
                 * Instantiate the blue LED pin object
                 * Write the GPIO pin value of low on the pin
                 * Set the GPIO pin drive mode to output
                 */
                BlueLEDPin = gpio.OpenPin(STATUS_LED_BLUE_PIN, GpioSharingMode.Exclusive);
                BlueLEDPin.Write(GpioPinValue.Low);
                BlueLEDPin.SetDriveMode(GpioPinDriveMode.Output);

                /*
                 * Initialize the green LED and set to "off"
                 * 
                 * Instantiate the green LED pin object
                 * Write the GPIO pin value of low on the pin
                 * Set the GPIO pin drive mode to output
                 */
                GreenLEDPin = gpio.OpenPin(STATUS_LED_GREEN_PIN, GpioSharingMode.Exclusive);
                GreenLEDPin.Write(GpioPinValue.Low);
                GreenLEDPin.SetDriveMode(GpioPinDriveMode.Output);

                /*
                 * Acquire the I2C device
                 * MSDN I2C Reference: https://msdn.microsoft.com/en-us/library/windows/apps/windows.devices.i2c.aspx
                 *
                 * Use the I2cDevice device selector to create an advanced query syntax string
                 * Use the Windows.Devices.Enumeration.DeviceInformation class to create a collection using the advanced query syntax string
                 * Take the device id of the first device in the collection
                 */
                string advanced_query_syntax = I2cDevice.GetDeviceSelector("I2C1");
                DeviceInformationCollection device_information_collection = await DeviceInformation.FindAllAsync(advanced_query_syntax);
                string deviceId = device_information_collection[0].Id;

                /*
                 * Establish an I2C connection to the HTDU21D
                 *
                 * Instantiate the I2cConnectionSettings using the device address of the HTDU21D
                 * - Set the I2C bus speed of connection to fast mode
                 * - Set the I2C sharing mode of the connection to shared
                 *
                 * Instantiate the the HTDU21D I2C device using the device id and the I2cConnectionSettings
                 */
                I2cConnectionSettings htdu21d_connection = new I2cConnectionSettings(HTDU21D_I2C_ADDRESS);
                htdu21d_connection.BusSpeed = I2cBusSpeed.FastMode;
                htdu21d_connection.SharingMode = I2cSharingMode.Shared;

                htdu21d = await I2cDevice.FromIdAsync(deviceId, htdu21d_connection);

                /*
                 * Establish an I2C connection to the MPL3115A2
                 *
                 * Instantiate the I2cConnectionSettings using the device address of the MPL3115A2
                 * - Set the I2C bus speed of connection to fast mode
                 * - Set the I2C sharing mode of the connection to shared
                 *
                 * Instantiate the the MPL3115A2 I2C device using the device id and the I2cConnectionSettings
                 */
                I2cConnectionSettings mpl3115a2_connection = new I2cConnectionSettings(MPL3115A2_I2C_ADDRESS);
                mpl3115a2_connection.BusSpeed = I2cBusSpeed.FastMode;
                mpl3115a2_connection.SharingMode = I2cSharingMode.Shared;

                mpl3115a2 = await I2cDevice.FromIdAsync(deviceId, mpl3115a2_connection);
            }

            /// <summary>
            /// Read altitude data
            /// </summary>
            /// <returns>
            /// Calculates the altitude in meters (m) using the US Standard Atmosphere 1976 (NASA) formula
            /// </returns>
            public float Altitude
            {
                get
                {
                    double pressure_Pa = Pressure;

                    // Calculate using US Standard Atmosphere 1976 (NASA)
                    double altitude_m = (44330.77 * (1 - Math.Pow((pressure_Pa / 101326), 0.1902632)) /*+ OFF_H*/); // OFF_H (disabled) is the user offset

                    return Convert.ToSingle(altitude_m);
                }
            }

            /// <summary>
            /// Calculate relative humidity
            /// </summary>
            /// <returns>
            /// The relative humidity
            /// </returns>
            public float Humidity
            {
                get
                {
                    ushort raw_humidity_data = RawHumidity;
                    double humidity_RH = (((125.0 * raw_humidity_data) / 65536) - 6.0);

                    return Convert.ToSingle(humidity_RH);
                }
            }

            /// <summary>
            /// Read pressure data
            /// </summary>
            /// <returns>
            /// The pressure in Pascals (Pa)
            /// </returns>
            public float Pressure
            {
                get
                {
                    uint raw_pressure_data = RawPressure;
                    double pressure_Pa = ((raw_pressure_data >> 6) + (((raw_pressure_data >> 4) & 0x03) / 4.0));

                    return Convert.ToSingle(pressure_Pa);
                }
            }

            /// <summary>
            /// Calculate current temperature
            /// </summary>
            /// <returns>
            /// The temperature in Celcius (C)
            /// </returns>
            public float Temperature
            {
                get
                {
                    ushort raw_temperature_data = RawTemperature;
                    double temperature_C = (((175.72 * raw_temperature_data) / 65536) - 46.85);

                    return Convert.ToSingle(temperature_C);
                }
            }

            private ushort RawHumidity
            {
                get
                {
                    ushort humidity = 0;
                    byte[] i2c_humidity_data = new byte[3];

                    /*
                     * Request humidity data from the HTDU21D
                     * HTDU21D datasheet: http://dlnmh9ip6v2uc.cloudfront.net/datasheets/BreakoutBoards/HTU21D.pdf
                     *
                     * Write the SAMPLE_HUMIDITY_HOLD command (0xE5) to the HTDU21D
                     * - HOLD means it will block the I2C line while the HTDU21D calculates the humidity value
                     *
                     * Read the three bytes returned by the HTDU21D
                     * - byte 0 - MSB of the humidity
                     * - byte 1 - LSB of the humidity
                     * - byte 2 - CRC
                     *
                     * NOTE: Holding the line allows for a `WriteRead` style transaction
                     */
                    htdu21d.WriteRead(new byte[] { SAMPLE_HUMIDITY_HOLD }, i2c_humidity_data);

                    /*
                     * Reconstruct the result using the first two bytes returned from the device
                     *
                     * NOTE: Zero out the status bits (bits 0 and 1 of the LSB), but keep them in place
                     * - status bit 0 - not assigned
                     * - status bit 1
                     * -- off = temperature data
                     * -- on = humdity data
                     */
                    humidity = (ushort)(i2c_humidity_data[0] << 8);
                    humidity |= (ushort)(i2c_humidity_data[1] & 0xFC);

                    /*
                     * Test the integrity of the data
                     *
                     * Ensure the data returned is humidity data (hint: byte 1, bit 1)
                     * Test cyclic redundancy check (CRC) byte
                     *
                     * WARNING: HTDU21D firmware error - XOR CRC byte with 0x62 before attempting to validate
                     */
                    bool humidity_data = (0x00 != (0x02 & i2c_humidity_data[1]));
                    if (!humidity_data) { return 0; }

                    bool valid_data = ValidHtdu21dCyclicRedundancyCheck(humidity, (byte)(i2c_humidity_data[2] ^ 0x62));
                    if (!valid_data) { return 0; }

                    return humidity;
                }
            }

            private uint RawPressure
            {
                get
                {
                    uint pressure = 0;
                    byte[] reg_data = new byte[1];
                    byte[] raw_pressure_data = new byte[3];

                    // Toggle one shot

                    /*
                     * Request pressure data from the MPL3115A2
                     * MPL3115A2 datasheet: http://dlnmh9ip6v2uc.cloudfront.net/datasheets/Sensors/Pressure/MPL3115A2.pdf
                     *
                     * Update Control Register 1 Flags
                     * - Read data at CTRL_REG1 (0x26) on the MPL3115A2
                     * - Update the SBYB (bit 0) and OST (bit 1) flags to STANDBY and initiate measurement, respectively.
                     * -- SBYB flag (bit 0)
                     * --- off = Part is in STANDBY mode
                     * --- on = Part is ACTIVE 
                     * -- OST flag (bit 1)
                     * --- off = auto-clear
                     * --- on = initiate measurement
                     * - Write the resulting value back to Control Register 1
                     */
                    mpl3115a2.WriteRead(new byte[] { CTRL_REG1 }, reg_data);
                    reg_data[0] &= 0xFE;  // ensure SBYB (bit 0) is set to STANDBY
                    reg_data[0] |= 0x02;  // ensure OST (bit 1) is set to initiate measurement
                    mpl3115a2.Write(new byte[] { CTRL_REG1, reg_data[0] });

                    /*
                     * Wait 10ms to allow MPL3115A2 to process the pressure value
                     */
                    Task.Delay(10);

                    /*
                     * Write the address of the register of the most significant byte for the pressure value, OUT_P_MSB (0x01)
                     * Read the three bytes returned by the MPL3115A2
                     * - byte 0 - MSB of the pressure
                     * - byte 1 - CSB of the pressure
                     * - byte 2 - LSB of the pressure
                     */
                    mpl3115a2.WriteRead(new byte[] { OUT_P_MSB }, raw_pressure_data);

                    /*
                     * Reconstruct the result using all three bytes returned from the device
                     */
                    pressure = (uint)(raw_pressure_data[0] << 16);
                    pressure |= (uint)(raw_pressure_data[1] << 8);
                    pressure |= raw_pressure_data[2];

                    return pressure;
                }
            }

            private ushort RawTemperature
            {
                get
                {
                    ushort temperature = 0;
                    byte[] i2c_temperature_data = new byte[3];

                    /*
                     * Request temperature data from the HTDU21D
                     * HTDU21D datasheet: http://dlnmh9ip6v2uc.cloudfront.net/datasheets/BreakoutBoards/HTU21D.pdf
                     *
                     * Write the SAMPLE_TEMPERATURE_HOLD command (0xE3) to the HTDU21D
                     * - HOLD means it will block the I2C line while the HTDU21D calculates the temperature value
                     *
                     * Read the three bytes returned by the HTDU21D
                     * - byte 0 - MSB of the temperature
                     * - byte 1 - LSB of the temperature
                     * - byte 2 - CRC
                     *
                     * NOTE: Holding the line allows for a `WriteRead` style transaction
                     */
                    htdu21d.WriteRead(new byte[] { SAMPLE_TEMPERATURE_HOLD }, i2c_temperature_data);

                    /*
                     * Reconstruct the result using the first two bytes returned from the device
                     *
                     * NOTE: Zero out the status bits (bits 0 and 1 of the LSB), but keep them in place
                     * - status bit 0 - not assigned
                     * - status bit 1
                     * -- off = temperature data
                     * -- on = humdity data
                     */
                    temperature = (ushort)(i2c_temperature_data[0] << 8);
                    temperature |= (ushort)(i2c_temperature_data[1] & 0xFC);

                    /*
                     * Test the integrity of the data
                     *
                     * Ensure the data returned is temperature data (hint: byte 1, bit 1)
                     * Test cyclic redundancy check (CRC) byte
                     */
                    bool temperature_data = (0x00 == (0x02 & i2c_temperature_data[1]));
                    if (!temperature_data) { return 0; }

                    bool valid_data = ValidHtdu21dCyclicRedundancyCheck(temperature, i2c_temperature_data[2]);
                    if (!valid_data) { return 0; }

                    return temperature;
                }
            }

            private bool ValidHtdu21dCyclicRedundancyCheck(
                ushort data_,
                byte crc_
            )
            {
                /*
                 * Validate the 8-bit cyclic redundancy check (CRC) byte
                 * CRC: http://en.wikipedia.org/wiki/Cyclic_redundancy_check
                 * Generator polynomial x^8 + x^5 + x^4 + 1: 100110001(0x0131)
                 */

                const int CRC_BIT_LENGTH = 8;
                const int DATA_LENGTH = 16;
                const ushort GENERATOR_POLYNOMIAL = 0x0131;

                int crc_data = data_ << CRC_BIT_LENGTH;

                for (int i = (DATA_LENGTH - 1); 0 <= i; --i)
                {
                    if (0 == (0x01 & (crc_data >> (CRC_BIT_LENGTH + i)))) { continue; }
                    crc_data ^= (GENERATOR_POLYNOMIAL << i);
                }

                return (crc_ == crc_data);
            }
        }
    }
}