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

        public static class Account
        {
            public static class SalesCategory
            {
                public const int Biologics = 100000001;
                public const int Orthopedics = 100000003;
                public const int Spine = 100000004;
                public const int Woundcare = 100000002;
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

        public static class CaseProduct
        {
            public static class ProductType
            {
                public const int StockAndBill = 293360000;
                public const int Consignment = 293360001;
            }
        }

        public static class SalesRepContract
        {
            public static class ContractType
            {
                public const int ExternalSalesRep = 293360002;
                public const int SalesRep = 293360000;
                public const int Distributor = 293360001;
            }

            public static class RepRole
            {
                public const int SR = 293360000;
                public const int CSS = 293360001;
                public const int ASR = 293360002;
                public const int VP = 293360004;
                public const int Manager = 293360003;
            }
        }

        public static class SalesRepCommissionRule
        {
            public static class CommissionType
            {
                public const int Standard = 293360000;
                public const int Override = 293360001;
                public const int Standalone = 293360002;
                public const int VPStandard = 293360003;
                public const int ManagerStandard = 293360004;
            }

            public static class SalesCategory
            {
                public const int Biologics = 100000001;
                public const int Orthopedics = 100000003;
                public const int Spine = 100000004;
                public const int Woundcare = 100000002;
            }
        }
    }
}
