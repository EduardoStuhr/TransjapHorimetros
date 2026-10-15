namespace TransjapHorimetros.Application.Common;

public static class AuditActions
{
    public const string MachineCreated = "CREATE_MACHINE";
    public const string MachineUpdated = "UPDATE_MACHINE";
    public const string ReadingCreated = "CREATE_READING";
    public const string ReadingMarkedSuspect = "READING_MARKED_SUSPECT";
    public const string WorkSiteCreated = "CREATE_WORKSITE";
    public const string WorkSiteUpdated = "UPDATE_WORKSITE";
    public const string MachineQrCodeIssued = "ISSUE_MACHINE_QR_CODE";
    public const string MachineQrCodeDeactivated = "DEACTIVATE_MACHINE_QR_CODE";
    public const string LoginSuccess = "LOGIN_SUCCESS";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string Logout = "LOGOUT";
    public const string RefreshTokenRotated = "REFRESH_TOKEN_ROTATED";
    public const string UserCreated = "USER_CREATED";
    public const string UserUpdated = "USER_UPDATED";
    public const string UserActivated = "USER_ACTIVATED";
    public const string UserDeactivated = "USER_DEACTIVATED";
    public const string UserRolesChanged = "USER_ROLES_CHANGED";
}
