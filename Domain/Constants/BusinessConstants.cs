using System.Globalization;

namespace Domain.Constants
{
    public static class BusinessConstants
    {
        // Finance
        public const decimal CurrencyScaleFactor = 10000m;
        public const string DefaultCurrencyCode = "PLN";
        public const int DefaultCurrencyDecimalPlaces = 2;
        public const int DefaultPercentageDecimalPlaces = 2;

        // Date
        public const string FullPolishDateTime = "dddd, dd MMMM yyyy HH:mm";
        public const string StandardDateFormat = "yyyy-MM-dd";

        // Culture
        public static readonly CultureInfo DefaultCultureCode = CultureInfo.GetCultureInfo("pl-PL");

        // Offer
        public const int DefaultMailingValidityDays = 7;
        public const int OfferNumberSuffixLength = 6;

        // Unit 
        public const string DefaultUnitSymbol = "szt.";

        // Role 
        public const string AdminNormalized = "ADMIN";
        public const string ManagerNormalized = "MANAGER";
        public const string RoleAdmin = "Admin";
        public const string RoleManager = "Manager";

        // Company
        public const int RequiredHeadquartersCount = 1;
        public const int MinimumCompanyAddressesCount = 1;

        // Product 
        public const decimal DimensionScaleFactor = 10m;
        public const decimal WeightScaleFactor = 1000m;

        // Steel Grade
        public const decimal DensityScaleFactor = 1000m;

        // User
        public const int UserNamePartLength = 3;
        public const int UserNameRandomMin = 100;
        public const int UserNameRandomMax = 1000;

    }
}
