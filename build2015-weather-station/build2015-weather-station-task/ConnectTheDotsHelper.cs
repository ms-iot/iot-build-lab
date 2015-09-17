using System;
using System.Diagnostics;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Web.Http;

namespace build2015_weather_station_task
{
    public sealed partial class StartupTask
    {
        // App Settings variables
        AppSettings localSettings = new AppSettings();

        // Http connection string, SAS tokem and client
        Uri uri;
        private string sas;
        HttpClient httpClient = new HttpClient();
        bool EventHubConnectionInitialized = false;

        string UrlEncode(string value)
        {
            return Uri.EscapeDataString(value).Replace("%20", "+");
        }
        /// <summary>
        /// Validate the settings 
        /// </summary>
        /// <returns></returns>
        bool ValidateSettings()
        {
            if ((localSettings.ServicebusNamespace == "") ||
                (localSettings.EventHubName == "") ||
                (localSettings.KeyName == "") ||
                (localSettings.Key == "") ||
                (localSettings.DisplayName == "") ||
                (localSettings.Organization == "") ||
                (localSettings.Location == ""))
            {
                this.localSettings.SettingsSet = false;
                return false;
            }

            this.localSettings.SettingsSet = true;
            return true;

        }

        /// <summary>
        /// When appSettings popup closes, apply new settings to sensors collection
        /// </summary>
        void SaveSettings()
        {
            if (ValidateSettings())
            {
                ApplySettingsToSensors();
                InitEventHubConnection();
            }
        }

        /// <summary>
        /// reset SAS token (as it expires after a given time we need to allow the app to renew the token regularly)
        /// </summary>
        void UpdateSasToken()
        {
            sas = SasTokenHelper();
            httpClient.DefaultRequestHeaders.Authorization = new Windows.Web.Http.Headers.HttpCredentialsHeaderValue("SharedAccessSignature", sas);
        }

        /// <summary>
        ///  Apply settings to sensors collection
        /// </summary>
        private void ApplySettingsToSensors()
        {
            foreach (ConnectTheDotsSensor sensor in sensors)
            {
                sensor.displayname = localSettings.DisplayName;
                sensor.location = localSettings.Location;
                sensor.organization = localSettings.Organization;
            }
        }

        /// <summary>
        /// Send message to Azure Event Hub using HTTP/REST API
        /// </summary>
        /// <param name="message"></param>
        private async void sendMessage(string message)
        {
            if (this.EventHubConnectionInitialized)
            {
                try
                {
                    HttpStringContent content = new HttpStringContent(message, Windows.Storage.Streams.UnicodeEncoding.Utf8, "application/json");
                    HttpResponseMessage postResult = await httpClient.PostAsync(uri, content);

                    if (postResult.IsSuccessStatusCode)
                    {
                        Debug.WriteLine("Message Sent: {0}", content);
                    }
                    else
                    {
                        Debug.WriteLine("Failed sending message: {0}", postResult.ReasonPhrase);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Exception when sending message:" + e.Message);
                }
            }
        }

        /// <summary>
        /// Helper function to get SAS token for connecting to Azure Event Hub
        /// </summary>
        /// <returns></returns>
        private string SasTokenHelper()
        {
            int expiry = (int)DateTime.UtcNow.AddMinutes(20).Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            string stringToSign = UrlEncode(uri.ToString()) + "\n" + expiry.ToString();
            string signature = HmacSha256(localSettings.Key.ToString(), stringToSign);
            string token = string.Format("sr={0}&sig={1}&se={2}&skn={3}", UrlEncode(uri.ToString()), UrlEncode(signature), expiry, localSettings.KeyName.ToString());

            return token;
        }

        /// <summary>
        /// Because Windows.Security.Cryptography.Core.MacAlgorithmNames.HmacSha256 doesn't
        /// exist in WP8.1 context we need to do another implementation
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public string HmacSha256(string key, string kvalue)
        {
            var keyStrm = CryptographicBuffer.ConvertStringToBinary(key, BinaryStringEncoding.Utf8);
            var valueStrm = CryptographicBuffer.ConvertStringToBinary(kvalue, BinaryStringEncoding.Utf8);

            var objMacProv = MacAlgorithmProvider.OpenAlgorithm(MacAlgorithmNames.HmacSha256);
            var hash = objMacProv.CreateHash(keyStrm);
            hash.Append(valueStrm);

            return CryptographicBuffer.EncodeToBase64String(hash.GetValueAndReset());
        }

        /// <summary>
        /// Initialize Event Hub connection
        /// </summary>
        private bool InitEventHubConnection()
        {
            try
            {
                uri = new Uri("https://" + localSettings.ServicebusNamespace +
                              ".servicebus.windows.net/" + localSettings.EventHubName +
                              "/publishers/" + localSettings.DisplayName + "/messages");
                UpdateSasToken();
                EventHubConnectionInitialized = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
