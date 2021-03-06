using System;
using System.Collections.Generic;
using System.Linq;
using Vera.Models;

namespace Vera.Norway.Audit
{
    public enum BasicTypes
    {
        Cost = 1,
        Product = 2,
        Project = 3,
        ArticleGroup = 4,
        TicketLine = 5,
        Logging = 6,
        Saving = 7,
        Discount = 8,
        Quantity = 9,
        Raise = 10,
        Transaction = 11,
        Payment = 12,
        Event = 13,
        Service = 14,
        User = 15,
        Other = 16
    }

    public static class PredefinedEventBasics
    {
        public const int ApplicationStart = 13001;
        public const int EmployeeLogin = 13003;
        public const int EmployeeLogout = 13004;
        public const int OpenCashDrawer = 13005;
        public const int CloseCashDrawer = 13006;
        public const int ApplicationUpdate = 13007;
        public const int XReport = 13008;
        public const int ZReport = 13009;
        public const int SalesReceipt = 13012;
        public const int CopyReceipt = 13014;
        public const int Other = 13999;

        // 13001	POS application start	Oppstart av POS applikasjon
        // 13002	POS application shut down	Avslutning av POS applikasjon
        // 13003	Employee log in	Pålogging bruker/ansatt
        // 13004	Employee log out	Avlogging bruker/ansatt
        // 13005	Open cash drawer	Kasseskuff åpning
        // 13006	Close cash drawer	Kasseskuff lukking
        // 13007	Update of POS application	Oppdatering av programvare
        // 13008	X report	Kjøring av X-rapport
        // 13009	Z report	Kjøring av Z-rapport
        // 13010	Suspend transaction	Parkering av kvittering/bong
        // 13011	Resume transaction	Tilbakehenting av parkert kvittering/bong
        // 13012	Sales receipt	Salgskvittering
        // 13013	Return receipt	Returkvittering
        // 13014	Copy receipt	Kopi av kvittering
        // 13015	Pro forma receipt	Proforma kvittering
        // 13016	Delivery receipt	Utleveringskvittering ved kredittsalg
        // 13017	Training receipt 	Treningsmodus - alle typer kvitteringer
        // 13018	Other reports or receipts	Andre rapporter eller kvitteringer
        // 13019	Cash withdrawal	Uttak av kontanter fra kassa
        // 13020	Export of journal to external storage (cash register)	Overføring av journal til annet lagringsmedie (ROM kasser)
        // 13021	Price change	Prisendringer
        // 13022	Price look-up 	Prisundersøkelse
        // 13023	Training mode on 	Trenings- modus/funksjon på
        // 13024	Training mode off	Trenings- modus/funksjon av
        // 13025	Memory full (cash register)	Fullt minne (ROM kasser)
        // 13026	Emergency mode on	Nødmodus på
        // 13027	Emergency mode off	Nødmodus av
        // 13028	Void receipt	Annulleringskvittering
        // 13999	Other	Øvrige hendelser
    }

    public static class PredefinedProductBasics
    {
        public const int Other = 4999;

        //    4001	Withdrawal of treatment services
        //    4002	Withdrawal of goods used for treatment services
        //    4003	Sale of goods
        //    4004	Sale of treatment services
        //    4005	Sale of haircut
        //    4006	Food
        //    4007	Beer
        //    4008	Wine
        //    4009	Liquor
        //    4010	Alcopops/Cider
        //    4011	Soft drinks/Mineral water
        //    4012	Other drinks (tea, coffe etc)
        //    4013	Tobacco
        //    4014	Other goods
        //    4015	Entrance fee (cover charge)
        //    4016	Free entrance (members etc)
        //    4017	Cloakroom fee
        //    4018	Free cloakroom
        //    4019	Accomodation - full board
        //    4020	Accomodation - half board
        //    4021	Accomodation - with breakfast
    }

    public static class BasicsMapper
    {
        public static AuditfileCompanyBasicsBasic FromEvent(EventLog e)
        {
            return new AuditfileCompanyBasicsBasic
            {
                BasicType = FormatBasicType(BasicTypes.Event),
                PredefinedBasicID = TranslateEventToBasicID(e.Type).ToString(),
                BasicID = e.Type.ToString().ToUpper(),
                BasicDesc = GetEventDescription(e.Type)
            };
        }

        public static AuditfileCompanyBasicsBasic FromPayment(Payment p)
        {
            return new AuditfileCompanyBasicsBasic
            {
                BasicType = FormatBasicType(BasicTypes.Payment),
                PredefinedBasicID = TranslatePaymentToBasicID(p.Category),
                BasicID = p.Category.ToString(),
                BasicDesc = p.Description
            };
        }

        public static AuditfileCompanyBasicsBasic FromProduct(Product p)
        {
            return new AuditfileCompanyBasicsBasic
            {
                BasicType = FormatBasicType(BasicTypes.Product),
                PredefinedBasicID = TranslateProductToBasicID(p),
                BasicID = p.Code,
                BasicDesc = p.Description
            };
        }

        public static AuditfileCompanyBasicsBasic FromDiscount(Settlement discount)
        {
            return new AuditfileCompanyBasicsBasic
            {
                BasicType = FormatBasicType(BasicTypes.Discount),
                BasicID = discount.SystemId,
                BasicDesc = discount.Description
            };
        }

        public static AuditfileCompanyBasicsBasic FromInvoice(Invoice invoice, IEnumerable<Payment> payments)
        {
            var basic = new AuditfileCompanyBasicsBasic
            {
                BasicType = FormatBasicType(BasicTypes.Transaction),
                BasicID = "OTHER",

                // Default to other
                BasicDesc = "OTHER",
                PredefinedBasicID = "11999"
            };

            if (invoice.Totals.Gross < 0)
            {
                // Return when receiving goods transaction
                basic.BasicDesc = "RETURN";
                basic.BasicID = "RETURN";
                basic.PredefinedBasicID = "11006";
            }
            else if (payments.Any(p => p.Category == PaymentCategory.Cash))
            {
                // Cash transaction
                basic.BasicDesc = "CASH";
                basic.BasicID = "CASH";
                basic.PredefinedBasicID = "11001";
            }

            // TODO (kevin) support more complex scenarios?
            // 11001	Cash sale
            // 11002	Credit sale
            // 11003	Purchase
            // 11004	Payment
            // 11005	Receiving payment
            // 11006	Return payment
            // 11007	Cash declaration
            // 11008	Cash difference
            // 11009	Correction
            // 11010	Out Payment
            // 11011	In Payment
            // 11012	Trade-in, exchange
            // 11013	Return products
            // 11014	Inventory, stock
            // 11015	Cash and credit sale
            // 11016	Cash sale and return
            // 11017	Credit sale and return
            // 11999	Other

            return basic;
        }

        public static string GetEventDescription(EventLogType eventLogType) =>
            eventLogType switch
            {
                EventLogType.AppStart => "App was started",
                EventLogType.Login => "User has logged in",
                EventLogType.Logout => "User has logged out",
                EventLogType.OpenCashDrawer => "Cash drawer was opened",
                EventLogType.CloseCashDrawer => "Cash drawer was closed",
                EventLogType.CurrentRegisterReportCreated => "XReport generated",
                EventLogType.EndOfDayRegisterReportCreated => "ZReport generated",
                EventLogType.ReceiptPrinted => "Receipt was printed",
                EventLogType.ReceiptReprinted => "Receipt was reprinted",
                _ => throw new NotSupportedException($"{eventLogType} not supported")
            };

        public static int TranslateEventToBasicID(EventLogType eventType) =>
            eventType switch
            {
                EventLogType.AppStart => PredefinedEventBasics.ApplicationStart,
                EventLogType.Login => PredefinedEventBasics.EmployeeLogin,
                EventLogType.Logout => PredefinedEventBasics.EmployeeLogout,
                EventLogType.OpenCashDrawer => PredefinedEventBasics.OpenCashDrawer,
                EventLogType.CloseCashDrawer => PredefinedEventBasics.CloseCashDrawer,
                EventLogType.CurrentRegisterReportCreated => PredefinedEventBasics.XReport,
                EventLogType.EndOfDayRegisterReportCreated => PredefinedEventBasics.ZReport,
                EventLogType.ReceiptPrinted => PredefinedEventBasics.SalesReceipt,
                EventLogType.ReceiptReprinted => PredefinedEventBasics.CopyReceipt,
                _ => PredefinedEventBasics.Other

                // TODO (kevin) receipt events
                //          13013	Return receipt\
                //          13014	Copy receipt
                //          13015	Pro forma receipt
                //          13016	Delivery receipt
            };

        public static string TranslatePaymentToBasicID(PaymentCategory paymentCategory) =>
            paymentCategory switch
            {
                PaymentCategory.Cash => "12001",
                PaymentCategory.Debit => "12002",
                PaymentCategory.Credit => "12003",
                _ => "12999"

                // TODO (kevin) map these
                //          12004	Bank account
                //          12005	Gift token
                //          12006	Customer card
                //          12007	Loyalty, stamps
                //          12008	Bottle deposit
                //          12009	Check
                //          12010	Credit note
                //          12011	Mobile phone apps
            };

        private static string TranslateProductToBasicID(Product p)
        {
            return FormatPredefinedBasicID(PredefinedProductBasics.Other);
        }

        public static string FormatPredefinedBasicID(int basicID) => basicID.ToString().PadLeft(5, '0');

        public static string FormatBasicType(BasicTypes t) => ((int)t).ToString().PadLeft(2, '0');
    }
}