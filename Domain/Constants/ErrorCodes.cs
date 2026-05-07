namespace Domain.Constants
{
    public static class ErrorCodes
    {
        // Ogólne kody błędów
        public const string ValidationError = "VALIDATION_ERROR";
        public const string InternalError = "INTERNAL_ERROR";
        public const string BadRequest = "BAD_REQUEST";
        public const string NotFound = "NOT_FOUND";

        // Szczegółowe błędy walidacji 
        public const string EmailRequired = "VAL_001";
        public const string EmailInvalid = "VAL_002";
        public const string PasswordRequired = "VAL_003";
        public const string TitleRequired = "VAL_004";
        public const string TitleLengthInvalid = "VAL_005";
        public const string MessageRequired = "VAL_006";
        public const string MessageLengthInvalid = "VAL_007";

        // Błędy Domenowe / Autoryzacji
        public const string UserNotFound = "AUTH_001";
        public const string EmailNotConfirmed = "AUTH_002";
        public const string InvalidCredentials = "AUTH_003";
        public const string NoRolesAssigned = "AUTH_004";
    }
}
