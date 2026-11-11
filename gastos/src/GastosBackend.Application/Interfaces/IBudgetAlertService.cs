namespace GastosBackend.Application.Interfaces
{
    public interface IBudgetAlertService
    {
        /// <summary>
        /// After an expense was saved: sends the mail if its category's month has just reached 80% of the budget or gone over it,
        /// the user asked for the mails and confirmed the address, and that alert was not sent already.
        /// If the mail cannot be sent the mark that it was sent is removed (so the next expense tries again) and the exception goes up.
        /// </summary>
        Task CheckAndNotify(Guid userId, Guid categoryId, DateOnly date);
    }
}
