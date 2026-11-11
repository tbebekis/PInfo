/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays the selected Open Library work with its description, bibliographic context and source link.
/// </summary>
public class BookDetailForm : AppCardForm
{
    // ● private
    readonly BookService fService;
    readonly JsonElement fItem;
    readonly TextBlock fStatus = new() { Text = "Loading Open Library. Please wait...", TextWrapping = TextWrapping.Wrap };
    readonly ProgressBar fProgress = new() { IsIndeterminate = true };
    readonly Button fRetry = new() { Content = "Try again", IsVisible = false };
    CancellationTokenSource fCancellation;
    bool fLoaded;

    /// <summary>
    /// Joins a bounded set of optional catalogue labels and indicates additional labels.
    /// </summary>
    static string StringArray(JsonElement Item, string Name)
    {
        if (!Item.TryGetProperty(Name, out JsonElement Values) || Values.ValueKind != JsonValueKind.Array)
            return string.Empty;
        List<string> Labels = Values.EnumerateArray().Where(Value => Value.ValueKind == JsonValueKind.String)
            .Select(Value => Value.GetString()).Where(Value => !string.IsNullOrWhiteSpace(Value)).Distinct().ToList();
        return string.Join(", ", Labels.Take(20)) + (Labels.Count > 20 ? $" (+{Labels.Count - 20} more)" : string.Empty);
    }
    /// <summary>
    /// Loads the chosen work once and retains its detail card when returning with Back.
    /// </summary>
    async Task LoadAsync()
    {
        if (fLoaded || fCancellation != null)
            return;
        using CancellationTokenSource Cancellation = new();
        fCancellation = Cancellation;
        fProgress.IsVisible = true;
        fRetry.IsVisible = false;
        fStatus.Text = "Loading Open Library. Please wait...";
        try
        {
            ApiClientResult Result = await fService.GetWorkAsync(fItem.GetProperty("key").GetString(), Cancellation.Token);
            if (Cancellation.IsCancellationRequested || !this.IsAttachedToVisualTree())
                return;
            if (Result.ResultStatus != ApiClientResultStatus.Success)
            {
                fStatus.Text = Result.ReasonPhrase;
                fRetry.IsVisible = true;
                return;
            }
            using JsonDocument Document = JsonDocument.Parse(Result.ResponseJsonText);
            JsonElement Work = Document.RootElement;
            string Title = Work.GetProperty("title").GetString();
            string Authors = StringArray(fItem, "author_name");
            SetHeader(Title, Authors, "📚");
            StackPanel Information = CreateSection("Book information");
            AddField(Information, "Record", "Work · editions may differ in language, publisher and ISBN");
            AddField(Information, "Authors", Authors);
            string Year = fItem.TryGetProperty("first_publish_year", out JsonElement Published) ? Published.ToString() : string.Empty;
            AddField(Information, "First publication year (catalogue)", Year);
            if (fItem.TryGetProperty("edition_count", out JsonElement Editions))
                AddField(Information, "Known editions", Editions.ToString());
            AddField(Information, "Subjects", StringArray(Work, "subjects"));
            AddField(Information, "Places", StringArray(Work, "subject_places"));
            AddField(Information, "People", StringArray(Work, "subject_people"));
            AddField(Information, "Periods", StringArray(Work, "subject_times"));
            AddSection(Information);
            StackPanel Section = CreateSection("Description");
            string Description = string.Empty;
            if (Work.TryGetProperty("description", out JsonElement Text))
            {
                if (Text.ValueKind == JsonValueKind.String)
                    Description = Text.GetString();
                else if (Text.ValueKind == JsonValueKind.Object && Text.TryGetProperty("value", out JsonElement Value)
                    && Value.ValueKind == JsonValueKind.String)
                    Description = Value.GetString();
            }
            TextBlock Introduction = new()
            {
                Text = string.IsNullOrWhiteSpace(Description)
                    ? "No description is available. Open the work on Open Library for more information." : Description,
                TextWrapping = TextWrapping.Wrap
            };
            Introduction.Classes.Add("BodyText");
            Section.Children.Add(Introduction);
            StackPanel Links = new();
            Links.Classes.Add("CardActions");
            AddLink(Links, "Read on Open Library", "https://openlibrary.org" + fItem.GetProperty("key").GetString());
            Section.Children.Add(Links);
            AddSection(Section);
            SetSource("Source: Open Library, an Internet Archive initiative. Community catalogue information may be incomplete.");
            fStatus.Text = Result.ReasonPhrase ?? string.Empty;
            fStatus.IsVisible = !string.IsNullOrWhiteSpace(fStatus.Text);
            if (string.IsNullOrWhiteSpace(fStatus.Text) && fStatus.Parent is StackPanel Status && Status.Parent is Border Card)
                Card.IsVisible = false;
            fLoaded = true;
        }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
        {
            // Leaving the form cancels its outstanding request.
        }
        catch (Exception Ex)
        {
            if (!Cancellation.IsCancellationRequested && this.IsAttachedToVisualTree())
            {
                ExceptionLog.WriteException("BookDetailForm.LoadAsync", Ex, false);
                fStatus.Text = "Open Library could not be read. Please try again.";
                fRetry.IsVisible = true;
            }
        }
        finally
        {
            if (fCancellation == Cancellation)
            {
                fCancellation = null;
                fProgress.IsVisible = false;
            }
        }
    }
    /// <summary>
    /// Loads or retries the work from the visible form.
    /// </summary>
    async void OnLoadRequested(object Sender, RoutedEventArgs Args)
    {
        await LoadAsync();
    }
    /// <summary>
    /// Cancels work when navigating away without discarding completed content.
    /// </summary>
    void OnBookUnloaded(object Sender, RoutedEventArgs Args)
    {
        CancellationTokenSource Cancellation = fCancellation;
        fCancellation = null;
        Cancellation?.Cancel();
    }

    // ● constructor
    /// <summary>
    /// Creates a detail card for the work selected in search results.
    /// </summary>
    /// <param name="Service">The registered Books service.</param>
    /// <param name="Item">The selected work search result.</param>
    public BookDetailForm(BookService Service, JsonElement Item)
    {
        ArgumentNullException.ThrowIfNull(Service);
        if (!BookService.IsWorkKey(Item.GetProperty("key").GetString()))
            throw new ArgumentException("A valid Open Library work is required.", nameof(Item));
        fService = Service;
        fItem = Item.Clone();
        SetHeader(Item.GetProperty("title").GetString(), StringArray(Item, "author_name"), "📚");
        StackPanel Status = CreateSection("Open Library");
        fStatus.Classes.Add("SecondaryText");
        fRetry.Classes.Add("PrimaryButton");
        Status.Children.Add(fStatus);
        Status.Children.Add(fProgress);
        Status.Children.Add(fRetry);
        AddSection(Status);
        Loaded += OnLoadRequested;
        Unloaded += OnBookUnloaded;
        fRetry.Click += OnLoadRequested;
    }
}
