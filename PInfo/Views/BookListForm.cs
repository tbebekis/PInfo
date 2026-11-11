/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Searches Open Library books explicitly and preserves the result list when returning from details.
/// </summary>
public class BookListForm : AppForm
{
    // ● private
    readonly BookService fService;
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
    int fNextPage = -1;
    bool fSearched;

    /// <summary>
    /// Creates a compact result with title, authors and first publication year.
    /// </summary>
    /// <param name="Item">The detached JSON result.</param>
    /// <returns>The selectable result's content.</returns>
    Control CreateItem(JsonElement Item)
    {
        TextBlock Title = new() { Text = Item.GetProperty("title").GetString(), TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("ListItemTitle");
        string Authors = Item.TryGetProperty("author_name", out JsonElement Names) && Names.ValueKind == JsonValueKind.Array
            ? string.Join(", ", Names.EnumerateArray().Where(Name => Name.ValueKind == JsonValueKind.String).Select(Name => Name.GetString()))
            : "Author unavailable";
        string Year = Item.TryGetProperty("first_publish_year", out JsonElement Value) ? Value.ToString() : string.Empty;
        TextBlock Description = new()
        {
            Text = Authors + (string.IsNullOrWhiteSpace(Year) ? string.Empty : " · " + Year),
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
    /// Requests one result page and ignores cancelled navigation.
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
            fStatus.Text = "Enter a title, author or ISBN, then press 🔍 or Enter.";
            return;
        }
        using CancellationTokenSource Cancellation = new();
        fCancellation = Cancellation;
        fSearched = false;
        fNextPage = -1;
        fList.ItemsSource = null;
        fPageText.Text = string.Empty;
        fFind.IsEnabled = false;
        fPrevious.IsEnabled = false;
        fNext.IsEnabled = false;
        fList.IsEnabled = false;
        fProgress.IsVisible = true;
        fStatus.Text = "Searching books. Please wait...";

        try
        {
            ApiClientResult Result = await fService.SearchAsync(fQuery, checked(fPage * fPageSize), fPageSize, Cancellation.Token);
            if (Cancellation.IsCancellationRequested || !this.IsAttachedToVisualTree())
                return;
            if (Result.ResultStatus != ApiClientResultStatus.Success)
            {
                fStatus.Text = Result.ReasonPhrase;
                return;
            }
            using JsonDocument Document = JsonDocument.Parse(Result.ResponseJsonText);
            JsonElement Root = Document.RootElement;
            List<JsonElement> Items = Root.GetProperty("docs").EnumerateArray().Select(Item => Item.Clone()).ToList();
            long Total = Root.GetProperty("numFound").GetInt64();
            bool Exact = !Root.TryGetProperty("numFoundExact", out JsonElement ExactValue) || ExactValue.ValueKind != JsonValueKind.False;
            fNextPage = Items.Count > 0 && (long)fPage * fPageSize + Items.Count < Total ? fPage + 1 : -1;
            fList.ItemsSource = Items;
            fSearched = true;
            fStatus.Text = (Total == 0 ? "No books found. Try changing the search."
                : $"{(Exact ? string.Empty : "About ")}{Total:N0} books found. Select a book.")
                + (string.IsNullOrWhiteSpace(Result.ReasonPhrase) ? string.Empty : "\n" + Result.ReasonPhrase);
            fPageText.Text = $"Page {fPage + 1} / {Math.Max(1, (Total + fPageSize - 1) / fPageSize):N0}";
            if (Items.Count > 0)
                fList.ScrollIntoView(Items[0]);
        }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
        {
            // Leaving the form cancels its outstanding request.
        }
        catch (Exception Ex)
        {
            if (!Cancellation.IsCancellationRequested && this.IsAttachedToVisualTree())
            {
                ExceptionLog.WriteException("BookListForm.SearchAsync", Ex, false);
                fStatus.Text = "Books could not be read. Press 🔍 to try again.";
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
                fNext.IsEnabled = fNextPage >= 0;
            }
        }
    }
    /// <summary>
    /// Starts an explicit search or moves between result pages.
    /// </summary>
    async void OnAction(object Sender, RoutedEventArgs Args)
    {
        if (fCancellation != null)
            return;
        if (Sender == fFind)
        {
            fQuery = (fSearch.Text ?? string.Empty).Trim();
            fPage = 0;
            fNextPage = -1;
        }
        else if (Sender == fPrevious && fPage > 0)
            fPage--;
        else if (Sender == fNext && fNextPage >= 0)
        {
            fPage = fNextPage;
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
    /// Opens the selected work while retaining results and scroll position for Back.
    /// </summary>
    void OnSelectionChanged(object Sender, SelectionChangedEventArgs Args)
    {
        if (fList.SelectedItem is not JsonElement Item)
            return;
        fList.SelectedItem = null;
        if (fCancellation == null && this.IsAttachedToVisualTree())
            MainView.Current?.PushForm(new BookDetailForm(fService, Item));
    }
    /// <summary>
    /// Loads the initial search once, or retries a request interrupted by navigation.
    /// </summary>
    async void OnBooksLoaded(object Sender, RoutedEventArgs Args)
    {
        if (!fSearched)
            await SearchAsync();
    }
    /// <summary>
    /// Cancels requests when this search form is left.
    /// </summary>
    void OnBooksUnloaded(object Sender, RoutedEventArgs Args)
    {
        CancellationTokenSource Cancellation = fCancellation;
        fCancellation = null;
        Cancellation?.Cancel();
    }

    // ● constructor
    /// <summary>
    /// Creates an initially empty Books search using the shared list page size.
    /// </summary>
    /// <param name="Service">The registered Open Library service.</param>
    public BookListForm(BookService Service)
    {
        ArgumentNullException.ThrowIfNull(Service);
        fService = Service;
        fQuery = string.Empty;
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("FormBackground");
        Classes.Add("CompactTitle");
        Grid Layout = new() { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto"), Margin = new Thickness(12) };
        StackPanel Header = new() { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        TextBlock Title = new() { Text = "Books", TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("FormTitle");
        Header.Children.Add(Title);
        TextBlock Hint = new()
        {
            Text = "Search books by title, author or ISBN · Open Library",
            TextWrapping = TextWrapping.Wrap
        };
        Hint.Classes.Add("SecondaryText");
        Header.Children.Add(Hint);
        Layout.Children.Add(Header);
        fSearch.Text = fQuery;
        fSearch.PlaceholderText = "Title, author or ISBN";
        Avalonia.Automation.AutomationProperties.SetName(fFind, "Search books");
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
        Loaded += OnBooksLoaded;
        Unloaded += OnBooksUnloaded;
    }
}
