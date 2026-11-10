/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays a country's selectable geographic places with filters, paging, and detail-card navigation.
/// </summary>
public class PlaceListForm : AppListForm
{
    // ● private
    readonly ComboBox fCategoryChoice = new() { MinHeight = 32, Height = 32, Padding = new Thickness(8, 2) };
    readonly string fCountryCode;
    readonly string fTitle;
    readonly TextBlock fLoadStatus;
    readonly int fPageSize;
    readonly ComboBox fSubcategory = new() { MinHeight = 32, Height = 32, Padding = new Thickness(8, 2) };
    readonly Button fPrevious = new() { Content = "←", IsEnabled = false, MinWidth = 44, MinHeight = 44 };
    readonly Button fNext = new() { Content = "→", IsEnabled = false, MinWidth = 44, MinHeight = 44 };
    readonly TextBlock fPageText = new()
    {
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
    };
    CancellationTokenSource fQueryCancellation;
    int fPage;
    int fQueryVersion;
    bool fReady;
    bool fNeedsQuery = true;

    /// <summary>
    /// Creates a compact selectable row whose name follows its data context during recycling.
    /// </summary>
    /// <param name="Place">The initial item, or null during container cleanup.</param>
    /// <returns>The bound list row, or null during cleanup.</returns>
    Control CreateItem(PlaceModel Place)
    {
        if (Place == null)
            return null;
        TextBlock Name = new();
        Name.Classes.Add("ListItemTitle");
        Name.Bind(TextBlock.TextProperty, new ReflectionBinding(".")
        {
            Converter = new FuncValueConverter<PlaceModel, string>(Item => Item == null ? string.Empty
                : string.IsNullOrWhiteSpace(Item.AsciiName) ? Item.Name : Item.AsciiName.Trim())
        });
        TextBlock Area = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        Area.Classes.Add("SecondaryText");
        Area.Bind(TextBlock.TextProperty, new ReflectionBinding("AdministrativeArea"));
        Area.Bind(TextBlock.IsVisibleProperty, new ReflectionBinding("AdministrativeArea")
        {
            Converter = new FuncValueConverter<string, bool>(Value => !string.IsNullOrWhiteSpace(Value))
        });
        StackPanel Content = new() { Spacing = 2 };
        Content.Children.Add(Name);
        Content.Children.Add(Area);
        Border Card = new() { Child = Content };
        Card.Classes.Add("ListItem");
        return Card;
    }
    /// <summary>
    /// Reads the filtered page off the UI thread and discards cancelled or superseded results.
    /// </summary>
    /// <param name="Debounce">Waits briefly for further search input before querying.</param>
    async Task DisplayPageAsync(bool Debounce)
    {
        int Version = ++fQueryVersion;
        PlaceCategory Category = SelectedCategory;
        string Search = SearchText;
        string Subcategory = fSubcategory.SelectedItem as string ?? "All";
        int Page = fPage;
        fQueryCancellation?.Cancel();
        using CancellationTokenSource Cancellation = new();
        fQueryCancellation = Cancellation;
        fNeedsQuery = true;
        fPrevious.IsEnabled = false;
        fNext.IsEnabled = false;
        try
        {
            if (Debounce)
                await Task.Delay(250, Cancellation.Token);
            if (Version != fQueryVersion || Cancellation.IsCancellationRequested || !this.IsAttachedToVisualTree())
                return;
            var Result = await Task.Run(() => Db.GetPlaces(fCountryCode, Category, Subcategory, Search,
                Page, fPageSize, Cancellation.Token), Cancellation.Token);
            if (Version != fQueryVersion || Cancellation.IsCancellationRequested || !this.IsAttachedToVisualTree())
                return;
            fNeedsQuery = false;
            fPage = Result.Page;
            ItemList.ItemsSource = Result.Items;
            SetResultCount(Result.Total, "place", "places");
            SetHeader($"{fTitle} ({Result.Total:N0})", string.Empty, "Search by name or alternative name");
            fLoadStatus.IsVisible = false;
            int PageCount = Math.Max(1, (Result.Total + fPageSize - 1) / fPageSize);
            fPageText.Text = $"Page {fPage + 1} / {PageCount}";
            fPrevious.IsEnabled = fPage > 0;
            fNext.IsEnabled = fPage + 1 < PageCount;
            if (Result.Items.Count > 0)
                ItemList.ScrollIntoView(Result.Items[0]);
        }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
        {
            // A new filter, page, or navigation superseded this query.
        }
        catch (Exception Ex)
        {
            if (Version != fQueryVersion || Cancellation.IsCancellationRequested || !this.IsAttachedToVisualTree())
                return;
            ExceptionLog.WriteException("PlaceListForm.DisplayPageAsync", Ex, false);
            ShowNotice("Places could not be read. Please try again.", true);
        }
        finally
        {
            if (fQueryCancellation == Cancellation)
                fQueryCancellation = null;
        }
    }
    /// <summary>
    /// Opens the previous or next page while preserving the current filters.
    /// </summary>
    async void OnPageClick(object Sender, RoutedEventArgs Args)
    {
        fPage += Sender == fNext ? 1 : -1;
        await DisplayPageAsync(false);
    }
    /// <summary>
    /// Replaces the subcategory choices and restarts paging when the selected category changes.
    /// </summary>
    void OnCategoryChanged(object Sender, SelectionChangedEventArgs Args)
    {
        fSubcategory.SelectionChanged -= OnSubcategoryChanged;
        try
        {
            fSubcategory.ItemsSource = SelectedCategory.Filters.Keys.ToArray();
            fSubcategory.SelectedItem = "All";
        }
        finally
        {
            fSubcategory.SelectionChanged += OnSubcategoryChanged;
        }
        ApplySearch();
    }
    /// <summary>
    /// Restarts paging when the selected subcategory changes.
    /// </summary>
    void OnSubcategoryChanged(object Sender, SelectionChangedEventArgs Args)
    {
        ApplySearch();
    }
    /// <summary>
    /// Resumes an interrupted page query while retaining a completed page on return.
    /// </summary>
    async void OnPlacesLoaded(object Sender, RoutedEventArgs Args)
    {
        if (fReady && fNeedsQuery)
            await DisplayPageAsync(false);
    }
    /// <summary>
    /// Cancels the current page query and suppresses late results after navigation.
    /// </summary>
    void OnPlacesUnloaded(object Sender, RoutedEventArgs Args)
    {
        ++fQueryVersion;
        fQueryCancellation?.Cancel();
    }

    // ● overridables
    /// <summary>
    /// Opens the selected item's existing data in a detail card while retaining this list for Back.
    /// </summary>
    /// <param name="Item">The selected list item.</param>
    protected override void OnItemSelected(object Item)
    {
        if (Item is PlaceModel Place && MainView.Current != null)
            MainView.Current.PushForm(new PlaceCardForm(Place, SelectedCategory));
    }
    /// <summary>
    /// Ensures the country's common place import is available, with shared loading progress.
    /// </summary>
    /// <param name="CancellationToken">Cancels the download or import.</param>
    /// <returns>The load result, including any offline fallback warning.</returns>
    protected override async Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        fReady = false;
        fLoadStatus.IsVisible = true;
        ++fQueryVersion;
        ApiClientResult Result = await GeoNamesService.LoadPlacesAsync(fCountryCode, CancellationToken, CreateLoadProgress());
        fReady = Result.ResultStatus == ApiClientResultStatus.Success && !CancellationToken.IsCancellationRequested;
        return Result;
    }
    /// <summary>
    /// Applies both filters to the full category and restarts at the first matching page.
    /// </summary>
    protected override async void ApplySearch()
    {
        if (!fReady)
            return;
        fPage = 0;
        await DisplayPageAsync(true);
    }

    // ● constructor
    /// <summary>
    /// Creates a country category list using the shared page size and existing place import.
    /// </summary>
    /// <param name="Country">The selected country.</param>
    public PlaceListForm(CountryModel Country)
    {
        ArgumentNullException.ThrowIfNull(Country);
        ArgumentException.ThrowIfNullOrWhiteSpace(Country.Alpha2Code);
        fCountryCode = Country.Alpha2Code.Trim().ToUpperInvariant();
        if (fCountryCode.Length != 2 || fCountryCode.Any(Character => Character < 'A' || Character > 'Z'))
            throw new ArgumentException("The country must have a two-letter ISO code.", nameof(Country));
        fPageSize = Math.Clamp(Lib.Settings.ItemsPerPage, 10, 100);
        fTitle = Country.Name + " - Places";
        fLoadStatus = this.FindControl<TextBlock>("ResultCountText");
        Classes.Add("CompactTitle");
        SetHeader(fTitle, string.Empty, "Search by name or alternative name");
        fCategoryChoice.Classes.Add("ChoiceBox");
        fCategoryChoice.ItemsSource = PlaceCategory.All.Select(Category => Category.Name).ToArray();
        fCategoryChoice.SelectedIndex = 0;
        Avalonia.Automation.AutomationProperties.SetName(fCategoryChoice, "Place category");
        fSubcategory.Classes.Add("ChoiceBox");
        fSubcategory.ItemsSource = SelectedCategory.Filters.Keys.ToArray();
        fSubcategory.SelectedItem = "All";
        Avalonia.Automation.AutomationProperties.SetName(fSubcategory, "Place subcategory");
        Grid Filters = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 8,
            RowSpacing = 6,
            Margin = new Thickness(0, 0, 0, 8)
        };
        TextBlock CategoryLabel = new() { Text = "Cat.", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        CategoryLabel.Classes.Add("BodyText");
        TextBlock FilterLabel = new() { Text = "SubCat.", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        FilterLabel.Classes.Add("BodyText");
        Grid.SetColumn(fCategoryChoice, 1);
        Grid.SetRow(FilterLabel, 1);
        Grid.SetColumn(fSubcategory, 1);
        Grid.SetRow(fSubcategory, 1);
        Filters.Children.Add(CategoryLabel);
        Filters.Children.Add(fCategoryChoice);
        Filters.Children.Add(FilterLabel);
        Filters.Children.Add(fSubcategory);
        Grid Paging = new()
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(0, 6, 0, 0)
        };
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
            // Keep the empty-state text in the same row as the list.
            Control[] ExistingChildren = ListPanel.Children.ToArray();
            ListPanel.RowDefinitions = new RowDefinitions("Auto,*,Auto");
            foreach (Control Child in ExistingChildren)
                Grid.SetRow(Child, 1);
            Grid.SetRow(Paging, 2);
            ListPanel.Children.Add(Filters);
            ListPanel.Children.Add(Paging);
        }
        fPrevious.Click += OnPageClick;
        fNext.Click += OnPageClick;
        fSubcategory.SelectionChanged += OnSubcategoryChanged;
        fCategoryChoice.SelectionChanged += OnCategoryChanged;
        Loaded += OnPlacesLoaded;
        Unloaded += OnPlacesUnloaded;
        EmptyMessage = "No places are available or match your filters.";
        ItemList.ItemTemplate = new FuncDataTemplate<PlaceModel>(
            (Place, Scope) => CreateItem(Place), supportsRecycling: true);
    }

    // ● properties
    /// <summary>
    /// The category selected in the list's category dropdown.
    /// </summary>
    PlaceCategory SelectedCategory => PlaceCategory.All.FirstOrDefault(Category => Category.Name == (fCategoryChoice.SelectedItem as string))
        ?? PlaceCategory.All[0];
    /// <summary>
    /// The status text displayed while loading places.
    /// </summary>
    protected override string LoadingText => "Loading places...";
    /// <summary>
    /// The status text displayed when loading places fails.
    /// </summary>
    protected override string LoadErrorText => "Places could not be loaded.";
}
