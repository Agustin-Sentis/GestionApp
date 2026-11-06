namespace GastosBackend.IntegrationTests.Support;

/// <summary>Ids of the migrations the tests stop at, to insert data in an older shape and then migrate it forward.</summary>
public static class MigrationIds
{
    public const string Init = "20261105211559_Init";
    public const string AddExchangeRateAndCreatedDate = "20261106014511_AddExchangeRateAndCreatedDate";
    public const string CaseInsensitiveCategoryNames = "20261106023938_CaseInsensitiveCategoryNames";
}
