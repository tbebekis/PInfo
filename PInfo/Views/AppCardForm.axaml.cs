/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo.Views;

/// <summary>
/// Provides a reusable detail-card layout and helpers for headers, sections, fields, and links.
/// </summary>
public partial class AppCardForm : AppForm
{
    // ● protected
    /// <summary>
    /// Adds a compact action at the upper right of the title, reserving space for wrapping title text.
    /// </summary>
    /// <param name="ActionButton">The title action configured by the derived card form.</param>
    protected void AddTitleAction(Button ActionButton)
    {
        ArgumentNullException.ThrowIfNull(ActionButton);
        if (TitleText.Parent is Grid Heading)
        {
            ActionButton.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
            ActionButton.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            ActionButton.Margin = new Thickness(8, 0, 0, 0);
            Grid.SetColumn(ActionButton, 1);
            Heading.Children.Add(ActionButton);
        }
    }
    /// <summary>
    /// Adds an action button below the header badges, showing the optional action area.
    /// </summary>
    /// <param name="ActionButton">The button configured by the derived card form.</param>
    protected void AddHeaderAction(Button ActionButton)
    {
        ArgumentNullException.ThrowIfNull(ActionButton);
        ActionButton.Classes.Add("PrimaryButton");
        ActionButton.Classes.Add("CardHeaderAction");
        HeaderActionsPanel.Children.Add(ActionButton);
        HeaderActionsPanel.IsVisible = true;
    }
    /// <summary>
    /// Adds a compact header badge when text is available.
    /// </summary>
    protected void AddBadge(string Text)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return;
        TextBlock Label = new() { Text = Text };
        Label.Classes.Add("BadgeText");
        Border Badge = new() { Child = Label, Margin = new Thickness(0, 0, 8, 0) };
        Badge.Classes.Add("Badge");
        BadgePanel.Children.Add(Badge);
    }
    /// <summary>
    /// Creates a section containing a heading and vertically arranged fields.
    /// </summary>
    protected StackPanel CreateSection(string Title)
    {
        StackPanel Panel = new();
        Panel.Classes.Add("CardFields");
        TextBlock Heading = new() { Text = Title };
        Heading.Classes.Add("CardSectionTitle");
        Panel.Children.Add(Heading);
        return Panel;
    }
    /// <summary>
    /// Adds a labeled value, omitting fields with no available content.
    /// </summary>
    protected void AddField(StackPanel Section, string Label, string Value)
    {
        if (string.IsNullOrWhiteSpace(Value))
            return;
        StackPanel Field = new();
        Field.Classes.Add("CardField");
        TextBlock LabelText = new() { Text = Label };
        LabelText.Classes.Add("FieldLabel");
        TextBlock ValueText = new() { Text = Value };
        ValueText.Classes.Add("FieldValue");
        Field.Children.Add(LabelText);
        Field.Children.Add(ValueText);
        Section.Children.Add(Field);
    }
    /// <summary>
    /// Displays a section only if it contains data as well as its heading.
    /// </summary>
    protected void AddSection(StackPanel Section)
    {
        if (Section.Children.Count <= 1)
            return;
        Border Card = new() { Child = Section };
        Card.Classes.Add("CardSection");
        SectionPanel.Children.Add(Card);
    }
    /// <summary>
    /// Formats multiple values as separate lines, ignoring empty values.
    /// </summary>
    protected string JoinValues(IEnumerable<string> Values)
    {
        return Values == null ? string.Empty : string.Join(Environment.NewLine,
            Values.Where(Value => !string.IsNullOrWhiteSpace(Value)));
    }
    /// <summary>
    /// Adds an external link action only for a valid HTTPS address.
    /// </summary>
    protected void AddLink(StackPanel Panel, string Caption, string Url)
    {
        if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri Address) || Address.Scheme != Uri.UriSchemeHttps)
            return;
        HyperlinkButton Link = new() { Content = Caption, NavigateUri = Address };
        Link.Classes.Add("CardAction");
        Panel.Children.Add(Link);
    }
    /// <summary>
    /// Sets the detail header, hiding optional identity text when it is unavailable.
    /// </summary>
    /// <param name="Title">The primary entity name.</param>
    /// <param name="Subtitle">The optional secondary name.</param>
    /// <param name="Icon">An optional emoji or short text symbol.</param>
    protected void SetHeader(string Title, string Subtitle, string Icon)
    {
        TitleText.Text = Title;
        SubtitleText.Text = Subtitle;
        SubtitleText.IsVisible = !string.IsNullOrWhiteSpace(Subtitle);
        HeaderIconText.Text = Icon;
        HeaderIconText.IsVisible = !string.IsNullOrWhiteSpace(Icon);
    }
    /// <summary>
    /// Sets the optional source attribution displayed below the sections.
    /// </summary>
    /// <param name="Text">The source text, or an empty value to hide it.</param>
    protected void SetSource(string Text)
    {
        SourceText.Text = Text;
        SourceText.IsVisible = !string.IsNullOrWhiteSpace(Text);
    }

    // ● constructor
    /// <summary>
    /// Creates the shared card controls without invoking derived form behavior.
    /// </summary>
    public AppCardForm()
    {
        InitializeComponent();
    }
}
