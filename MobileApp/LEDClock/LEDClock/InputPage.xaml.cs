using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Extensions;

namespace LEDClock
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class InputPage : ContentPage
	{
        private int offset = 0;
        ICharacteristic characteristic;

		public InputPage (ICharacteristic c)
		{
			InitializeComponent();
            characteristic = c;
		}

        private async void SegmentButton_Clicked(object sender, EventArgs e)
        {
            var btn = sender as Button;
            var num = int.Parse(btn.BindingContext.ToString()) + offset;
            await characteristic.WriteAsync(Encoding.UTF8.GetBytes($"$toggle,{num}]"));
        }

        private void NumberToggle_Clicked(object sender, EventArgs e)
        {
            switch (NumberToggle.Text)
            {
                case "Number 1":
                    NumberToggle.Text = "Number 2";
                    offset = 7;
                    break;
                case "Number 2":
                    NumberToggle.Text = "Dots";
                    offset = 14;
                    SegmentLayout.IsVisible = false;
                    DotLayout.IsVisible = true;
                    break;
                case "Dots":
                    NumberToggle.Text = "Number 3";
                    offset = 16;
                    SegmentLayout.IsVisible = true;
                    DotLayout.IsVisible = false;
                    break;
                case "Number 3":
                    NumberToggle.Text = "Number 4";
                    offset = 23;
                    break;
                case "Number 4":
                    NumberToggle.Text = "Number 1";
                    offset = 0;
                    break;
                default:
                    NumberToggle.Text = "Number 1";
                    SegmentLayout.IsVisible = true;
                    DotLayout.IsVisible = false;
                    break;
            }
        }

        private async void InvertButton_Clicked(object sender, EventArgs e)
        {
            await characteristic.WriteAsync(Encoding.UTF8.GetBytes($"$toggle,i]"));
        }

        private async void ClearButton_Clicked(object sender, EventArgs e)
        {
            await characteristic.WriteAsync(Encoding.UTF8.GetBytes($"$toggle,c]"));
        }
    }
}