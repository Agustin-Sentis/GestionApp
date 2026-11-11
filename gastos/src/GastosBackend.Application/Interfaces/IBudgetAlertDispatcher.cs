namespace GastosBackend.Application.Interfaces
{
    public interface IBudgetAlertDispatcher
    {
        /// <summary>
        /// Starts <see cref="IBudgetAlertService.CheckAndNotify"/> in the background and returns at once: saving an expense never
        /// waits for the mail, and never fails because of it. A failure is logged, not thrown.
        /// </summary>
        void Dispatch(Guid userId, Guid categoryId, DateOnly date);
    }
}
