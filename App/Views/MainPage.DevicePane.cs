using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.Views;

public sealed partial class MainPage
{
    private bool _isDevicePaneCollapsed;

    private void CollapseDevicePaneButton_Click(object sender, RoutedEventArgs e)
    {
        _isDevicePaneCollapsed = true;
        UpdateDevicePaneUi();
    }

    private void ExpandDevicePaneButton_Click(object sender, RoutedEventArgs e)
    {
        _isDevicePaneCollapsed = false;
        UpdateDevicePaneUi();
    }

    private void UpdateDevicePaneUi()
    {
        if (DevicePaneColumn == null ||
            DeviceList == null ||
            CollapseDevicePaneButton == null ||
            DevicePaneCollapsedRail == null)
        {
            return;
        }

        DevicePaneColumn.Width = new GridLength(_isDevicePaneCollapsed ? 44 : 220);
        DeviceList.Visibility = _isDevicePaneCollapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseDevicePaneButton.Visibility = _isDevicePaneCollapsed ? Visibility.Collapsed : Visibility.Visible;
        DevicePaneCollapsedRail.Visibility = _isDevicePaneCollapsed ? Visibility.Visible : Visibility.Collapsed;
    }
}
