namespace Domain.Constants
{
    public static class ErrorCodes
    {
        // Basic
        public const string ValidationError = "VALIDATION_ERROR";
        public const string InternalError = "INTERNAL_ERROR";
        public const string BadRequest = "BAD_REQUEST";
        public const string NotFound = "NOT_FOUND";
        public const string InvalidDate = "INVALID_DATE";
        public const string InvalidSortColumn = "INVALID_SORT_COLUMN";

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
        public const string ContactNotFound = "CON_003";

        // Product
        public const string ProductNotFound = "PROD_001";
        public const string ProductAlreadyExists = "PROD_002";
        public const string InvalidCategory = "PROD_003";
        public const string InvalidProductName = "PROD_004";
        public const string InvalidProductSteelGrade = "PROD_005";
        public const string InvalidProductDimmension = "PROD_006";
        public const string InvalidProductWeight = "PROD_007";
        public const string InvalidProductPricePerUnit = "PROD_008";
        public const string InvalidProductStockQuantity = "PROD_009";
        public const string DiameterIsRequiredForPipeAndWire = "PROD_010";

        // Note
        public const string NoteNotFound = "NOTE_001";
        public const string NoteTitleIsNotValid = "NOTE_003";
        public const string NoteContentIsNotValid = "NOTE_004";
        public const string NoteTargetNotFound = "NOTE_005";

        // Mailing
        public const string ClientNotFound = "MAIL_001";

        // Promotion
        public const string InvalidPromotionDiscount = "PROMO_001";
        public const string InvalidPromotionPrice = "PROMO_002";
        public const string PromotionNotFound = "PROMO_003";
        public const string ActivePromotionAlreadyExists = "PROMO_004";
        public const string InvalidPromotionName = "PROM_005";
        public const string InvalidPromotioMinQuantity = "PROM_006";
        public const string InvalidPromotioMinWeight = "PROM_007";
        public const string DiscountPercentageAndPriceCannotBothChoice = "PROM_008";

        // Currency
        public const string CurrencyNotFound = "CUR_001";

        // Steel Grade
        public const string SteelGradeInUse = "ST_001";
        public const string DuplicateProductReassignment = "ST_002";
        public const string SteelGradeAlreadyExist = "ST_003";
        public const string InvalidSteelGradeName = "ST_004";
        public const string InvalidSteelGradeDensity = "ST_005";
        public const string InvalidSteelGradeStandard = "ST_006";
    }
}
