namespace E2E.Api.Tests.Config
{
    public static class OptionSetValues
    {
        public static class General
        {
            public static class StatusCode
            {
                public const int Active = 1;
                public const int Closed = 2;
            }
        }

        public static class Contact
        {
            public static class CustomerTypeCode
            {
                public const int NonMedical = 1;
                public const int Physician = 293360001;
                public const int VendorManufacturer = 293360002;
                public const int HospitalEmployee = 293360003;
                public const int DistributorRepresentative = 293360004;
                public const int Nurse = 293360005;
                public const int FacilityAdministrator = 293360007;
                public const int PracticeManager = 293360008;
            }

            public static class Salutation
            {
                public const int Dr = 293360000;
                public const int Mr = 293360001;
                public const int Ms = 293360002;
                public const int Mrs = 293360003;
                public const int Prof = 293360004;
            }
        }

        public static class Product
        {
            public static class ProductStructure
            {
                public const int Product = 1;
                public const int ProductFamily = 2;
                public const int ProductBundle = 3;
            }

            public static class ProductType
            {
                public const int StockAndBill = 1;
                public const int Consignment = 2;
                public const int Loaner = 293360000;
                public const int Services = 3;
                public const int FlatFees = 4;
                public const int SalesInventory = 100000000;
            }
        }

        public static class CaseProductConfig
        {
            public static class ProductType
            {
                public const int StockAndBill = 293360000;
                public const int Consignment = 293360001;
            }
        }
    }
}
