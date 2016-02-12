This is a sample prepared for the presentation "SOA: The Swiss Army Knife of iMIS" given at the iNNOVATIONS 2014 conference in May, 2014.

It is meant to be used with the File Importer Dynamic Content Item, or iPart.  The input file expected by the plug is comma delimited 
with three possible types of lines:

1)  Product,product code, product name, product desctription, product type, standard price, discount price, accounting treatment, tax category
2)  Order, iMIS id, first name, last name, email, phone, country, address line 1, address line 2, city, state/province, postal code
3)  OrderLine, product code, quantity, price

In each case the first column needs to be one of the strings "Product," "Order," or "OrderLine."  The lines are processed in order, so any 
products used in orders must appear before the order.  OrderLines are assumed to belong to the previous order until a new Order record
is encountered.

An example file is this:

Product,P1,P1 Name,Description of P1,SALES-PUB,37.00,25.00,Accrual,Non-Taxable
Order,,Beau,Zeau,bozo@clown.org,512-555-1212,,1701 Bridgeway Drive,,Austin,TX,78704
OrderLine,P1,1,37.00

This is a simple example that assumes that no tax, shipping, or handling charges will be added to the order.

The references to iMIS libraries use the path: C:\Program Files (x86)\ASI\iMIS15\Net\Bin\
You will need to change that path if it is not appropriate for your environment.

The library created by this project needs to be copied to the bin directory, and the web.config file needs to updated in order for
the library to be used by the File Importer iPart.  The line that needs to be added is 

    <importedDataContractBuilder type="SampleOrderImporter.OrderImporter, SampleOrderImporter" />

This goes in the iMIS/Soa/importedDataContractBuilders section of the web.config file.