namespace TransjapHorimetros.Application.Common;

public static class AuditActions
{
    public const string PhotoOcrProcessed = "PHOTO_OCR_PROCESSED";
    public const string PhotoOcrFailed = "PHOTO_OCR_FAILED";
    public const string PhotoOcrReprocessed = "PHOTO_OCR_REPROCESSED";
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
    public const string PhotoEvidenceCreated = "PHOTO_EVIDENCE_CREATED";
    public const string PhotoEvidenceViewed = "PHOTO_EVIDENCE_VIEWED";
    public const string PhotoEvidenceRevoked = "PHOTO_EVIDENCE_REVOKED";
}
