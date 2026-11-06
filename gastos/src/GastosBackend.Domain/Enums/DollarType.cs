namespace GastosBackend.Domain.Enums
{
    /// <summary>
    /// Which dollar a USD amount is converted with. The numbers are stored in the database, so they never change:
    /// 0 is the default, which is what every existing row gets.
    /// </summary>
    public enum DollarType
    {
        /// <summary>The official rate ("oficial" in dolarapi.com).</summary>
        Official = 0,

        /// <summary>The informal market rate ("blue").</summary>
        Blue = 1,

        /// <summary>The stock-market rate, MEP ("bolsa" in dolarapi.com).</summary>
        Mep = 2
    }
}
