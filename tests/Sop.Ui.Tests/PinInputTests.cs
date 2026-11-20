using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sop.Ui.Controls;
using Xunit;

namespace Sop.Ui.Tests
{
    public class PinInputTests
    {
        private static readonly object Gate = new object();

        private static void EnsureApp()
        {
            lock (Gate)
            {
                if (Application.Current == null)
                {
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    Theme.Initialize(app);
                }
            }
            Theme.Mode = ThemeMode.Light;
            Theme.IsRtl = false;
        }

        private static Window Show(UIElement content)
        {
            var window = new Window
            {
                Left = -20000,
                Top = -20000,
                Width = 800,
                Height = 600,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None,
                Content = content,
            };
            Theme.Attach(window);
            window.Show();
            window.UpdateLayout();
            return window;
        }

        [StaFact]
        public void PinInput_DefaultProperties_AreSetCorrectly()
        {
            var pin = new PinInput();
            Assert.Equal(4, pin.Length);
            Assert.Equal(string.Empty, pin.Value);
            Assert.Equal(string.Empty, pin.Pin);
            Assert.True(pin.IsMasked);
            Assert.Equal("•", pin.MaskChar);
            Assert.False(pin.IsReadOnly);
            Assert.Equal(ControlSize.Md, pin.Size);
        }

        [StaFact]
        public void PinInput_ValueAndPin_AreSynchronized()
        {
            var pin = new PinInput();
            pin.Value = "1234";
            Assert.Equal("1234", pin.Pin);

            pin.Pin = "5678";
            Assert.Equal("5678", pin.Value);
        }

        [StaFact]
        public void PinInput_Length_ClampsBetweenTwoAndTwelve()
        {
            var pin = new PinInput();
            pin.Length = 1;
            Assert.Equal(2, pin.Length);

            pin.Length = 6;
            Assert.Equal(6, pin.Length);

            pin.Length = 20;
            Assert.Equal(12, pin.Length);
        }

        [StaFact]
        public void PinInput_CompletedEvent_FiresWhenFullLengthEntered()
        {
            var pin = new PinInput { Length = 4 };
            bool completedFired = false;
            pin.Completed += (s, e) => completedFired = true;

            pin.Value = "123";
            Assert.False(completedFired);

            pin.Value = "1234";
            Assert.True(completedFired);
        }

        [StaFact]
        public void PinInput_Clear_ResetsValue()
        {
            var pin = new PinInput { Length = 4, Value = "1234" };
            Assert.Equal("1234", pin.Value);

            pin.Clear();
            Assert.Equal(string.Empty, pin.Value);
            Assert.Equal(string.Empty, pin.Pin);
        }

        [StaFact]
        public void PinDigitBox_ReceivesTemplateAndTokensFromTheTheme()
        {
            EnsureApp();
            var box = new PinDigitBox();
            var window = Show(box);
            try
            {
                Assert.NotNull(box.Template);
                var frame = box.Template.FindName("Frame", box) as Border;
                Assert.NotNull(frame);
                Assert.NotNull(frame!.Background);
            }
            finally
            {
                window.Close();
            }
        }

        [StaFact]
        public void PinInput_CreatesStyledPinDigitBoxes()
        {
            EnsureApp();
            var pin = new PinInput { Length = 4, Value = "1234" };
            var window = Show(pin);
            try
            {
                Assert.NotNull(pin.Template);
                var panel = VisualTreeHelper.GetChild(pin, 0) as Panel;
                Assert.NotNull(panel);
                Assert.Equal(4, panel!.Children.Count);

                var first = panel.Children[0] as PinDigitBox;
                Assert.NotNull(first);
                Assert.NotNull(first!.Template);
                Assert.Equal("•", first.Text);
                Assert.Equal(44, first.Width);
                Assert.Equal(48, first.Height);
            }
            finally
            {
                window.Close();
            }
        }

        [StaFact]
        public void PinInput_SizeVariants_PropagateCorrectDimensions()
        {
            EnsureApp();
            var pinSm = new PinInput { Size = ControlSize.Sm };
            var pinLg = new PinInput { Size = ControlSize.Lg };
            var stack = new StackPanel();
            stack.Children.Add(pinSm);
            stack.Children.Add(pinLg);
            var window = Show(stack);
            try
            {
                var panelSm = VisualTreeHelper.GetChild(pinSm, 0) as Panel;
                var boxSm = panelSm!.Children[0] as PinDigitBox;
                Assert.NotNull(boxSm);
                Assert.Equal(36, boxSm!.Width);
                Assert.Equal(36, boxSm.Height);

                var panelLg = VisualTreeHelper.GetChild(pinLg, 0) as Panel;
                var boxLg = panelLg!.Children[0] as PinDigitBox;
                Assert.NotNull(boxLg);
                Assert.Equal(52, boxLg!.Width);
                Assert.Equal(56, boxLg.Height);
            }
            finally
            {
                window.Close();
            }
        }

        [StaFact]
        public void PinInput_SwitchingToDark_ReThemesDigitBoxSurfaces()
        {
            EnsureApp();
            var pin = new PinInput { Length = 4 };
            var window = Show(pin);
            try
            {
                var panel = VisualTreeHelper.GetChild(pin, 0) as Panel;
                var first = (PinDigitBox)panel!.Children[0];
                var frame = (Border)first.Template.FindName("Frame", first);

                var lightBrush = frame.Background as SolidColorBrush;
                Assert.NotNull(lightBrush);
                var lightColor = lightBrush!.Color;

                Theme.Mode = ThemeMode.Dark;
                window.UpdateLayout();

                var darkBrush = frame.Background as SolidColorBrush;
                Assert.NotNull(darkBrush);
                var darkColor = darkBrush!.Color;

                Assert.NotEqual(lightColor, darkColor);
            }
            finally
            {
                Theme.Mode = ThemeMode.Light;
                window.Close();
            }
        }
    }
}
