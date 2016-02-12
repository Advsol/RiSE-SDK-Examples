using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Asi.Soa.ClientServices;
using Asi.Soa.Commerce.DataContracts;
using Asi.Soa.Core.DataContracts;
using Asi.Soa.Fundraising.DataContracts;
using Asi.Soa.Membership.DataContracts;

namespace ImportBatchCreator
{
    /// <summary>
    /// A simple console app that accepts the name of a comma-delimited file and creates an ImportBatch of donation combo orders
    /// based on information in the file.  The comma-delimited file contains only three columns:
    /// 1) iMIS ID of the donor
    /// 2) amount of the donation
    /// 3) name of the fund or product
    /// Many simplifying assumptions are made:
    ///  - syntax of file is correct
    ///  - contact exists in the database
    ///  - fund exists in the database
    ///  - currency is USD
    ///  - payment method is CASH
    ///  - userid/pw hard-coded
    /// </summary>
    class ImportBatchCreator
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Syntax: ImportBatchCreator filepath");
                return;
            }
            var filePath = args[0];
            var em = new EntityManager("RichardK", "RichardK");
            var objects = new List<Object>();
            foreach (var line in File.ReadLines(filePath))
            {
                var pieces = new String[0];
                if (line != null)
                    pieces = line.Split(',');
                if (pieces.Length < 3)
                {
                    Console.WriteLine("Line not valid: " + line);
                    return;
                }
                var comboOrder = BuildComboOrder(pieces[0], Decimal.Parse(pieces[1]), pieces[2]);
                objects.Add(comboOrder);
            }

            var batchName = String.Format(CultureInfo.CurrentCulture, "Import batch {0}.xml", DateTime.Now.ToString("s", CultureInfo.CurrentCulture).Replace(':', '-'));
            var batch = new ImportBatchData { Batch = objects, Name = batchName };
            var addResults = em.Add(batch);
            if (!addResults.IsValid)
                Console.WriteLine("Results not valid: " + addResults.ValidationResults.Summary);
            else
                Console.WriteLine("Import batch created.");
        }

        private static ComboOrderData BuildComboOrder(string partyId, decimal amount, string fund)
        {
            var usd = new CurrencyData("USD");
            var party = new PersonData { PartyId = partyId };
            var customerParty = new CustomerPartyData { Id = partyId, Party = party };
            var orderData = new OrderData
            {
                BillToCustomerParty = customerParty,
                SoldToCustomerParty = customerParty,
                Currency = usd,
                Lines = new OrderLineDataCollection{
                    new OrderLineData
                    {
                        Item = new GiftItemData {ItemCode = fund},
                        QuantityOrdered = new QuantityData(1),
                        UnitPrice = new MonetaryAmountData(amount, usd),
                        ExtendedAmount = new MonetaryAmountData(amount, usd)
                    }
                },
            };
            var comboOrder = new ComboOrderData
            {
                Currency = usd,
                Order = orderData
            };
            comboOrder.Payments = new RemittanceDataCollection
            {
                new RemittanceData
                {
                    Amount = new MonetaryAmountData(amount, usd),
                    PaymentMethod = new PaymentMethodData {PaymentMethodId = "CASH"},
                    PayorParty = new CustomerPartyData {Id = partyId, Party = party}
                }
            };
            return comboOrder;
        }
    }
}
