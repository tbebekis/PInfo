/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */
namespace PInfo.Views;

/// <summary>Displays the available public services.</summary>
public partial class ServiceListForm : AppForm
{
    // ● private
    /// <summary>Opens the form registered for the service attached to the clicked button.</summary>
    void AnyClick(object Sender, RoutedEventArgs Args)
    {
        if (Sender is Button ServiceButton && ServiceButton.DataContext is PublicServiceItem Item)
            MainView.Current?.OpenService(Item);
    }

    // ● constructor
    /// <summary>Creates the service list view.</summary>
    public ServiceListForm()
    {
        InitializeComponent();
        ServiceList.ItemsSource = AppHost.PublicServices;
    }
}
