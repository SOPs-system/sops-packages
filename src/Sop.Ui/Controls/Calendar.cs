using System.Windows;

namespace Sop.Ui.Controls
{
    /// <summary>
    /// Sop Calendar control — authentic desktop-beta styled calendar with rounded date pills,
    /// tabular mono numerals, month/year navigation, and two-click date range support.
    /// </summary>
    public class Calendar : System.Windows.Controls.Calendar
    {
        static Calendar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(Calendar),
                new FrameworkPropertyMetadata(typeof(Calendar)));
        }
    }
}
