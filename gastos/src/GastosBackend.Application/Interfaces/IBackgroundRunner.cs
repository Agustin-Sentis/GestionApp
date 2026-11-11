namespace GastosBackend.Application.Interfaces
{
    /// <summary>Runs work after the response is on its way, for what the user does not need to wait for (sending a mail).</summary>
    public interface IBackgroundRunner
    {
        /// <summary>
        /// Starts <paramref name="work"/> in the background and returns at once. The work gets its own DI scope, because
        /// everything the request used (the database context above all) is released when the request ends. A failure is
        /// logged, never thrown: nobody is waiting for the result.
        /// </summary>
        void Run<TService>(Func<TService, Task> work) where TService : notnull;
    }
}
