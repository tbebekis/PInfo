/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */
namespace PInfo.Views;

/// <summary>
/// Supplies country loading, filtering, item presentation, and card navigation to the shared list form.
/// </summary>
public class CountryListForm : AppListForm
{
    // ● private
    readonly CountryService fService;

    /// <summary>
    /// Creates a country row using the common list, flag, and badge styles.
    /// Bindings follow the current DataContext when a row is recycled.
    /// </summary>
    /// <param name="Country">The initial country, or null while an item container is being cleared.</param>
    /// <returns>A bound row, or null when no country is available.</returns>
    Control CreateItem(CountryModel Country)
    {
        if (Country == null)
            return null;

        Grid Row = new() { ColumnDefinitions = new ColumnDefinitions("56,*,Auto") };
        TextBlock Flag = new();
        Flag.Bind(TextBlock.TextProperty, new ReflectionBinding(nameof(CountryModel.Flag)));
        Flag.Classes.Add("FlagEmoji");
        Row.Children.Add(Flag);

        TextBlock Name = new() { Margin = new Thickness(0, 0, 12, 0) };
        Name.Bind(TextBlock.TextProperty, new ReflectionBinding(nameof(CountryModel.Name)));
        Name.Classes.Add("ListItemTitle");
        Grid.SetColumn(Name, 1);
        Row.Children.Add(Name);

        TextBlock Code = new();
        Code.Bind(TextBlock.TextProperty, new ReflectionBinding(nameof(CountryModel.Alpha3Code)));
        Code.Classes.Add("BadgeText");
        Border Badge = new() { Child = Code };
        Badge.Classes.Add("Badge");
        Grid.SetColumn(Badge, 2);
        Row.Children.Add(Badge);

        Border Card = new() { Child = Row };
        Card.Classes.Add("ListItem");
        return Card;
    }

    // ● overridables
    /// <summary>
    /// Loads complete country data through the shared Countries service.
    /// </summary>
    /// <param name="CancellationToken">Cancels this load or its wait for the service.</param>
    /// <returns>The country service load outcome.</returns>
    protected override Task<ApiClientResult> LoadListAsync(CancellationToken CancellationToken)
    {
        return fService.LoadCountriesAsync(CancellationToken);
    }
    /// <summary>
    /// Filters countries by name or ISO code without another network request.
    /// </summary>
    protected override void ApplySearch()
    {
        string Search = SearchText;
        List<CountryModel> Items = fService.Countries.Where(Item =>
            Item.Name.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || Item.Alpha2Code.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || Item.Alpha3Code.Contains(Search, StringComparison.OrdinalIgnoreCase)).ToList();
        ItemList.ItemsSource = Items;
        SetResultCount(Items.Count, "country", "countries");
    }
    /// <summary>
    /// Opens the selected country's detail card while retaining this list instance for Back.
    /// </summary>
    /// <param name="Item">The selected country.</param>
    protected override void OnItemSelected(object Item)
    {
        if (Item is CountryModel Country && MainView.Current != null)
            MainView.Current.PushForm(new CountryCardForm(Country));
    }

    // ● constructor
    /// <summary>
    /// Configures the shared list layout and country-specific item template.
    /// </summary>
    public CountryListForm()
    {
        fService = AppHost.PublicServices.Select(Item => Item.Service).OfType<CountryService>().Single();
        SetHeader("Countries", "Explore countries around the world", "Search by country name or ISO code");
        EmptyMessage = "No countries match your search.";
        ItemList.ItemTemplate = new FuncDataTemplate<CountryModel>(
            (Country, Scope) => CreateItem(Country), supportsRecycling: true);
    }

    // ● properties
    /// <summary>
    /// The country loading status text.
    /// </summary>
    protected override string LoadingText => "Loading countries...";
    /// <summary>
    /// The country load failure status text.
    /// </summary>
    protected override string LoadErrorText => "Countries could not be loaded.";
}
