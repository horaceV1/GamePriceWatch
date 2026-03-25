using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;

namespace GamePriceWatch;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void CouponButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button) return;

        var couponCode = button.Tag as string;
        if (string.IsNullOrEmpty(couponCode)) return;

        // Copy to clipboard
        try { Clipboard.SetText(couponCode); } catch { return; }

        // Find the CouponContent and CopiedContent panels inside the button template
        var couponContent = FindChildByName(button, "CouponContent") as UIElement;
        var copiedContent = FindChildByName(button, "CopiedContent") as UIElement;

        if (couponContent == null || copiedContent == null) return;

        // Create smooth fade animation: fade out coupon, fade in "Copied!", then reverse
        var duration = new Duration(TimeSpan.FromMilliseconds(200));
        var holdDuration = TimeSpan.FromMilliseconds(1200);

        // Fade out the coupon text
        var fadeOutCoupon = new DoubleAnimation(1, 0, duration) { EasingFunction = new QuadraticEase() };
        // Fade in the "Copied!" text
        var fadeInCopied = new DoubleAnimation(0, 1, duration) { BeginTime = TimeSpan.FromMilliseconds(150), EasingFunction = new QuadraticEase() };
        // After hold, fade out "Copied!"
        var fadeOutCopied = new DoubleAnimation(1, 0, duration) { BeginTime = holdDuration, EasingFunction = new QuadraticEase() };
        // Fade coupon text back in
        var fadeInCoupon = new DoubleAnimation(0, 1, duration)
        {
            BeginTime = holdDuration + TimeSpan.FromMilliseconds(50),
            EasingFunction = new QuadraticEase()
        };

        couponContent.BeginAnimation(UIElement.OpacityProperty, fadeOutCoupon);
        copiedContent.BeginAnimation(UIElement.OpacityProperty, fadeInCopied);

        // Schedule the reverse
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = holdDuration + TimeSpan.FromMilliseconds(50)
        };
        timer.Tick += (s, _) =>
        {
            timer.Stop();
            copiedContent.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0, duration) { EasingFunction = new QuadraticEase() });
            couponContent.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration)
            {
                BeginTime = TimeSpan.FromMilliseconds(50),
                EasingFunction = new QuadraticEase()
            });
        };
        timer.Start();

        // Update status bar via ViewModel
        if (DataContext is GamePriceWatch.ViewModels.MainViewModel vm)
            vm.StatusText = $"Copied coupon code: {couponCode}";
    }

    /// <summary>
    /// Recursively finds a named child element within the visual tree.
    /// </summary>
    private static DependencyObject? FindChildByName(DependencyObject parent, string name)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && fe.Name == name)
                return child;
            var result = FindChildByName(child, name);
            if (result != null) return result;
        }
        return null;
    }
}