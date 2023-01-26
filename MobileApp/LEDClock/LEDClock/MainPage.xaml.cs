using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xamarin.Forms;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Extensions;
using Plugin.BLE.Abstractions.Exceptions;

namespace LEDClock
{
    public partial class MainPage : ContentPage
    {
        CancellationTokenSource cts;
        IBluetoothLE ble;
        IAdapter adapter;
        public static IDevice device;
        public static IService service;
        public static ICharacteristic characteristic;

        public MainPage()
        {
            InitializeComponent();

            cts = new CancellationTokenSource();

            ble = CrossBluetoothLE.Current;
            adapter = CrossBluetoothLE.Current.Adapter;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (ble.State != BluetoothState.On)
            {
                ConnectionLabel.Text = "Bluetooth is off";
                return;
            }

            string ble_file_path = Path.Combine(App.FolderPath, "ble.txt");
            if (File.Exists(ble_file_path))
            {
                var lines = File.ReadAllLines(ble_file_path);

                //ConnectionLabel.Text = $"\"{lines[0]}\"\n\"{lines[1]}\"\n\"{lines[2]}\"";
                Guid device_id = new Guid(lines[0]);
                Guid service_id = new Guid(lines[1]);
                Guid characteristic_id = new Guid(lines[2]);

                if (device?.Id != device_id || service?.Id != service_id || characteristic?.Id != characteristic_id)
                {
                    try
                    {
                        device = await adapter.ConnectToKnownDeviceAsync(device_id, cancellationToken: cts.Token);
                        service = await device.GetServiceAsync(service_id);
                        characteristic = await service.GetCharacteristicAsync(characteristic_id);
                    }
                    catch (Exception e)
                    {
                        ConnectionLabel.Text = $"Error connecting: {e.GetType()}: {e.Message}";
                        return;
                    }

                    ConnectionLabel.Text = $"Connected to {device.Name}";
                    ConnectionLabel.TextColor = Color.LightGreen;
                    Enable_ble_buttons();
                }
            }
            else
            {
                ConnectionLabel.Text = "Please select a device in the settings page.";
            }
        }

        private void Enable_ble_buttons()
        {
            ShowClockButton.IsEnabled = true;
            SyncTimeButton.IsEnabled = true;
            InputButton.IsEnabled = true;
            ColourButton.IsEnabled = true;
        }

        private async void ShowClockButton_Clicked(object sender, EventArgs e)
        {
            await characteristic.WriteAsync(Encoding.UTF8.GetBytes("$showclock]"));
        }

        private async void ColourButton_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new ColoursPage(characteristic));
        }

        private async void Settings_Button_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SettingsPage());
        }

        private async void InputButton_Clicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new InputPage(characteristic));
        }

        private async void SyncTimeButton_Clicked(object sender, EventArgs e)
        {
            await characteristic.WriteAsync(Encoding.UTF8.GetBytes($"$time,{DateTime.Now:yyMMddHHmmss}]"));
        }
    }
}
