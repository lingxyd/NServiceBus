namespace NServiceBus.Installation
{
    using System.Threading.Tasks;

    /// <summary>
    /// Interface invoked by the infrastructure when going to install an endpoint.
    /// </summary>
    public interface INeedToInstallSomething
    {
        /// <summary>
        /// Performs the installation providing permission for the given user.
        /// </summary>
        /// <param name="identity">The user for whom permissions will be given.</param>
        /// <param name="config"><see cref="Configure"/> instance.</param>
        Task InstallAsync(string identity, Configure config);
    }
}
