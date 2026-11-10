/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Searches English Wikipedia and lets the user choose a matching article before reading its introduction.
/// </summary>
public class WikipediaForm : AppForm
{
    // ● private
    readonly TextBox fSearch = new();
    readonly Button fFind = new() { Content = "Search" };
    readonly Button fPrevious = new() { Content = "←", IsEnabled = false };
    readonly Button fNext = new() { Content = "→", IsEnabled = false };
    readonly TextBlock fStatus = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock fPageText = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    readonly ProgressBar fProgress = new() { IsIndeterminate = true, IsVisible = false };
    readonly ListBox fList = new();
    readonly List<int> fOffsets = new() { 0 };
    readonly int fPageSize;
    CancellationTokenSource fCancellation;
    string fQuery;
    int fPage;
    int fNextOffset = -1;
    bool fSearched;

    /// <summary>
    /// Creates a compact search result with plain-text title and snippet.
    /// </summary>
    /// <param name="Item">The detached JSON result.</param>
    /// <returns>The selectable result's content.</returns>
    Control CreateItem(JsonElement Item)
    {
        TextBlock Title = new() { Text = Item.GetProperty("title").GetString(), TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("ListItemTitle");
        string Snippet = Item.TryGetProperty("snippet", out JsonElement Value) ? Value.GetString() : string.Empty;
        TextBlock Description = new()
        {
            Text = System.Net.WebUtility.HtmlDecode(Regex.Replace(Snippet ?? string.Empty, "<[^>]*>", string.Empty)),
            TextWrapping = TextWrapping.Wrap, FontSize = 12
        };
        Description.Classes.Add("SecondaryText");
        StackPanel Text = new() { Spacing = 2 };
        Text.Children.Add(Title);
        Text.Children.Add(Description);
        Border Card = new() { Child = Text };
        Card.Classes.Add("ListItem");
        return Card;
    }
    /// <summary>
    /// Requests one result page, retaining server continuation and ignoring cancelled navigation.
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
            fStatus.Text = "Enter a name to search English Wikipedia.";
            return;
        }
        using CancellationTokenSource Cancellation = new();
        fCancellation = Cancellation;
        fSearched = false;
        fNextOffset = -1;
        fList.ItemsSource = null;
        fPageText.Text = string.Empty;
        fFind.IsEnabled = false;
        fPrevious.IsEnabled = false;
        fNext.IsEnabled = false;
        fList.IsEnabled = false;
        fProgress.IsVisible = true;
        fStatus.Text = "Searching English Wikipedia. Please wait...";
        long SinglePageId = 0;
        try
        {
            ApiClientResult Result = await WikipediaService.SearchAsync(fQuery, fOffsets[fPage], fPageSize, Cancellation.Token);
            if (Cancellation.IsCancellationRequested || !this.IsAttachedToVisualTree())
                return;
            if (Result.ResultStatus != ApiClientResultStatus.Success)
            {
                fStatus.Text = Result.ReasonPhrase;
                return;
            }
            using JsonDocument Document = JsonDocument.Parse(Result.ResponseJsonText);
            JsonElement Query = Document.RootElement.GetProperty("query");
            List<JsonElement> Items = Query.GetProperty("search").EnumerateArray().Select(Item => Item.Clone()).ToList();
            int Total = Query.GetProperty("searchinfo").GetProperty("totalhits").GetInt32();
            fNextOffset = Document.RootElement.TryGetProperty("continue", out JsonElement Continuation)
                && Continuation.TryGetProperty("sroffset", out JsonElement Offset) ? Offset.GetInt32() : -1;
            fList.ItemsSource = Items;
            fSearched = true;
            fStatus.Text = (Total == 0 ? "No articles found. Try changing the search."
                : $"{Total:N0} articles found. Select an article.")
                + (string.IsNullOrWhiteSpace(Result.ReasonPhrase) ? string.Empty : "\n" + Result.ReasonPhrase);
            fPageText.Text = $"Page {fPage + 1}";
            if (Items.Count > 0)
                fList.ScrollIntoView(Items[0]);
            if (fPage == 0 && Total == 1 && Items.Count == 1 && fNextOffset < 0)
                SinglePageId = Items[0].GetProperty("pageid").GetInt64();
        }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
        {
            // Leaving the form cancels its outstanding request.
        }
        catch (Exception Ex)
        {
            if (!Cancellation.IsCancellationRequested && this.IsAttachedToVisualTree())
            {
                ExceptionLog.WriteException("WikipediaForm.SearchAsync", Ex, false);
                fStatus.Text = "Wikipedia could not be read. Press Search to try again.";
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
                fNext.IsEnabled = fNextOffset >= 0;
            }
        }
        if (!Cancellation.IsCancellationRequested && SinglePageId > 0 && this.IsAttachedToVisualTree())
            MainView.Current?.PushForm(new WikipediaArticleForm(SinglePageId));
    }
    /// <summary>
    /// Starts an explicit search or follows a server-provided page offset.
    /// </summary>
    async void OnAction(object Sender, RoutedEventArgs Args)
    {
        if (fCancellation != null)
            return;
        if (Sender == fFind)
        {
            fQuery = (fSearch.Text ?? string.Empty).Trim();
            fPage = 0;
            fOffsets.Clear();
            fOffsets.Add(0);
            fNextOffset = -1;
        }
        else if (Sender == fPrevious && fPage > 0)
            fPage--;
        else if (Sender == fNext && fNextOffset >= 0)
        {
            if (fOffsets.Count > fPage + 1)
                fOffsets.RemoveRange(fPage + 1, fOffsets.Count - fPage - 1);
            fOffsets.Add(fNextOffset);
            fPage++;
        }
        else
            return;
        await SearchAsync();
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
    /// Opens the selected article while keeping the existing results and scroll position for Back.
    /// </summary>
    void OnSelectionChanged(object Sender, SelectionChangedEventArgs Args)
    {
        if (fList.SelectedItem is not JsonElement Item)
            return;
        fList.SelectedItem = null;
        if (fCancellation == null && this.IsAttachedToVisualTree())
            MainView.Current?.PushForm(new WikipediaArticleForm(Item.GetProperty("pageid").GetInt64()));
    }
    /// <summary>
    /// Loads the initial search once, or retries a request interrupted by navigation.
    /// </summary>
    async void OnWikipediaLoaded(object Sender, RoutedEventArgs Args)
    {
        if (!fSearched)
            await SearchAsync();
    }
    /// <summary>
    /// Cancels requests when this search form is left.
    /// </summary>
    void OnWikipediaUnloaded(object Sender, RoutedEventArgs Args)
    {
        CancellationTokenSource Cancellation = fCancellation;
        fCancellation = null;
        Cancellation?.Cancel();
    }

    // ● constructor
    /// <summary>
    /// Creates a Wikipedia search seeded with the selected country or location's name.
    /// </summary>
    /// <param name="Search">The editable initial search text.</param>
    /// <param name="Context">Administrative or country context displayed as a search aid.</param>
    public WikipediaForm(string Search, string Context = "")
    {
        fQuery = Search ?? string.Empty;
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("FormBackground");
        Classes.Add("CompactTitle");
        Grid Layout = new() { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto"), Margin = new Thickness(12) };
        StackPanel Header = new() { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        TextBlock Title = new() { Text = "Wikipedia", TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("FormTitle");
        Header.Children.Add(Title);
        TextBlock Hint = new()
        {
            Text = "English Wikipedia · " + Search + (string.IsNullOrWhiteSpace(Context) ? string.Empty : " · " + Context),
            TextWrapping = TextWrapping.Wrap
        };
        Hint.Classes.Add("SecondaryText");
        Header.Children.Add(Hint);
        Layout.Children.Add(Header);
        fSearch.Text = fQuery;
        fSearch.PlaceholderText = "Search English Wikipedia";
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
        Content = Layout;
        fFind.Click += OnAction;
        fSearch.KeyDown += OnSearchKeyDown;
        fPrevious.Click += OnAction;
        fNext.Click += OnAction;
        fList.SelectionChanged += OnSelectionChanged;
        Loaded += OnWikipediaLoaded;
        Unloaded += OnWikipediaUnloaded;
    }
}
