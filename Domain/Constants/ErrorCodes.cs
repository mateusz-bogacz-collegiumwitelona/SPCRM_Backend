namespace Domain.Constants
{
    public static class ErrorCodes
    {
        // Basic
        public const string ValidationError = "VALIDATION_ERROR";
        public const string InternalError = "INTERNAL_ERROR";
        public const string BadRequest = "BAD_REQUEST";
        public const string NotFound = "NOT_FOUND";

        // Validation 
        public const string EmailRequired = "VAL_001";
        public const string EmailInvalid = "VAL_002";
        public const string PasswordRequired = "VAL_003";
        public const string TitleRequired = "VAL_004";
        public const string TitleLengthInvalid = "VAL_005";
        public const string MessageRequired = "VAL_006";
        public const string MessageLengthInvalid = "VAL_007";

        // Domain / Auth
        public const string UserNotFound = "AUTH_001";
        public const string EmailNotConfirmed = "AUTH_002";
        public const string InvalidCredentials = "AUTH_003";
        public const string NoRolesAssigned = "AUTH_004";
        public const string UnauthorizedAccess = "AUTH_005";

        // Company
        public const string CompanyNotFound = "COM_001";

        // Contact
        public const string InvalidContactDetailType = "CON_002";
        
        // Product
        public const string ProductNotFound = "PROD_001";

        // Note
        public const string NoteNotFound = "NOTE_001";
        public const string NoteIdRequired = "NOTE_002";
        public const string NoteTitleIsNotValid = "NOTE_003";
        public const string NoteContentIsNotValid = "NOTE_004";
        public const string NoteTargetNotFound = "NOTE_005";

        // Mailing
        public const string ClientNotFound = "MAIL_001";
    }
}
