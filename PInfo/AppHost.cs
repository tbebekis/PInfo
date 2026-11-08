/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Provides shared application services and functionality.
/// </summary>
static public class AppHost
{
    // ● private
    static readonly ReadOnlyCollection<PublicServiceItem> fPublicServices = RegisterServices();

    /// <summary>
    /// Registers each service together with its form factory and checks unique service names.
    /// </summary>
    static ReadOnlyCollection<PublicServiceItem> RegisterServices()
    {
        List<PublicServiceItem> Items = new();
        Items.Add(new PublicServiceItem(new CountryService(), () => new CountryListForm()));

        HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase);
        foreach (PublicServiceItem Item in Items)
        {
            if (!Names.Add(Item.Service.Name))
                throw new InvalidOperationException($"Duplicate public service name: {Item.Service.Name}");
        }
        return Items.AsReadOnly();
    }

    // ● properties
    /// <summary>
    /// The registered services and their form factories, exposed as a read-only collection.
    /// </summary>
    static public IReadOnlyList<PublicServiceItem> PublicServices => fPublicServices;
}
