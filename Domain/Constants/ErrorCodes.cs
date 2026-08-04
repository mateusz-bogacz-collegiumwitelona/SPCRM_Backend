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
        public const string NameRequired = "VAL_008";
        public const string NameLengthInvalid = "VAL_009";
        public const string PhoneInvalid = "VAL_010";
        public const string UrlInvalid = "VAL_011";
        public const string GuidRequired = "VAL_012";
        public const string GuidInvalid = "VAL_013";
        public const string PageNumberInvalid = "VAL_014";
        public const string PageSizeInvalid = "VAL_015";
        public const string TypeInvalid = "VAL_016";
        public const string LabelRequired = "VAL_017";
        public const string LabelLengthInvalid = "VAL_018";
        public const string NumberRequired = "VAL_019";
        public const string NumberInvalid = "VAL_020";
        public const string LinkedInUrlInvalid = "VAL_021";
        public const string LinkedInUrlRequired = "VAL_022";

        // Domain / Auth
        public const string UserNotFound = "AUTH_001";
        public const string EmailNotConfirmed = "AUTH_002";
        public const string InvalidCredentials = "AUTH_003";
        public const string NoRolesAssigned = "AUTH_004";
        public const string UnauthorizedAccess = "AUTH_005";

        // Company
        public const string CompanyNotFound = "COM_001";

        // Contact
        public const string InvalidContactDetailType = "CON_001";
        public const string PrimaryContactDetailRequired = "CON_002";
        // Product
        public const string ProductNotFound = "PROD_001";

        // Note
        public const string NoteNotFound = "NOTE_001";
        public const string NoteTitleIsNotValid = "NOTE_003";
        public const string NoteContentIsNotValid = "NOTE_004";
        public const string NoteTargetNotFound = "NOTE_005";

        // Mailing
        public const string ClientNotFound = "MAIL_001";
    }
}
