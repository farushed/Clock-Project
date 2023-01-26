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
    public partial class ColoursPage : ContentPage
    {
        ICharacteristic characteristic;
        public ColoursPage(ICharacteristic c)
        {
            InitializeComponent();
            characteristic = c;
        }

        private async Task<string> AskWhichLeds()
        {
            var which = await DisplayActionSheet("Which LEDs?", "Cancel", null, "All", "Hours", "Minutes", "Dots");
            if (which == "All")
                return "all";
            if (which == "Hours")
                return "hrs";
            if (which == "Minutes")
                return "mns";
            if (which == "Dots")
                return "dts";
            return null;
        }

        private async void SendButton_Clicked(object sender, EventArgs e)
        {
            var which = await AskWhichLeds();
            if (which == null)
                return;
            await characteristic.WriteAsync(Encoding.UTF8.GetBytes($"$rgb,{which},{ColorPicker.PickedColor.ToHex().Substring(3)}]"));
        }
        private async void BrightnessButton_Clicked(object sender, EventArgs e)
        {
            var which = await AskWhichLeds();
            if (which == null)
                return;
            await characteristic.WriteAsync(Encoding.UTF8.GetBytes($"$brightness,{which},{(int)BrightnessSlider.Value}]"));
        }

        private async void SendHexButton_Clicked(object sender, EventArgs e)
        {
            var result = await DisplayPromptAsync("Input colour:", null, "OK", "Cancel", "FFFFFF");
            if (result == null)
                return;
            if (result.Length != 6 || !Int32.TryParse(result, System.Globalization.NumberStyles.HexNumber, null, out int _))
                return;

            var which = await AskWhichLeds();
            if (which == null)
                return;
            await characteristic.WriteAsync(Encoding.UTF8.GetBytes($"$rgb,{which},{result.ToUpper()}]"));
        }

        private async void OtherButton_Clicked(object sender, EventArgs e)
        {
            var which = await DisplayActionSheet("Other colour modes", "Cancel", null, "Rainbow", "Rainbow wave", "Idk");
            if (which == null || which == "Cancel")
                return;

            await characteristic.WriteAsync(Encoding.UTF8.GetBytes($"${which.ToLower()}]"));
        }

        private void ColorPicker_PickedColorChanged(object sender, Color colorPicked)
        {
            ColorPickerFrame.BorderColor = colorPicked;
            ColorPickerFrame.BackgroundColor = colorPicked;
            
            SendButton.Text = $"Send 0x{colorPicked.ToHex().Substring(3)}";
        }

        private void BrightnessSlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            BrightnessButton.Text = $"Brightness {(int)e.NewValue}";
        }
    }
}