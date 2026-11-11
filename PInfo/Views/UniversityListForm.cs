/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Shows a searchable, locally paged university catalogue and retains its state when opening details.
/// </summary>
public class UniversityListForm : AppListForm
{
    // ● private
    readonly UniversityService fService;
    readonly int fPageSize;
    readonly Button fPrevious = new() { Content = "←", IsEnabled = false };
    readonly Button fNext = new() { Content = "→", IsEnabled = false };
    readonly TextBlock fPageText = new()
    {
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
    };
    IReadOnlyList<JsonElement> fItems = Array.Empty<JsonElement>();
    int fPage;

    /// <summary>
    /// Creates a compact university row with country and optional region beneath the name.
    /// </summary>
    Control CreateItem(JsonElement Item)
    {
        TextBlock Title = new() { Text = Item.GetProperty("name").GetString(), TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("ListItemTitle");
        string Region = Item.TryGetProperty("state-province", out JsonElement Value) ? Value.GetString() : string.Empty;
        TextBlock Context = new()
        {
            Text = Item.GetProperty("country").GetString() + (string.IsNullOrWhiteSpace(Region) ? string.Empty : " · " + Region),
            FontSize = 12, TextWrapping = TextWrapping.Wrap
        };
        Context.Classes.Add("SecondaryText");
        StackPanel Text = new() { Spacing = 2 };
        Text.Children.Add(Title);
        Text.Children.Add(Context);
        Border Card = new() { Child = Text };
        Card.Classes.Add("ListItem");
        return Card;
    }
    /// <summary>
    /// Filters names and countries before paging without issuing another network request.
    /// </summary>
    void DisplayPage()
    {
        List<JsonElement> Matches = fItems.Where(Item => Item.GetProperty("name").GetString().Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || Item.GetProperty("country").GetString().Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        int Count = Matches.Count;
        int PageCount = Math.Max(1, (Count + fPageSize - 1) / fPageSize);
        fPage = Math.Clamp(fPage, 0, PageCount - 1);
        ItemList.ItemsSource = Matches.Skip(fPage * fPageSize).Take(fPageSize).ToList();
        SetHeader($"Universities ({Count:N0})", "Select a university to see its details.", "Search by university name or country");
        SetResultCount(Count, "university", "universities");
        fPageText.Text = $"Page {fPage + 1} / {PageCount}";
        fPrevious.IsEnabled = fPage > 0;
        fNext.IsEnabled = fPage + 1 < PageCount;
    }
    /// <summary>
    /// Displays the requested local page and positions its first row in view.
    /// </summary>
    void OnPageClick(object Sender, RoutedEventArgs Args)
    {
        fPage += Sender == fNext ? 1 : -1;
        DisplayPage();
        if (ItemList.ItemCount > 0)
            ItemList.ScrollIntoView(ItemList.Items[0]);
    }

    // ● overridables
    /// <summary>
    /// Loads the validated catalogue and retains detached JSON values in alphabetical order.
    /// </summary>
    protected override async Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        fPrevious.IsEnabled = false;
        fNext.IsEnabled = false;
        IProgress<string> DisplayProgress = CreateLoadProgress();
        string Stage = "Loading university catalogue...";
        IProgress<string> Progress = new Progress<string>(Message =>
        {
            if (!CancellationToken.IsCancellationRequested)
            {
                Stage = Message;
                DisplayProgress.Report(Message);
            }
        });
        DispatcherTimer Timer = new() { Interval = TimeSpan.FromSeconds(1) };
        Timer.Tick += (Sender, Args) => DisplayProgress.Report(Stage);
        Timer.Start();
        try
        {
            ApiClientResult Result = await fService.LoadUniversitiesAsync(CancellationToken, Progress);
            CancellationToken.ThrowIfCancellationRequested();
            if (Result.ResultStatus == ApiClientResultStatus.Success)
            {
                Progress.Report("Preparing university list...");
                IReadOnlyList<JsonElement> Items = await Task.Run(() =>
                {
                    using JsonDocument Document = JsonDocument.Parse(Result.ResponseJsonText);
                    List<JsonElement> Universities = new();
                    JsonElement Catalogue = Document.RootElement.Clone();
                    int Total = Catalogue.GetArrayLength();
                    int Completed = 0;
                    long Started = Environment.TickCount64;
                    long LastReport = Started;
                    foreach (JsonElement Item in Catalogue.EnumerateArray())
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        Universities.Add(Item);
                        Completed++;
                        if (Environment.TickCount64 - LastReport >= 500)
                        {
                            Progress.Report($"Preparing university list... {Completed:N0} / {Total:N0} records");
                            LastReport = Environment.TickCount64;
                        }
                    }
                    Progress.Report("Sorting university list...");
                    IReadOnlyList<JsonElement> Sorted = Universities
                        .OrderBy(Item => Item.GetProperty("name").GetString(), StringComparer.OrdinalIgnoreCase)
                        .ThenBy(Item => Item.GetProperty("country").GetString(), StringComparer.OrdinalIgnoreCase).ToList();
                    CancellationToken.ThrowIfCancellationRequested();
                    return Sorted;
                }, CancellationToken);
                CancellationToken.ThrowIfCancellationRequested();
                fItems = Items;
                Progress.Report($"Done. {Items.Count:N0} university records ready.");
            }
            return Result;
        }
        finally
        {
            Timer.Stop();
        }
    }
    /// <summary>
    /// Restarts local paging when the filter changes.
    /// </summary>
    protected override void ApplySearch()
    {
        fPage = 0;
        DisplayPage();
        if (ItemList.ItemCount > 0)
            ItemList.ScrollIntoView(ItemList.Items[0]);
    }
    /// <summary>
    /// Opens a separate detail card while preserving the existing list for Back.
    /// </summary>
    protected override void OnItemSelected(object Item)
    {
        if (Item is JsonElement University)
            MainView.Current?.PushForm(new UniversityDetailForm(University));
    }

    // ● constructor
    /// <summary>
    /// Creates a worldwide university list using the common search, theme and page-size preferences.
    /// </summary>
    /// <param name="Service">The registered Universities service.</param>
    public UniversityListForm(UniversityService Service)
    {
        ArgumentNullException.ThrowIfNull(Service);
        fService = Service;
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("CompactTitle");
        SetHeader("Universities", "Select a university to see its details.", "Search by university name or country");
        EmptyMessage = "No universities match your search.";
        ItemList.ItemTemplate = new FuncDataTemplate<JsonElement>((Item, Scope) => CreateItem(Item));
        Grid Paging = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        fPrevious.Classes.Add("PrimaryButton");
        fPrevious.Classes.Add("PageNavigation");
        fNext.Classes.Add("PrimaryButton");
        fNext.Classes.Add("PageNavigation");
        Avalonia.Automation.AutomationProperties.SetName(fPrevious, "Previous page");
        Avalonia.Automation.AutomationProperties.SetName(fNext, "Next page");
        Grid.SetColumn(fPageText, 1);
        Grid.SetColumn(fNext, 2);
        Paging.Children.Add(fPrevious);
        Paging.Children.Add(fPageText);
        Paging.Children.Add(fNext);
        if (ItemList.Parent is Grid ListPanel)
        {
            ListPanel.RowDefinitions = new RowDefinitions("*,Auto");
            Grid.SetRow(Paging, 1);
            Paging.Margin = new Thickness(0, 6, 0, 0);
            ListPanel.Children.Add(Paging);
        }
        fPrevious.Click += OnPageClick;
        fNext.Click += OnPageClick;
    }

    // ● properties
    /// <summary>
    /// The visible resource loading message.
    /// </summary>
    protected override string LoadingText => "Loading university catalogue...";
    /// <summary>
    /// The visible resource failure message.
    /// </summary>
    protected override string LoadErrorText => "Universities could not be loaded.";
}
