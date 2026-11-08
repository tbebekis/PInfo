/* PInfo, Copyright (c) 2026 Theo Bebekis, see License. */

namespace PInfo;

/// <summary>
/// Associates a shared public service with a factory that creates its UI form on demand.
/// </summary>
public class PublicServiceItem
{
    // ● constructor
    /// <summary>Creates a service registration without creating its form.</summary>
    /// <param name="Service">The shared service instance.</param>
    /// <param name="CreateForm">The factory to invoke on the UI thread when opening the service.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public PublicServiceItem(PublicService Service, Func<AppForm> CreateForm)
    {
        ArgumentNullException.ThrowIfNull(Service);
        ArgumentNullException.ThrowIfNull(CreateForm);
        this.Service = Service;
        this.CreateForm = CreateForm;
    }

    // ● properties
    /// <summary>The shared service instance.</summary>
    public PublicService Service { get; }
    /// <summary>The factory that creates a new service form when invoked on the UI thread.</summary>
    public Func<AppForm> CreateForm { get; }
}
