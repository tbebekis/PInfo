/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Browses Met collections and preserves the search, page and scroll position when returning from details.
/// </summary>
public class MetListForm : AppForm
{
    // ● private
    readonly MetService fService;
    readonly TextBox fSearch = new();
    readonly Button fFind = new() { Content = "🔍" };
    readonly Button fPrevious = new() { Content = "←", IsEnabled = false };
    readonly Button fNext = new() { Content = "→", IsEnabled = false };
    readonly TextBlock fStatus = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock fPageText = new() { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
    readonly ProgressBar fProgress = new() { IsIndeterminate = true, IsVisible = false };
    readonly ListBox fList = new();
    readonly ComboBox fCollection = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch, MinHeight = 32, Height = 32, Padding = new Thickness(8, 2) };
    readonly CheckBox fWithImages = new() { Content = "With image only", IsChecked = true };
    readonly List<(int Id, string Name)> fDepartments = new();
    readonly Dictionary<int, Avalonia.Media.Imaging.Bitmap> fImages = new();
    readonly int fPageSize;
    CancellationTokenSource fCancellation;
    string fQuery;
    int fPage;
    int fNextPage = -1;
    bool fSearched;

    /// <summary>
    /// Creates a compact artwork row with preview, title, artist and catalogue date.
    /// </summary>
    /// <param name="Item">The detached JSON result.</param>
    /// <returns>The selectable result's content.</returns>
    Control CreateItem(JsonElement Item)
    {
        if (Item.ValueKind != JsonValueKind.Object)
            return new Border();
        TextBlock Title = new() { Text = MetService.Text(Item, "title"), TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("ListItemTitle");
        string Authors = MetService.Text(Item, "artistDisplayName");
        string Year = MetService.Text(Item, "objectDate");
        TextBlock Description = new()
        {
            Text = string.Join(" · ", new[] { Authors, Year }.Where(Value => !string.IsNullOrWhiteSpace(Value))),
            TextWrapping = TextWrapping.Wrap, FontSize = 12
        };
        Description.Classes.Add("SecondaryText");
        StackPanel Text = new() { Spacing = 2 };
        Text.Children.Add(Title);
        Text.Children.Add(Description);
        Grid Row = new() { ColumnDefinitions = new ColumnDefinitions("64,*"), ColumnSpacing = 8 };
        if (Item.TryGetProperty("objectID", out JsonElement Id) && fImages.TryGetValue(Id.GetInt32(), out Avalonia.Media.Imaging.Bitmap Bitmap))
            Row.Children.Add(new Image { Source = Bitmap, Width = 64, Height = 56, Stretch = Stretch.Uniform });
        else
            Row.Children.Add(new TextBlock { Text = "🖼", FontSize = 24, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        Grid.SetColumn(Text, 1);
        Row.Children.Add(Text);
        Border Card = new() { Child = Row };
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
        using CancellationTokenSource Cancellation = new();
        fCancellation = Cancellation;
        fSearched = false;
        fNextPage = -1;
        fList.ItemsSource = null;
        foreach (Avalonia.Media.Imaging.Bitmap Bitmap in fImages.Values)
            Bitmap.Dispose();
        fImages.Clear();
        fPageText.Text = string.Empty;
        fFind.IsEnabled = false;
        fCollection.IsEnabled = false;
        fWithImages.IsEnabled = false;
        fPrevious.IsEnabled = false;
        fNext.IsEnabled = false;
        fList.IsEnabled = false;
        fProgress.IsVisible = true;
        fStatus.Text = "Searching artworks. Please wait...";

        try
        {
            if (fDepartments.Count == 0)
            {
                ApiClientResult Departments = await fService.GetDepartmentsAsync(Cancellation.Token);
                if (Cancellation.IsCancellationRequested || fCancellation != Cancellation || !this.IsAttachedToVisualTree())
                    return;
                if (Departments.ResultStatus != ApiClientResultStatus.Success)
                {
                    fStatus.Text = Departments.ReasonPhrase;
                    return;
                }
                using JsonDocument Catalogue = JsonDocument.Parse(Departments.ResponseJsonText);
                fDepartments.Add((0, "All departments"));
                foreach (JsonElement Department in Catalogue.RootElement.GetProperty("departments").EnumerateArray())
                    fDepartments.Add((Department.GetProperty("departmentId").GetInt32(), Department.GetProperty("displayName").GetString()));
                fCollection.ItemsSource = fDepartments.Select(Item => Item.Name).ToArray();
                fCollection.SelectedIndex = Math.Max(0, fDepartments.FindIndex(Item => Item.Id == 11));
            }
            int DepartmentId = fDepartments[Math.Max(0, fCollection.SelectedIndex)].Id;
            ApiClientResult Result = await fService.SearchAsync(fQuery, DepartmentId, fWithImages.IsChecked == true,
                checked(fPage * fPageSize), fPageSize, Cancellation.Token);
            if (Cancellation.IsCancellationRequested || fCancellation != Cancellation || !this.IsAttachedToVisualTree())
                return;
            if (Result.ResultStatus != ApiClientResultStatus.Success)
            {
                fStatus.Text = Result.ReasonPhrase;
                return;
            }
            using JsonDocument Document = JsonDocument.Parse(Result.ResponseJsonText);
            JsonElement Root = Document.RootElement;
            int Total = Root.GetProperty("total").GetInt32();
            List<JsonElement> Items = new();
            JsonElement Ids = Root.GetProperty("objectIDs");
            int[] Identifiers = Ids.ValueKind == JsonValueKind.Array ? Ids.EnumerateArray().Select(Item => Item.GetInt32()).ToArray() : Array.Empty<int>();
            int Failed = 0;
            string Warning = Result.ReasonPhrase;
            for (int Index = 0; Index < Identifiers.Length; Index++)
            {
                fStatus.Text = $"Reading artworks {Index + 1}/{Identifiers.Length}. Please wait...";
                ApiClientResult ObjectResult = await fService.GetObjectAsync(Identifiers[Index], Cancellation.Token);
                if (Cancellation.IsCancellationRequested || fCancellation != Cancellation || !this.IsAttachedToVisualTree())
                    return;
                if (ObjectResult.ResultStatus != ApiClientResultStatus.Success)
                {
                    Failed++;
                    continue;
                }
                if (!string.IsNullOrWhiteSpace(ObjectResult.ReasonPhrase))
                    Warning = ObjectResult.ReasonPhrase;
                using JsonDocument ObjectDocument = JsonDocument.Parse(ObjectResult.ResponseJsonText);
                JsonElement Artwork = ObjectDocument.RootElement.Clone();
                Items.Add(Artwork);
                byte[] Bytes = await fService.GetImageAsync(Artwork, Cancellation.Token);
                if (Cancellation.IsCancellationRequested || fCancellation != Cancellation || !this.IsAttachedToVisualTree())
                    return;
                if (Bytes != null)
                {
                    try
                    {
                        using MemoryStream Stream = new(Bytes);
                        fImages[Identifiers[Index]] = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(Stream, 128);
                    }
                    catch (Exception Ex) when (Ex is ArgumentException || Ex is InvalidOperationException || Ex is IOException)
                    {
                        // Metadata remains usable if a provider image cannot be decoded.
                    }
                }
            }
            fNextPage = Identifiers.Length > 0 && (long)fPage * fPageSize + Identifiers.Length < Total ? fPage + 1 : -1;
            fList.ItemsSource = Items;
            fSearched = true;
            fStatus.Text = (Total == 0 ? "No artworks found. Try changing the search."
                : $"{Total:N0} artworks found. Select an artwork.")
                + (string.IsNullOrWhiteSpace(Warning) ? string.Empty : "\n" + Warning);
            if (Failed > 0)
                fStatus.Text += $"\n{Failed} artworks on this page could not be loaded. Press 🔍 to retry.";
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
                ExceptionLog.WriteException("MetListForm.SearchAsync", Ex, false);
                fStatus.Text = "Art could not be read. Press 🔍 to try again.";
            }
        }
        finally
        {
            if (fCancellation == Cancellation)
            {
                fCancellation = null;
                fProgress.IsVisible = false;
                fFind.IsEnabled = true;
                fCollection.IsEnabled = true;
                fWithImages.IsEnabled = true;
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
            MainView.Current?.PushForm(new MetDetailForm(fService, Item));
    }
    /// <summary>
    /// Loads the initial search once, or retries a request interrupted by navigation.
    /// </summary>
    async void OnArtLoaded(object Sender, RoutedEventArgs Args)
    {
        if (!fSearched)
            await SearchAsync();
    }
    /// <summary>
    /// Cancels requests when this search form is left.
    /// </summary>
    void OnArtUnloaded(object Sender, RoutedEventArgs Args)
    {
        CancellationTokenSource Cancellation = fCancellation;
        fCancellation = null;
        Cancellation?.Cancel();
    }

    // ● constructor
    /// <summary>
    /// Creates an initially browsable Art collection using the shared list page size.
    /// </summary>
    /// <param name="Service">The registered museum service.</param>
    public MetListForm(MetService Service)
    {
        ArgumentNullException.ThrowIfNull(Service);
        fService = Service;
        fQuery = string.Empty;
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        Classes.Add("FormBackground");
        Classes.Add("CompactTitle");
        Grid Layout = new() { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto"), Margin = new Thickness(12) };
        StackPanel Header = new() { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };
        TextBlock Title = new() { Text = "Art - The Met", TextWrapping = TextWrapping.Wrap };
        Title.Classes.Add("FormTitle");
        Header.Children.Add(Title);
        TextBlock Hint = new()
        {
            Text = "Choose a collection to explore, or search for an artist, artwork or subject.",
            TextWrapping = TextWrapping.Wrap
        };
        Hint.Classes.Add("SecondaryText");
        Header.Children.Add(Hint);
        StackPanel Browse = new() { Spacing = 4 };
        Browse.Children.Add(new TextBlock { Text = "Collection", Classes = { "SecondaryText" } });
        Browse.Children.Add(fCollection);
        Browse.Children.Add(fWithImages);
        WrapPanel Examples = new();
        foreach (string Example in new[] { "Van Gogh", "Monet", "Cats", "Flowers" })
        {
            Button Suggestion = new() { Content = Example, Padding = new Thickness(6, 2), Margin = new Thickness(0, 0, 4, 0) };
            Suggestion.Click += (Sender, Args) =>
            {
                if (fCancellation != null)
                    return;
                fSearch.Text = Example;
                fCollection.SelectedIndex = 0;
                OnAction(fFind, new RoutedEventArgs());
            };
            Examples.Children.Add(Suggestion);
        }
        Browse.Children.Add(Examples);
        Header.Children.Add(Browse);
        Layout.Children.Add(Header);
        Avalonia.Automation.AutomationProperties.SetName(fCollection, "Museum collection");
        fSearch.Text = fQuery;
        fSearch.PlaceholderText = "Artist, artwork or subject (optional)";
        Avalonia.Automation.AutomationProperties.SetName(fFind, "Search artworks");
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
        fCollection.SelectionChanged += (Sender, Args) =>
        {
            if (fCancellation == null && fDepartments.Count > 0)
                OnAction(fFind, new RoutedEventArgs());
        };
        fWithImages.IsCheckedChanged += (Sender, Args) =>
        {
            if (fCancellation == null && fDepartments.Count > 0)
                OnAction(fFind, new RoutedEventArgs());
        };
        fFind.Click += OnAction;
        fSearch.KeyDown += OnSearchKeyDown;
        fPrevious.Click += OnAction;
        fNext.Click += OnAction;
        fList.SelectionChanged += OnSelectionChanged;
        Loaded += OnArtLoaded;
        Unloaded += OnArtUnloaded;
    }
}
