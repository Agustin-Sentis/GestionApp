namespace GastosBackend.Application.dtos.Requests
{
    /// <summary>Start turning the 2FA on: the current password, so a stolen session cannot do it.</summary>
    public class TwoFactorSetupRequest
    {
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>The first code of the authenticator app, to confirm it was set up correctly.</summary>
    public class TwoFactorEnableRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>Turn the 2FA off, or replace the recovery codes: the password and a code (from the app, or a recovery one).</summary>
    public class TwoFactorPasswordCodeRequest
    {
        public string Password { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}
