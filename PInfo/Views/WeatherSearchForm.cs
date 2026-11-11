/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Searches Photon explicitly and opens weather for a chosen location while retaining the result list.
/// </summary>
public class WeatherSearchForm : AppForm
{
    // ● private
    readonly PhotonService fService;
    readonly TextBox fSearch = new();
    readonly Button fFind = new() { Content = "🔍" };
    readonly Button fPrevious = new() { Content = "←", IsEnabled = false };
    readonly Button fNext = new() { Content = "→", IsEnabled = false };
    readonly TextBlock fStatus = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock fPageText = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    readonly ProgressBar fProgress = new() { IsIndeterminate = true, IsVisible = false };
    readonly ListBox fList = new();
    readonly int fPageSize;
    CancellationTokenSource fCancellation;
    string fQuery;
    int fPage;
    List<JsonElement> fItems = new();
    bool fSearched;

    /// <summary>
    /// Reads an optional plain-text label from a Photon location.
    /// </summary>
    static string Label(JsonElement Properties, string Name)
    {
        return Properties.TryGetProperty(Name, out JsonElement Value) && Value.ValueKind == JsonValueKind.String
            ? Value.GetString() : string.Empty;
    }
    /// <summary>
    /// Describes administrative context without repeating the primary location name.
    /// </summary>
    static string Context(JsonElement Properties)
    {
        string Name = Label(Properties, "name");
        return string.Join(" · ", new[] { "district", "city", "county", "state", "country" }
            .Select(Key => Label(Properties, Key)).Where(Value => !string.IsNullOrWhiteSpace(Value)
                && !string.Equals(Name, Value, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase));
    }
    /// <summary>
    /// Creates a compact location row with administrative area and country context.
    /// </summary>
    Control CreateItem(JsonElement Item)
    {
        JsonElement Properties = Item.GetProperty("properties");
        TextBlock Title = new() { Text = Label(Properties, "name"), TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("ListItemTitle");
        TextBlock Description = new() { Text = Context(Properties), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        Description.Classes.Add("SecondaryText");
        StackPanel Text = new() { Spacing = 2 };
        Text.Children.Add(Title);
        Text.Children.Add(Description);
        Border Card = new() { Child = Text };
        Card.Classes.Add("ListItem");
        return Card;
    }
    /// <summary>
    /// Pages only the returned suggestions; Photon does not provide an exhaustive result total or offset paging.
    /// </summary>
    void DisplayPage()
    {
        int Pages = Math.Max(1, (fItems.Count + fPageSize - 1) / fPageSize);
        fPage = Math.Clamp(fPage, 0, Pages - 1);
        fList.ItemsSource = fItems.Skip(fPage * fPageSize).Take(fPageSize).ToList();
        fPageText.Text = $"Page {fPage + 1} / {Pages}";
        fPrevious.IsEnabled = fPage > 0;
        fNext.IsEnabled = fPage + 1 < Pages;
        if (fList.ItemCount > 0)
            fList.ScrollIntoView(fList.Items[0]);
    }
    /// <summary>
    /// Requests one suggestion set and ignores cancelled navigation.
    /// </summary>
    async Task SearchAsync()
    {
        if (fCancellation != null || !this.IsAttachedToVisualTree())
            return;
        if (string.IsNullOrWhiteSpace(fQuery))
        {
            fList.ItemsSource = null;
            fPrevious.IsEnabled = false;
            fNext.IsEnabled = false;
            fPageText.Text = string.Empty;
            fStatus.Text = "Enter a city name or part of a name, then press 🔍 or Enter.";
            return;
        }
        using CancellationTokenSource Cancellation = new();
        fCancellation = Cancellation;
        fSearched = false;
        fItems.Clear();
        fList.ItemsSource = null;
        fPageText.Text = string.Empty;
        fFind.IsEnabled = false;
        fPrevious.IsEnabled = false;
        fNext.IsEnabled = false;
        fList.IsEnabled = false;
        fProgress.IsVisible = true;
        fStatus.Text = "Searching locations. Please wait...";

        try
        {
            ApiClientResult Result = await fService.SearchAsync(fQuery, Cancellation.Token);
            if (Cancellation.IsCancellationRequested || !this.IsAttachedToVisualTree())
                return;
            if (Result.ResultStatus != ApiClientResultStatus.Success)
            {
                fStatus.Text = Result.ReasonPhrase;
                return;
            }
            using JsonDocument Document = JsonDocument.Parse(Result.ResponseJsonText);
            fItems = Document.RootElement.GetProperty("features").EnumerateArray().Select(Item => Item.Clone())
                .OrderBy(Item => Label(Item.GetProperty("properties"), "name"), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(Item => Context(Item.GetProperty("properties")), StringComparer.CurrentCultureIgnoreCase).ToList();
            fSearched = true;
            fStatus.Text = (fItems.Count == 0 ? "No locations found. Try changing the search."
                : $"{fItems.Count:N0} suggested locations. Select one to see the weather.")
                + (string.IsNullOrWhiteSpace(Result.ReasonPhrase) ? string.Empty : "\n" + Result.ReasonPhrase);
            DisplayPage();
        }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
        {
            // Leaving the form cancels its outstanding request.
        }
        catch (Exception Ex)
        {
            if (!Cancellation.IsCancellationRequested && this.IsAttachedToVisualTree())
            {
                ExceptionLog.WriteException("WeatherSearchForm.SearchAsync", Ex, false);
                fStatus.Text = "Locations could not be loaded. Press 🔍 to try again.";
            }
        }
        finally
        {
            if (fCancellation == Cancellation)
            {
                fCancellation = null;
                fProgress.IsVisible = false;
                fFind.IsEnabled = true;
                fList.IsEnabled = true;
                fPrevious.IsEnabled = fPage > 0;
                fNext.IsEnabled = (fPage + 1) * fPageSize < fItems.Count;
            }
        }
    }
    /// <summary>
    /// Starts an explicit search or moves locally between suggestion pages.
    /// </summary>
    async void OnAction(object Sender, RoutedEventArgs Args)
    {
        if (fCancellation != null)
            return;
        if (Sender == fFind)
        {
            fQuery = (fSearch.Text ?? string.Empty).Trim();
            fPage = 0;
            await SearchAsync();
        }
        else if (Sender == fPrevious && fPage > 0)
        {
            fPage--;
            DisplayPage();
        }
        else if (Sender == fNext && (fPage + 1) * fPageSize < fItems.Count)
        {
            fPage++;
            DisplayPage();
        }
    }
    /// <summary>
    /// Submits the editable query from the keyboard without starting a request for each keystroke.
    /// </summary>
    void OnSearchKeyDown(object Sender, Avalonia.Input.KeyEventArgs Args)
    {
        if (Args.Key == Avalonia.Input.Key.Enter)
        {
            Args.Handled = true;
            OnAction(fFind, new RoutedEventArgs());
        }
    }
    /// <summary>
    /// Opens the forecast for the selected point while retaining results and scroll position for Back.
    /// </summary>
    void OnSelectionChanged(object Sender, SelectionChangedEventArgs Args)
    {
        if (fList.SelectedItem is not JsonElement Item)
            return;
        fList.SelectedItem = null;
        if (fCancellation == null && this.IsAttachedToVisualTree())
        {
            JsonElement Properties = Item.GetProperty("properties");
            JsonElement Coordinates = Item.GetProperty("geometry").GetProperty("coordinates");
            MainView.Current?.PushForm(new WeatherForm(Label(Properties, "name"), Coordinates[1].GetDouble(),
                Coordinates[0].GetDouble(), string.Empty, Context(Properties)));
        }
    }
    /// <summary>
    /// Loads the initial search once, or retries a request interrupted by navigation.
    /// </summary>
    async void OnSearchLoaded(object Sender, RoutedEventArgs Args)
    {
        if (!fSearched)
            await SearchAsync();
    }
    /// <summary>
    /// Cancels requests when this search form is left.
    /// </summary>
    void OnSearchUnloaded(object Sender, RoutedEventArgs Args)
    {
        CancellationTokenSource Cancellation = fCancellation;
        fCancellation = null;
        Cancellation?.Cancel();
    }

    // ● constructor
    /// <summary>
    /// Creates an initially empty Weather search using the shared list page size.
    /// </summary>
    /// <param name="Service">The registered Photon location search service.</param>
    public WeatherSearchForm(PhotonService Service)
    {
        ArgumentNullException.ThrowIfNull(Service);
        fService = Service;
        fQuery = string.Empty;
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("FormBackground");
        Classes.Add("CompactTitle");
        Grid Layout = new() { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto,Auto"), Margin = new Thickness(12) };
        StackPanel Header = new() { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        TextBlock Title = new() { Text = "Weather", TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("FormTitle");
        Header.Children.Add(Title);
        TextBlock Hint = new()
        {
            Text = "Find a city or village · add a country to narrow the search",
            TextWrapping = TextWrapping.Wrap
        };
        Hint.Classes.Add("SecondaryText");
        Header.Children.Add(Hint);
        Layout.Children.Add(Header);
        fSearch.Text = fQuery;
        fSearch.PlaceholderText = "City name or partial name";
        Avalonia.Automation.AutomationProperties.SetName(fFind, "Search locations");
        fSearch.Classes.Add("SearchBox");
        fFind.Classes.Add("PrimaryButton");
        Grid SearchRow = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6, Margin = new Thickness(0, 0, 0, 8) };
        SearchRow.Children.Add(fSearch);
        Grid.SetColumn(fFind, 1);
        SearchRow.Children.Add(fFind);
        Grid.SetRow(SearchRow, 1);
        Layout.Children.Add(SearchRow);
        fStatus.Classes.Add("SecondaryText");
        Grid.SetRow(fStatus, 2);
        Layout.Children.Add(fStatus);
        Grid.SetRow(fProgress, 3);
        Layout.Children.Add(fProgress);
        fList.Classes.Add("ItemList");
        fList.ItemTemplate = new FuncDataTemplate<JsonElement>((Item, Scope) => CreateItem(Item));
        Grid.SetRow(fList, 4);
        Layout.Children.Add(fList);
        Grid Paging = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 6, 0, 0) };
        fPrevious.Classes.Add("PrimaryButton");
        fPrevious.Classes.Add("PageNavigation");
        fNext.Classes.Add("PrimaryButton");
        fNext.Classes.Add("PageNavigation");
        Avalonia.Automation.AutomationProperties.SetName(fPrevious, "Previous page");
        Avalonia.Automation.AutomationProperties.SetName(fNext, "Next page");
        fPageText.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        Grid.SetColumn(fPageText, 1);
        Grid.SetColumn(fNext, 2);
        Paging.Children.Add(fPrevious);
        Paging.Children.Add(fPageText);
        Paging.Children.Add(fNext);
        Grid.SetRow(Paging, 5);
        Layout.Children.Add(Paging);
        HyperlinkButton Attribution = new()
        {
            Content = new TextBlock
            {
                Text = "Photon · © OpenStreetMap contributors · ODbL", FontSize = 11, TextWrapping = TextWrapping.Wrap
            },
            NavigateUri = new Uri("https://www.openstreetmap.org/copyright"),
            MinHeight = 0, Padding = new Thickness(0), Margin = new Thickness(0, 4, 0, 0)
        };
        Grid.SetRow(Attribution, 6);
        Layout.Children.Add(Attribution);
        Content = Layout;
        fFind.Click += OnAction;
        fSearch.KeyDown += OnSearchKeyDown;
        fPrevious.Click += OnAction;
        fNext.Click += OnAction;
        fList.SelectionChanged += OnSelectionChanged;
        Loaded += OnSearchLoaded;
        Unloaded += OnSearchUnloaded;
    }
}
