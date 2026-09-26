namespace Domain.Constants
{
    public static class CacheTags
    {
        // Company
        public const string CompaniesMap = "companies_map";
        public const string CompaniesList = "companies_list";
        public const string CompaniesSimpleList = "companies_simple_list";
        public const string CompanyDetails = "company_details";
        public const string CompanyAddresses = "company_addresses";
        public const string CompanyAddressTypes = "company_address_types";
        public const string CompanyDebts = "company_debts";

        public static readonly string[] CompanyAll =
        {
            CompaniesMap,
            CompaniesList,
            CompaniesSimpleList,
            CompanyDetails,
            CompanyAddresses,
            CompanyDebts
        };

        // Contacts
        public const string ContactsList = "contacts_list";
        public const string ContactDetails = "contact_details";
        public const string ContactTypes = "contact_types";
        public const string ContactWays = "contact_ways";
        public const string ContactNotes = "contact_notes";
        public const string ContactTasks = "contact_tasks";
        public const string ContactCompanies = "contact_companies";
        public const string ContactToDeals = "contact_to_deals";
        public const string ContactOwners = "contact_owners";

        public static readonly string[] ContactAll =
        {
            ContactsList,
            ContactDetails,
            ContactWays,
            ContactNotes,
            ContactTasks,
            ContactCompanies,
            ContactToDeals,
            ClientDataForMailing
        };

        // Deals
        public const string DealsList = "deals_list";
        public const string DealDetails = "deal_details";
        public const string DealStatuses = "deal_statuses";
        public const string DealProducts = "deal_products";
        public const string DealNotes = "deal_notes";
        public const string DealTasks = "deal_tasks";
        public const string DealAssignableContacts = "deal_assignable_contacts";
        public static readonly string[] DealAll =
        {
            DealsList,
            DealDetails,
            DealProducts,
            DealNotes,
            DealTasks,
            DealAssignableContacts
        };

        // Invoices
        public const string InvoicesList = "invoices_list";
        public const string InvoiceDetails = "invoice_details";
        public const string InvoiceProducts = "invoice_products";
        public const string InvoicePayments = "invoice_payments";
        public const string InvoicePaymentSummary = "invoice_payment_summary";

        public static readonly string[] InvoiceAll =
        {
            InvoicesList,
            InvoiceDetails,
            InvoiceProducts,
            InvoicePayments,
            InvoicePaymentSummary,
            CompanyDebts
        };

        // products
        public const string ProductsList = "products_list";
        public const string ProductCategories = "product_categories";
        public const string ProductDetails = "product_details";
        public const string ProductAutocomplete = "product_autocomplete";
        public const string ProductDeals = "product_deals";
        public const string ProductInvoices = "product_invoices";
        public const string ProductMailing = "product_mailing";

        public static readonly string[] ProductAll =
        {
            ProductsList,
            ProductCategories,
            ProductSteelGrades,
            ProductDetails,
            ProductAutocomplete,
            ProductDeals,
            ProductInvoices,
            ProductMailing,
            DealProducts,
            InvoiceProducts
        };

        // Currencies
        public const string CurrencyList = "currency_list";
        public const string CurrencySimple = "currency_simple";

        public static readonly string[] CurrencyAll =
        {
            CurrencyList,
            CurrencySimple
        };

        // Analytics
        public const string AnalyticsTeamKpi = "analytics_team_kpi";
        public const string AnalyticsTeamChart = "analytics_team_chart";
        public const string AnalyticsEmployeeChart = "analytics_employee_chart";
        public const string AnalyticsLeaderboard = "analytics_leaderboard";
        public const string AnalyticsEmployeeKpi = "analytics_employee_kpi";
        public const string AnalyticsUserKpi = "analytics_user_kpi";
        public const string AnalyticsAdminMetrics = "analytics_admin_metrics";

        public static readonly string[] AnalyticsAll =
        {
            AnalyticsTeamKpi,
            AnalyticsTeamChart,
            AnalyticsEmployeeChart,
            AnalyticsLeaderboard,
            AnalyticsEmployeeKpi,
            AnalyticsUserKpi
        };

        // Users
        public const string UsersSimpleList = "users_simple_list";
        public const string UsersList = "users_list";
        public const string UserDetails = "user_details";
        public const string AvailableOwners = "available_owners";
        public const string RolesList = "roles_list";

        public static readonly string[] UserAll =
        {
            UsersList,
            UsersSimpleList,
            UserDetails,
            AvailableOwners,
            RolesList
        };

        // Tasks
        public const string TasksForCalendar = "tasks_calendar";
        public const string TaskDictionary = "task_dictionary";
        public const string TaskDetails = "task_details";
        public const string TaskContacs = "task_contacts";
        public const string TaskDeals = "task_deals";
        public const string UserTasks = "user_tasks";

        public static readonly string[] TaskAll =
        {
            TasksForCalendar,
            TaskDictionary,
            TaskDetails,
            TaskContacs,
            TaskDeals,
            UserTasks,
            TaskNotes,
            ContactTasks,
            DealTasks
        };

        // Note
        public const string TaskNotes = "note_details";
        public static readonly string[] NoteAll =
        {
            TaskNotes,
            DealNotes,
            ContactNotes
        };

        // Contacts 
        public const string ClientDataForMailing = "client_data_for_mailing";

        public static readonly string[] ClientDataForMailingAll =
        {
            ClientDataForMailing
        };

        // Offers
        public const string OffersList = "offers_list";
        public const string OfferDetails = "offer_details";
        public const string OfferClientDetails = "offer_client_details";
        public const string OfferProducts = "offer_products";
        public const string OfferStatuses = "offer_statuses";
        public const string OfferAllowedActions = "offer_allowed_actions";

        public static readonly string[] OffersAll =
        {
            OffersList,
            OfferDetails,
            OfferClientDetails,
            OfferProducts,
            OfferAllowedActions
        };

        // Steel grades
        public const string ProductSteelGrades = "product_steel_grades";
        public const string SteelGradeList = "steel_grade_list";
        public const string SteelGradeAssociatedProducts = "steel_grade_associated_products";

        public static readonly string[] SteelGradeAll =
        {
            ProductSteelGrades,
            SteelGradeList,
            SteelGradeAssociatedProducts,
        };

        // promotions
        public const string PromotionsList = "promotions_list";
        public const string PromotionDetails = "promotion_details";

        public static readonly string[] PromotionAll =
        {
            PromotionsList,
            PromotionDetails
        };

        // unit of measures
        public const string UnitSimple = "unit_simple";
        public const string UnitList = "unit_list";

        public static readonly string[] UnitAll =
        {
            UnitSimple,
            UnitList
        };
    }
}
