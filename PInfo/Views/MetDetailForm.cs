/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays a Met artwork and its public-domain preview in a separate retained detail card.
/// </summary>
public class MetDetailForm : AppCardForm
{
    // ● private
    readonly MetService fService;
    readonly int fId;
    readonly StackPanel fInformation;
    readonly Image fImage = new() { MaxHeight = 420, Stretch = Stretch.Uniform };
    readonly TextBlock fStatus = new() { TextWrapping = TextWrapping.Wrap };
    readonly Button fRetry = new() { Content = "Try again" };
    readonly ProgressBar fProgress = new() { IsIndeterminate = true };
    Avalonia.Media.Imaging.Bitmap fBitmap;
    CancellationTokenSource fCancellation;
    bool fLoaded;

    /// <summary>
    /// Loads current catalogue metadata and an optional image without discarding useful text on image failure.
    /// </summary>
    async Task LoadAsync()
    {
        if (fLoaded || fCancellation != null || !this.IsAttachedToVisualTree())
            return;
        using CancellationTokenSource Cancellation = new();
        fCancellation = Cancellation;
        fRetry.IsVisible = false;
        fProgress.IsVisible = true;
        fStatus.Text = "Loading artwork. Please wait...";
        try
        {
            ApiClientResult Result = await fService.GetObjectAsync(fId, Cancellation.Token);
            if (Cancellation.IsCancellationRequested || fCancellation != Cancellation || !this.IsAttachedToVisualTree())
                return;
            if (Result.ResultStatus != ApiClientResultStatus.Success)
            {
                fStatus.Text = Result.ReasonPhrase;
                fRetry.IsVisible = true;
                return;
            }
            using JsonDocument Document = JsonDocument.Parse(Result.ResponseJsonText);
            JsonElement Item = Document.RootElement;
            SetHeader(MetService.Text(Item, "title"), MetService.Text(Item, "artistDisplayName"), string.Empty);
            while (fInformation.Children.Count > 1)
                fInformation.Children.RemoveAt(1);
            foreach (var Field in new (string Key, string Label)[]
            {
                ("artistDisplayName", "Artist"), ("artistDisplayBio", "Artist biography"), ("objectDate", "Date"),
                ("culture", "Culture"), ("period", "Period"), ("dynasty", "Dynasty"), ("medium", "Materials"),
                ("dimensions", "Dimensions"), ("department", "Department"), ("classification", "Classification"),
                ("country", "Country"), ("creditLine", "Credit"), ("accessionNumber", "Accession number")
            })
                AddField(fInformation, Field.Label, MetService.Text(Item, Field.Key));
            fStatus.Text = "Loading image. Please wait...";
            byte[] Bytes = await fService.GetImageAsync(Item, Cancellation.Token);
            if (Cancellation.IsCancellationRequested || fCancellation != Cancellation || !this.IsAttachedToVisualTree())
                return;
            bool HasImage = false;
            if (Bytes != null)
            {
                try
                {
                    using MemoryStream Stream = new(Bytes);
                    fBitmap = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(Stream, 900);
                    fImage.Source = fBitmap;
                    HasImage = true;
                }
                catch (Exception Ex) when (Ex is ArgumentException || Ex is InvalidOperationException || Ex is IOException)
                {
                    // Keep the catalogue details available if an image is damaged or unsupported.
                }
            }
            fImage.IsVisible = HasImage;
            fStatus.Text = (Result.ReasonPhrase ?? string.Empty) + (HasImage ? string.Empty
                : MetService.ImageUrl(Item).Length == 0 ? "\nNo Open Access image is available. View the work at The Met."
                : "\nThe image could not be loaded. Press Try again.");
            fRetry.IsVisible = !HasImage && MetService.ImageUrl(Item).Length > 0;
            fLoaded = !fRetry.IsVisible;
        }
        catch (OperationCanceledException) when (Cancellation.IsCancellationRequested)
        {
            // Navigation cancels only the unfinished request, preserving the form for Back.
        }
        catch (Exception Ex)
        {
            if (!Cancellation.IsCancellationRequested && fCancellation == Cancellation && this.IsAttachedToVisualTree())
            {
                ExceptionLog.WriteException("MetDetailForm.LoadAsync", Ex, false);
                fStatus.Text = "The artwork could not be read. Please try again.";
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
    /// Loads the selected artwork or retries a failed request.
    /// </summary>
    async void OnLoadRequested(object Sender, RoutedEventArgs Args)
    {
        await LoadAsync();
    }
    /// <summary>
    /// Cancels outstanding requests when leaving the detail form.
    /// </summary>
    void OnArtUnloaded(object Sender, RoutedEventArgs Args)
    {
        CancellationTokenSource Cancellation = fCancellation;
        fCancellation = null;
        Cancellation?.Cancel();
    }

    // ● constructor
    /// <summary>
    /// Creates a detail form from a selected, detached catalogue result.
    /// </summary>
    public MetDetailForm(MetService Service, JsonElement Item)
        : this(Service, Item.GetProperty("objectID").GetInt32(), MetService.Text(Item, "title"))
    {
    }
    /// <summary>
    /// Reopens an artwork by stable identifier, including from Favorites.
    /// </summary>
    public MetDetailForm(MetService Service, int Id, string Name)
    {
        ArgumentNullException.ThrowIfNull(Service);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(Id);
        fService = Service;
        fId = Id;
        SetHeader(Name, string.Empty, string.Empty);
        AddTitleAction(new FavoriteButton(FavoriteService.Art(Id, Name)));
        StackPanel Picture = CreateSection(string.Empty);
        Picture.Children[0].IsVisible = false;
        Picture.Children.Add(fImage);
        Picture.Children.Add(fStatus);
        Picture.Children.Add(fProgress);
        fRetry.Classes.Add("PrimaryButton");
        Picture.Children.Add(fRetry);
        AddSection(Picture);
        fInformation = CreateSection("Information");
        AddField(fInformation, "Museum", "The Metropolitan Museum of Art, New York");
        AddSection(fInformation);
        StackPanel Links = CreateSection("Source");
        AddLink(Links, "View at The Met", "https://www.metmuseum.org/art/collection/search/" + Id.ToString(CultureInfo.InvariantCulture));
        AddLink(Links, "Open Access · CC0", "https://www.metmuseum.org/policies/image-resources");
        AddSection(Links);
        SetSource("The Metropolitan Museum of Art · Open Access catalogue. Only images explicitly marked public domain are displayed.");
        Loaded += OnLoadRequested;
        Unloaded += OnArtUnloaded;
        fRetry.Click += OnLoadRequested;
    }
}
