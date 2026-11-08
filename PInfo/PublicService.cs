/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */
namespace PInfo;


/// <summary>
/// Base service for all public services
/// </summary>
public abstract class PublicService
{
    // ● constructor
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="Name">The non-empty unique service name.</param>
    /// <exception cref="ArgumentException">The name is null, empty, or whitespace.</exception>
    public PublicService(string Name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        this.Name = Name;
    }

    // ● properties
    /// <summary>
    /// A unique name for the service, among all the other services.
    /// It is displayed in the service list view
    /// </summary>
    public string Name { get; }
}
