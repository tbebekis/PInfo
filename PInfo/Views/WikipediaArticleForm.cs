/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Displays a selected English Wikipedia introduction with source, licence, and browser links.
/// </summary>
public class WikipediaArticleForm : AppCardForm
{
    // ● private
    readonly long fPageId;
    readonly TextBlock fStatus = new() { Text = "Loading Wikipedia. Please wait...", TextWrapping = TextWrapping.Wrap };
    readonly ProgressBar fProgress = new() { IsIndeterminate = true };
    readonly Button fRetry = new() { Content = "Try again", IsVisible = false };
    CancellationTokenSource fCancellation;
    bool fLoaded;

    /// <summary>
    /// Loads the chosen page once and retains its detail card when returning with Back.
    /// </summary>
    async Task LoadAsync()
    {
        if (fLoaded || fCancellation != null)
            return;
        using CancellationTokenSource Cancellation = new();
        fCancellation = Cancellation;
        fProgress.IsVisible = true;
        fRetry.IsVisible = false;
        fStatus.Text = "Loading Wikipedia. Please wait...";
        try
        {
            ApiClientResult Result = await WikipediaService.GetArticleAsync(fPageId, Cancellation.Token);
            if (Cancellation.IsCancellationRequested || !this.IsAttachedToVisualTree())
                return;
            if (Result.ResultStatus != ApiClientResultStatus.Success)
            {
                fStatus.Text = Result.ReasonPhrase;
                fRetry.IsVisible = true;
                return;
            }
            using JsonDocument Document = JsonDocument.Parse(Result.ResponseJsonText);
            JsonElement Page = Document.RootElement.GetProperty("query").GetProperty("pages")[0];
            if (Page.TryGetProperty("missing", out _))
            {
                fStatus.Text = "This article is no longer available. Go back and try another search.";
                return;
            }
            long ArticleId = Page.GetProperty("pageid").GetInt64();
            string Title = Page.GetProperty("title").GetString();
            SetHeader(Title, "English Wikipedia", "📖");
            StackPanel Section = CreateSection("Introduction");
            if (Page.TryGetProperty("pageprops", out JsonElement Properties) && Properties.TryGetProperty("disambiguation", out _))
                AddField(Section, "Multiple meanings", "This is a disambiguation page. Open Wikipedia to choose a meaning, or go back and refine the search.");
            string Extract = Page.TryGetProperty("extract", out JsonElement Text) ? Text.GetString() : string.Empty;
            TextBlock Introduction = new()
            {
                Text = string.IsNullOrWhiteSpace(Extract) ? "No short introduction is available. Open the article on Wikipedia to read more." : Extract,
                TextWrapping = TextWrapping.Wrap
            };
            Introduction.Classes.Add("BodyText");
            Section.Children.Add(Introduction);
            StackPanel Links = new();
            Links.Classes.Add("CardActions");
            string Url = Page.TryGetProperty("fullurl", out JsonElement Address) ? Address.GetString()
                : "https://en.wikipedia.org/?curid=" + ArticleId.ToString(CultureInfo.InvariantCulture);
            if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri ArticleUrl) || ArticleUrl.Scheme != "https" || ArticleUrl.Host != "en.wikipedia.org")
                Url = "https://en.wikipedia.org/?curid=" + ArticleId.ToString(CultureInfo.InvariantCulture);
            AddLink(Links, "Read on Wikipedia", Url);
            AddLink(Links, "Contributors and article history", "https://en.wikipedia.org/w/index.php?curid="
                + ArticleId.ToString(CultureInfo.InvariantCulture) + "&action=history");
            AddLink(Links, "Licence: CC BY-SA 4.0", "https://creativecommons.org/licenses/by-sa/4.0/");
            Section.Children.Add(Links);
            AddSection(Section);
            SetSource("Source: English Wikipedia contributors. Introductory excerpt; the full article is available at the link above.");
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
                ExceptionLog.WriteException("WikipediaArticleForm.LoadAsync", Ex, false);
                fStatus.Text = "Wikipedia could not be read. Please try again.";
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
    /// Loads or retries the article from the visible form.
    /// </summary>
    async void OnLoadRequested(object Sender, RoutedEventArgs Args)
    {
        await LoadAsync();
    }
    /// <summary>
    /// Cancels work when navigating away without discarding completed content.
    /// </summary>
    void OnArticleUnloaded(object Sender, RoutedEventArgs Args)
    {
        CancellationTokenSource Cancellation = fCancellation;
        fCancellation = null;
        Cancellation?.Cancel();
    }

    // ● constructor
    /// <summary>
    /// Creates a detail card for the selected Wikipedia page identifier.
    /// </summary>
    /// <param name="PageId">The page identifier returned by search.</param>
    public WikipediaArticleForm(long PageId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(PageId);
        fPageId = PageId;
        SetHeader("Wikipedia", "English Wikipedia", "📖");
        StackPanel Status = CreateSection("Wikipedia");
        fStatus.Classes.Add("SecondaryText");
        fRetry.Classes.Add("PrimaryButton");
        Status.Children.Add(fStatus);
        Status.Children.Add(fProgress);
        Status.Children.Add(fRetry);
        AddSection(Status);
        Loaded += OnLoadRequested;
        Unloaded += OnArticleUnloaded;
        fRetry.Click += OnLoadRequested;
    }
}
