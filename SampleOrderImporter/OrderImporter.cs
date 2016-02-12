using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Asi.Soa.Commerce.DataContracts;
using Asi.Soa.Core.DataContracts;
using Asi.Soa.Membership.DataContracts;

namespace SampleOrderImporter
{
    public class OrderImporter :  ICommerceDataContractBuilder 
    {
        const int maxErrorsToShow = 10; // possibly could make this a configuration option
        #region column ordinal constants
        private const int flagColumn = 0;
        // order
        private const int partyIdColumn = 1;
        private const int firstNameColumn = 2;
        private const int lastNameColumn = 3;
        private const int emailColumn = 4;
        private const int phoneColumn = 5;
        private const int countryColumn = 6;
        private const int addressLine1Column = 7;
        private const int addressLine2Column = 8;
        private const int cityColumn = 9;
        private const int stateProvinceColumn = 10;
        private const int postalCodeColumn = 11;
        // order line
        private const int itemCodeColumn = 1;
        private const int quantityColumn = 2;
        private const int unitPriceColumn = 3;
        // product
        private const int productCodeColumn = 1;
        private const int productNameColumn = 2;
        private const int productDescriptionColumn = 3;
        private const int productClassNameColumn = 4;
        private const int productPriceColumn = 5;
        private const int productDiscountPriceColumn = 6;
        private const int productAccountingTypeColumn = 7;
        private const int productTaxCategoryColumn = 8;

        const int minimumSupportedColumnsForOrder = 12;
        const int minimumSupportedColumnsForLine = 4;
        const int minimumSupportedColumnsForProduct = 9;
        #endregion
        private StringBuilder messageBuilder;
        int numberOfErrors;
        List<Object> contractsToImport;
        public ICollection<Object> CreateImportCollection(string filePath)
        {
            messageBuilder = new StringBuilder();
            contractsToImport = new List<Object>();
            var numberOfOrders = 0;
            var lineNumber = 0;
            ObjectCount = 0;
            numberOfErrors = 0;
            ErrorLines = new Collection<String>();
            ComboOrderData comboOrder = null;
            OrderData order = null;
            foreach (var line in File.ReadLines(filePath))
            {
                lineNumber++;
                var pieces = new String[0];
                if (line != null)
                    pieces = line.Split(',');
                if (pieces.Length < 1)
                {
                    AddError(line, lineNumber, "Invalid line - too few columns");
                }
                else
                {
                    var lineType = pieces[flagColumn];
                    switch (lineType.ToUpperInvariant())
                    {
                        case "ORDER":
                            if (comboOrder != null)
                            {
                                // add previous comboOrder to the list
                                FinishComboOrder(comboOrder);
                            }
                            order = ProcessOrderImportRow(pieces, line, lineNumber);
                            comboOrder = new ComboOrderData {Order = order};
                            numberOfOrders++;
                            break;
                        case "ORDERLINE":
                            ProcessOrderLineImportRow(pieces, line, lineNumber, order);
                            break;
                        case "PRODUCT":
                            ProcessProductImportRow(pieces, line, lineNumber);
                            break;
                        default:
                            AddError(line, lineNumber, "Line does not start with ORDER, ORDERLINE, or PRODUCT");
                            break;
                    }
                }
            }
            if (comboOrder != null)
            {
                // add last combo order
                FinishComboOrder(comboOrder);
            }
            ObjectCount = numberOfOrders;
            if (messageBuilder.Length > 0)
            {
                if (numberOfErrors > maxErrorsToShow)
                    messageBuilder.AppendFormat("<li>Additional errors: {0}</li>", numberOfErrors - maxErrorsToShow);
                messageBuilder.Append("</ul>");
            }
            HasErrors = numberOfErrors > 0;
            ErrorMessage = messageBuilder.ToString();
            return contractsToImport;
        }

        private OrderData ProcessOrderImportRow(string[] pieces, string line, int lineNumber)
        {
            OrderData orderData = null;
            if (pieces.Length < minimumSupportedColumnsForOrder)
            {
                AddError(line, lineNumber, "Invalid line - too few columns for ORDER");
            }
            else
            {
                var partyId = pieces[partyIdColumn];
                var firstName = pieces[firstNameColumn];
                var lastName = pieces[lastNameColumn];
                var email = pieces[emailColumn];
                var phone = pieces[phoneColumn];
                var country = pieces[countryColumn];
                var line1 = pieces[addressLine1Column];
                var line2 = pieces[addressLine2Column];
                var city = pieces[cityColumn];
                var stateProvince = pieces[stateProvinceColumn];
                var postalCode = pieces[postalCodeColumn];
                
                PartyData party;
                var updateParty = false;
                AlternateIdData originatorId = null;
                if (String.IsNullOrEmpty(partyId))
                {
                    party = CreateNewParty(firstName, lastName, country, city, stateProvince, postalCode, email, phone, line1, line2); 
                    originatorId = new AlternateIdData("SourceId", Guid.NewGuid().ToString());
                    updateParty = true;
                }
                else
                    party = new PersonData {PartyId = partyId};

               var customerParty = new CustomerPartyData { Id = partyId, UpdateParty = updateParty, Party = party, OriginatorCustomerId = originatorId};
               var deliveryData = new DeliveryData
               {
                   DeliveryMethod = new DeliveryMethodData { DeliveryMethodId = "USPS" },
                   Address = party.Addresses[0],
                   CustomerParty = customerParty 
               };
                orderData = new OrderData
                {
                    BillToCustomerParty = customerParty,
                    SoldToCustomerParty = customerParty,
                    Currency = CommerceSettings.DefaultCurrency,
                    Lines = new OrderLineDataCollection(),
                    OrderReference = new OrderReferenceData(),
                    Delivery = new DeliveryDataCollection { deliveryData }
                };

            }
            return orderData;
        }

        private void ProcessOrderLineImportRow(string[] pieces, string line, int lineNumber, OrderData order)
        {
            var processLine = true;
            if (order == null)
            {
                AddError(line, lineNumber, "No existing Order for line.");
                processLine = false;
            }
            if (pieces.Length < minimumSupportedColumnsForLine)
            {
                AddError(line, lineNumber, "Invalid line - too few columns for LINE");
                processLine = false;
            }

            var productCode = pieces[itemCodeColumn];
            int quantity;
            processLine &= SetIntegerFromString(line, lineNumber, pieces[quantityColumn], out quantity);
            decimal unitPrice;
            processLine &= SetDecimalFromString(line, lineNumber, pieces[unitPriceColumn], out unitPrice);
            if (processLine)
            {

                var orderLine = new OrderLineData
                {
                    Item = new ProductItemData {ItemCode = productCode},
                    QuantityOrdered = new QuantityData(quantity),
                    UnitPrice = new MonetaryAmountData(unitPrice, CommerceSettings.DefaultCurrency),
                    ExtendedAmount = new MonetaryAmountData(unitPrice*quantity, CommerceSettings.DefaultCurrency)
                };
                order.Lines.Add(orderLine);
            }
        }

        private void ProcessProductImportRow(string[] pieces, string line, int lineNumber)
        {
            var processLine = true;
            if (pieces.Length < minimumSupportedColumnsForProduct)
            {
                AddError(line, lineNumber, "Invalid line - too few columns for PRODUCT");
                processLine = false;
            }
            var productCode = pieces[productCodeColumn];
            var productName = pieces[productNameColumn];
            var productDescription = pieces[productDescriptionColumn];
            var productClass = pieces[productClassNameColumn];
            var productAccountingMethod = pieces[productAccountingTypeColumn];

            AccountingMethodData accountingMethod;
            if (!Enum.TryParse(productAccountingMethod, true, out accountingMethod))
            {
                AddError(line, lineNumber, String.Format("Invalid accounting method: {0}", pieces[productAccountingTypeColumn]));
                processLine = false;
            }
            var productTaxCategory = pieces[productTaxCategoryColumn];

            decimal price;
            decimal discountPrice;
            processLine &= SetDecimalFromString(line, lineNumber, pieces[productPriceColumn], out price);
            processLine &= SetDecimalFromString(line, lineNumber, pieces[productDiscountPriceColumn], out discountPrice);
            if (processLine)
            {
                var product = new ProductItemData
                {
                    Description = productDescription,
                    ItemCode = productCode,
                    Name = productName,
                    ItemId = productCode,
                    ItemClass = new ItemClassSummaryData {Name = productClass, ItemClassId = productClass},
                    TempDefaultPrice = price,
                    ItemFinancialInformation =
                        new ItemFinancialInformationData
                        {
                            AccountingMethod = accountingMethod,
                            TaxCategory = new TaxCategorySummaryData {Name = productTaxCategory}
                        }
                };
                contractsToImport.Add(product);
                var standardPriceData = new ItemPriceData
                {
                    PriceSheet = new PriceSheetSummaryData{PriceSheetId = CommerceSettings.DefaultPriceSheetId},
                    Item = product,
                    DefaultPrice = new MonetaryAmountData(price, CommerceSettings.DefaultCurrency)
                };
                var discountPriceData = new ItemPriceData
                {
                    PriceSheet = new PriceSheetSummaryData { PriceSheetId = CommerceSettings.DefaultDiscountPriceSheetId },
                    Item = product,
                    DefaultPrice = new MonetaryAmountData(discountPrice, CommerceSettings.DefaultCurrency)
                };
                contractsToImport.Add(standardPriceData);
                contractsToImport.Add(discountPriceData);
            }
        }

        private void FinishComboOrder(ComboOrderData comboOrder)
        {
            var total = 0m;
            foreach (var line in comboOrder.Order.Lines)
            {
                if (line.ExtendedAmount.HasValue)
                    total += line.ExtendedAmount.Value.Amount;
            }
            comboOrder.Currency = CommerceSettings.DefaultCurrency;
            comboOrder.Payments = new RemittanceDataCollection
            {
                new RemittanceData
                {
                    Amount = new MonetaryAmountData(total, CommerceSettings.DefaultCurrency),
                    PaymentMethod = new PaymentMethodData {PaymentMethodId = "CASH"},
                    PayorParty = comboOrder.Order.BillToCustomerParty
                }

            };
            contractsToImport.Add(comboOrder);
        }
        private bool SetDecimalFromString(string line, int lineNumber, string trialValue, out decimal price)
        {
            var processLine = true;
            if (!Decimal.TryParse(trialValue, out price))
            {
                AddError(line, lineNumber, String.Format("Invalid amount: {0}", trialValue));
                processLine = false;
            }
            else if (price <= 0m)
            {
                AddError(line, lineNumber, String.Format("Invalid amount: {0}", trialValue));
                processLine = false;
            }
            return processLine;
        }
        private bool SetIntegerFromString(string line, int lineNumber, string trialValue, out int amount)
        {
            var processLine = true;
            if (!Int32.TryParse(trialValue, out amount))
            {
                AddError(line, lineNumber, String.Format("Invalid amount: {0}", trialValue));
                processLine = false;
            }
            else if (amount <= 0m)
            {
                AddError(line, lineNumber, String.Format("Invalid amount: {0}", trialValue));
                processLine = false;
            }
            return processLine;
        }
        private void AddError(string line, int lineNumber, string msg)
        {
            numberOfErrors++;
            ErrorLines.Add(line);
            if (numberOfErrors <= maxErrorsToShow)
            {
                if (messageBuilder.Length <= 0)
                    messageBuilder.Append("<ul>");
                messageBuilder.AppendFormat("<li>Line {0}; {1}</li>", lineNumber, msg);
            }
        }
        private PartyData CreateNewParty(string firstName, string lastName, string country, string city, 
            string stateProvince, string postalCode, string email, string phone,
            string address1, string address2)
        {
            var person = new PersonData
            {
                PersonName = new PersonNameData { FirstName = firstName, LastName = lastName },
                Addresses = new FullAddressDataCollection()
            };

                var address = new FullAddressData
                {
                    Address =
                        new AddressData
                        {
                            AddressLines = new AddressLineDataCollection(),
                            CountryCode = country,
                            CityName = city,
                            CountrySubEntityCode = stateProvince,
                            PostalCode = postalCode
                        },
                    Email = email,
                    Phone = phone,
                    AddressPurpose =  "Address"
                };
                address.Address.AddressLines.Add(address1);
                address.Address.AddressLines.Add(address2);
                person.Addresses.Add(address);
        
            return person;
        }
        public string Name
        {
            get { return "Order import"; }
        }
        public string ErrorMessage { get; private set; }
        public bool HasErrors { get; private set; }
        public int ObjectCount { get; private set; }
        public string DisplayNameOfObjects
        {
            get { return "Order"; }
        }
        public Collection<string> ErrorLines { get; private set; }
        public CommerceSettingsData CommerceSettings { get; set; }
    }
}
