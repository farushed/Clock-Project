using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Exceptions;
using Plugin.BLE.Abstractions.EventArgs;

namespace LEDClock
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class SettingsPage : ContentPage
    {
        IBluetoothLE ble;
        IAdapter adapter;
        ObservableCollection<IDevice> device_list;

        public SettingsPage ()
		{
			InitializeComponent ();

            ble = CrossBluetoothLE.Current;
            adapter = CrossBluetoothLE.Current.Adapter;

            device_list = new ObservableCollection<IDevice>();
		}

        private async void StartScan_Clicked(object sender, EventArgs e)
        {
            device_list.Clear();
            BLEList.ItemsSource = device_list;

            adapter.ScanTimeout = 5000;
            StartScan.Text = "Restart scan";
            await adapter.StartScanningForDevicesAsync();

            adapter.DeviceDiscovered += Adapter_DeviceDiscovered;
        }

        private void Adapter_DeviceDiscovered(object sender, DeviceEventArgs e)
        {
            device_list.Add(e.Device);
        }

        private void BLEList_ItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            var device = e.SelectedItem as IDevice;

            string ble_file_path = Path.Combine(App.FolderPath, "ble.txt");

            File.WriteAllText(ble_file_path, $"{device.Id}\n0000FFE0-0000-1000-8000-00805F9B34FB\n0000FFE1-0000-1000-8000-00805F9B34FB\n");
        }
    }
}