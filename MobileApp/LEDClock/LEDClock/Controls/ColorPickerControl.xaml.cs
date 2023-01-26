﻿using System;
using SkiaSharp;
using SkiaSharp.Views.Forms;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LEDClock.Controls
{
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class ColorPickerControl : ContentView
	{
        public event EventHandler<Color> PickedColorChanged;

        public static readonly BindableProperty PickedColorProperty
            = BindableProperty.Create(
                nameof(PickedColor),
                typeof(Color),
                typeof(ColorPickerControl));

        public Color PickedColor
        {
            get { return (Color)GetValue(PickedColorProperty); }
            set { SetValue(PickedColorProperty, value); }
        }

        private SKPoint _lastTouchPoint = new SKPoint();

        public ColorPickerControl()
        {
            InitializeComponent();
        }

        private void SkCanvasView_OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var skImageInfo = e.Info;
            var skSurface = e.Surface;
            var skCanvas = skSurface.Canvas;

            var skCanvasWidth = skImageInfo.Width;
            var skCanvasHeight = skImageInfo.Height;

            skCanvas.Clear(SKColors.White);

            // Draw colour spectrum
            using (var paint = new SKPaint())
            {
                paint.IsAntialias = true;

                // Initiate the primary Color list
                // picked up from Google Web Color Picker
                var colors = new SKColor[]
                {
                new SKColor(255, 0, 0), // Red
                new SKColor(255, 255, 0), // Yellow
                new SKColor(0, 255, 0), // Green (Lime)
                new SKColor(0, 255, 255), // Aqua
                new SKColor(0, 0, 255), // Blue
                new SKColor(255, 0, 255), // Fuchsia
                new SKColor(255, 0, 0), // Red
                };

                // create gradient shader between colours
                using (var shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(skCanvasWidth, 0),
                    colors,
                    null,
                    SKShaderTileMode.Clamp))
                {
                    paint.Shader = shader;
                    skCanvas.DrawPaint(paint);
                }
            }

            // Draw darker gradient spectrum
            using (var paint = new SKPaint())
            {
                paint.IsAntialias = true;

                // Initiate the darkened primary color list
                var colors = new SKColor[]
                {
                    SKColors.White,
                    SKColors.Transparent,
                    SKColors.Transparent,
                    SKColors.Black
                };

                // create the gradient shader 
                using (var shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0),
                    new SKPoint(0, skCanvasHeight),
                    colors,
                    null,
                    SKShaderTileMode.Clamp))
                {
                    paint.Shader = shader;
                    skCanvas.DrawPaint(paint);
                }
            }

            // the color of the current Touch point
            SKColor touchPointColor;

            // create a 1x1 bitmap (auto allocates the pixel buffer)
            using (SKBitmap bitmap = new SKBitmap(skImageInfo))
            {
                // get the pixel buffer for the bitmap
                IntPtr dstpixels = bitmap.GetPixels();

                // read the surface into the bitmap
                skSurface.ReadPixels(skImageInfo,
                    dstpixels,
                    skImageInfo.RowBytes,
                    (int)_lastTouchPoint.X, (int)_lastTouchPoint.Y);

                // access the color
                touchPointColor = bitmap.GetPixel(0, 0);
            }

            // Paint the Touch point (feedback)
            using (SKPaint paintTouchPoint = new SKPaint())
            {
                paintTouchPoint.Style = SKPaintStyle.Fill;
                paintTouchPoint.Color = SKColors.White;
                paintTouchPoint.IsAntialias = true;
                // Outer white circle
                var outerRingRadius = /*((float)skCanvasWidth / (float)skCanvasHeight) * (float)*/25; //18
                skCanvas.DrawCircle(_lastTouchPoint.X, _lastTouchPoint.Y, outerRingRadius, paintTouchPoint);

                // Draw inner circle in picked color
                paintTouchPoint.Color = touchPointColor;
                var innerRingRadius = /*((float)skCanvasWidth / (float)skCanvasHeight) * (float)*/20; //12
                skCanvas.DrawCircle(_lastTouchPoint.X, _lastTouchPoint.Y, innerRingRadius, paintTouchPoint);
            }

            PickedColor = touchPointColor.ToFormsColor();
            PickedColorChanged?.Invoke(this, PickedColor);
        }

        private void SkCanvasView_OnTouch(object sender, SKTouchEventArgs e)
        {
            _lastTouchPoint = e.Location;

            var canvasSize = SkCanvasView.CanvasSize;

            // Check for each touch point XY position to be inside Canvas
            // Ignore and Touch event ocurred outside the Canvas region
            if ((e.Location.X > 0 && e.Location.X < canvasSize.Width)
                && (e.Location.Y > 0 && e.Location.Y < canvasSize.Height))
            {
                e.Handled = true;

                SkCanvasView.InvalidateSurface();
            }
        }
	}
}