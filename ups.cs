using System;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Web.Http;
using WBGLibrary;
using WBGLibrary.UPSShip;
using Enterprise.Business.OrderEntryBL;
using System.IO;
using System.Collections.Generic;
using System.Web.Services.Protocols;
using Enterprise.DataServer;
using System.Text;

namespace WBG_EPMSAPI.Controllers
{
    [System.Web.Http.RoutePrefix("api/UPS")]
    public class UPSController : ApiController
    {
        string connName = @"connection string here";
        [System.Web.Http.Route("Ship")]
        [System.Web.Http.HttpPost]
        public WBGLibrary.Response<JobShipment> Ship([FromBody]WBGLibrary.GenericClass<JobShipment> GC)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
            JobShipment shipment = GC.Model;
            string upsAccount = "";
            if (shipment.Location == "Burnside")
                upsAccount = "accountNum";
            if (shipment.Location == "Kent")
                upsAccount = "accountNum";
            if (shipment.Location == "Portland Warehouse")
                upsAccount = "accountNum";
            if (shipment.Location == "Corporate")
                upsAccount = "accountNum";
            if (shipment.Location == "Chino")
                upsAccount = "accountNum";
            if (shipment.Location == "Imaging")
                upsAccount = "accountNum";
            shipment.errorText = new List<string>();            
            WBGLibrary.Response<JobShipment> response = new WBGLibrary.Response<JobShipment>();
            WBGLibrary.UPSRate.RateResponse rate = new WBGLibrary.UPSRate.RateResponse();
            if (shipment.PortalId == "some company")
            {
                UPSRateController upsRate = new UPSRateController();
                rate = upsRate.GetRate(shipment);
                cJobHeader header = new cJobHeader();
                header.Load("ORDER", shipment.JobNumber);
                AuthNetCharge charge = new AuthNetCharge();
                decimal amountToCharge = 0;
                if(rate.RatedShipment[0].TotalChargesWithTaxes != null)                
                    amountToCharge = Convert.ToDecimal(rate.RatedShipment[0].TotalChargesWithTaxes.MonetaryValue) + header.GetJobTotalPrice();
                else                
                    amountToCharge = Convert.ToDecimal(rate.RatedShipment[0].TotalCharges.MonetaryValue) + header.GetJobTotalPrice();
                charge.Amount = amountToCharge;
                charge.BankTag = header.JobDetailDescription;
                charge.EPMSJobNumber = header.JobNumber;
                charge.PageDNAJobNumber = header.JobDescription;
                charge.ChargeCustomerProfile();
                if (charge.ErrorMessage != "" && charge.ErrorMessage != null)
                {
                    shipment.errorText.Add("There was an error processing the credit card for job number " + shipment.JobNumber + ", shipping labels will not be produced until the credit card is approved. Please contact the CSR and try again later.");
                    response.ReturnedResult = shipment;
                    response.Successful = true;
                }                    
            }
            if (shipment.errorText.Count == 0)
            {
                ShipService shipService = new ShipService();
                shipService.UPSSecurityValue = GetSecurityCredentials();
                ShipmentRequest shipmentRequest = MapJobShipmentToShipmentRequest(shipment, upsAccount);
                try
                {
                    ShipmentResponse shipmentResponse = shipService.ProcessShipment(shipmentRequest);
                    UPSShipment upsShipment = SetUPSShipment(shipmentRequest, shipmentResponse, shipment, rate, GC.myLogin.ObjID);
                    upsShipment.Packages = SetUPSPackages(shipmentRequest, shipmentResponse, upsShipment, shipment);
                    JobsController.CloseOutJob(shipment.JobNumber);
                    shipment.MasterTrackingNumber = shipmentResponse.ShipmentResults.ShipmentIdentificationNumber;
                    response.ReturnedResult = shipment;
                    response.Successful = true;
                }
                catch (SoapException ex)
                {
                    shipment.errorText.Add(ex.Detail.InnerText);
                    response.ReturnedResult = shipment;
                    response.Successful = true;
                }
            }
            return response;
        }
        [System.Web.Http.Route("GetShipmentsByJobID")]
        [System.Web.Http.HttpPost]
        public ResponseList<JobShipment> GetShipmentsByJobID([FromBody]GenericClass<JobShipment> GC)
        {
            var response = new ResponseList<JobShipment>();
            string jobNum = GC.Model.JobNumber.Trim();
            cJobHeader jobHeader = new cJobHeader();
            jobHeader.Load("ORDER", jobNum);
            cJobComponent comp = new cJobComponent();
            comp.Load("ORDER", jobNum, 1);
            if (String.IsNullOrEmpty(jobHeader.JobNumber))
            {
                response.Successful = false;
                return response;
            }
            cShipments shipments = new cShipments();
            shipments.Load(jobNum, false);
            if (shipments.Count < 1)
            {
                response.Successful = false;
                return response;
            }
            foreach (cShipment shipment in shipments)
            {
                string returnName = "";
                string returnCompany = "";
                string returnAddress = "";
                string returnCity = "";
                string returnState = "";
                string returnZip = "";
                string returnPhone = "";
                if(shipment.UserDefined3 != "")
                {
                    string[] csz = shipment.UserDefined3.Split(',');
                    try
                    {
                        returnCompany = shipment.UserDefined1;
                        returnName = shipment.UserDefined4;
                        returnAddress = shipment.UserDefined2;
                        returnPhone = shipment.UserDefined5;
                        returnCity = csz[0];
                        returnState = csz[1];
                        returnZip = csz[2];       
                        if(returnPhone == "")
                        {
                            returnPhone = "8005478397";
                        }                 
                    }
                    catch (Exception ex)
                    {
                        //Maybe add an error here or at least log.
                        Console.WriteLine(ex.Message);
                    }
                } else
                {
                    returnCompany = "Shipping";
                    if (GC.Model.Location == "Burnside")
                    {
                        returnAddress = "Default Address";
                        returnCity = "Default City";
                        returnState = "Default State";
                        returnZip = "Default Zip";
                        returnPhone = "Default Phone";
                    }
                    if (GC.Model.Location == "Kent")
                    {
                        returnAddress = "Default Address";
                        returnCity = "Default City";
                        returnState = "Default State";
                        returnZip = "Default Zip";
                        returnPhone = "Default Phone";
                    }
                    if (GC.Model.Location == "Portland Warehouse")
                    {
                        returnAddress = "Default Address";
                        returnCity = "Default City";
                        returnState = "Default State";
                        returnZip = "Default Zip";
                        returnPhone = "Default Phone";
                    }
                    if (GC.Model.Location == "Corporate")
                    {
                        returnName = "Default Name";
                        returnAddress = "Default Address";
                        returnCity = "Default City";
                        returnState = "Default State";
                        returnZip = "Default Zip";
                        returnPhone = "Default Phone";
                    }
                    if (GC.Model.Location == "Chino")
                    {
                        returnAddress = "Default Address";
                        returnCity = "Default City";
                        returnState = "Default State";
                        returnZip = "Default Zip";
                        returnPhone = "Default Phone";
                    }
                }                
                JobShipment job = new JobShipment()
                {
                    Location = GC.Model.Location,
                    JobNumber = GC.Model.JobNumber,
                    epmsCustomerNumber = jobHeader.CustAccount,
                    ShipmentNumber = shipment.ShipmentNumber.ToString(),
                    ShipInNameOf = shipment.ShipInNameOf.Trim(),
                    ShipName = shipment.ShipName.Trim(),
                    ShipAddress1 = shipment.ShipAddress1.Trim(),
                    ShipAddress2 = shipment.ShipAddress2.Trim(),
                    ShipCity = shipment.ShipCity.Trim(),
                    ShipState = shipment.ShipState.Trim(),
                    ShipZip = shipment.ShipZip.Trim(),
                    ShipCountry = shipment.ShipCountry.Trim() == "" ? "US" : shipment.ShipCountry.Trim(),
                    ShipPhone = shipment.ShipPhone.Trim() == "" ? "0000000000" : shipment.ShipPhone.Trim(),
                    ShipEmail = shipment.Email.Trim(),
                    ServiceLevel = GetServiceLevel(shipment.ShipViaService),
                    ThirdPartyBilling = shipment.ThirdPartyBilling.Trim(),
                    CarrierID = shipment.ShipVia.Trim(),
                    PONumber = jobHeader.PONumber.Trim(),
                    PortalId = comp.UserDefined2.Trim(),
                    ReturnName = returnName.Trim(),
                    ReturnCompany = returnCompany.Trim(),
                    ReturnAddress1 = returnAddress.Trim(),                    
                    ReturnCity = returnCity.Trim(),
                    ReturnState = returnState.Trim(),
                    ReturnZip = returnZip.Trim(),
                    ReturnPhone = returnPhone.Trim(),
                    ReturnCountry = "US",                    
                    BillAccountZip = shipment.UserDefined6.Trim()
                };

                job.Cartons = new List<JobCarton>();

                if (shipment.Shipped)
                {
                    foreach (cPackage pkg in shipment.objPackages)
                    {
                        JobCarton carton = new JobCarton();
                        carton.TrackingNumber = pkg.TrackingNumber;
                        job.Cartons.Add(carton);
                    }

                    job.ShippingStatus = "Shipped";
                }

                if (shipment.Shipped) job.ShippingStatus = "Shipped";
                response.ReturnedResults.Add(job);
            }
            response.Successful = true;
            return response;
        }
        [System.Web.Http.Route("GetTodaysPackages")]
        [System.Web.Http.HttpPost]
        public ResponseList<UPSPackage> GetTodaysPackages()
        {
            var response = new ResponseList<UPSPackage>();
            using (SqlConnection Connection = new SqlConnection(connName))
            {
                string sqlQuery = "select UPS_Packages.Id, UPS_Shipments.Id AS Expr1, UPS_Packages.ShipmentId, UPS_Packages.TrackingNumber, UPS_Packages.ShipTime, UPS_Packages.Status, UPS_Packages.Weight, UPS_Shipments.EPMSOrderNumber FROM UPS_Packages INNER JOIN UPS_Shipments ON UPS_Packages.ShipmentId = UPS_Shipments.Id where ups_packages.ShipTime > DateAdd(hh, -24, GETDATE())";
                using (SqlCommand Command = new SqlCommand(sqlQuery, Connection))
                {
                    Connection.Open();
                    using (SqlDataReader rdr = Command.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            UPSPackage package = new UPSPackage();
                            package.Id = Convert.ToInt32(rdr["Id"].ToString());
                            package.ShipmentId = Convert.ToInt32(rdr["ShipmentId"].ToString());
                            package.TrackingNumber = rdr["TrackingNumber"].ToString();
                            package.ShipTime = Convert.ToDateTime(rdr["ShipTime"].ToString());
                            package.Status = rdr["Status"].ToString();
                            package.Weight = (float)Convert.ToDouble(rdr["Weight"].ToString());
                            package.JobNumber = rdr["EPMSOrderNumber"].ToString();

                            response.ReturnedResults.Add(package);
                        }
                    }
                    Connection.Close();
                }
            }
            response.Successful = true;
            return response;
        }
        private ShipmentRequest MapJobShipmentToShipmentRequest(JobShipment shipment, string upsAccount)
        {
            ShipmentRequest request = new ShipmentRequest();
            request.Request = SetRequest();
            request.Shipment = SetShipment(shipment, upsAccount);
            request.LabelSpecification = SetLabelSpecification(shipment);

            //We currently do not print receipts, commenting this out. Apparently we cannot use this unless PRL or Exchange Return Receipt
            //request.ReceiptSpecification = SetReceiptSpecification(shipment); 

            return request;
        }
        private RequestType SetRequest()
        {
            RequestType request = new RequestType();
            request.RequestOption = new string[1];
            request.RequestOption[0] = "validate";
            request.SubVersion = "1601";
            //This is optional, indicates which version of the api to use. Do not enter unless an old version is needed. 
            //request.SubVersion = "";
            //This is to pass cutomer related info and have it come back with the request. It is optional and unneeded at this time.
            //request.TransactionReference = new TransactionReferenceType();
            //request.TransactionReference.CustomerContext = "";
            return request;
        }
        private ShipmentType SetShipment(JobShipment jobShipment, string upsAccount)
        {
            ShipmentType shipment = new ShipmentType();
            //This is only required for international shipments.
            //shipment.Description = "";
            //This is not required. We currently do not use return services.
            //shipment.ReturnService = new ReturnServiceType();
            //shipment.ReturnService.Code = "";
            //shipment.ReturnService.Description = "";
            //This is only required for international shipments and will never be used for our purposes.
            //shipment.DocumentsOnlyIndicator = "";

            //Required for every shipment
            shipment.Shipper = SetShipper(jobShipment, upsAccount);
            //Required for every shipment
            shipment.ShipTo = SetShipTo(jobShipment);

            //This is only required for if ShipmentIndicationType is 01 or 02. Not used for our purposes right now.
            //shipment.AlternateDeliveryAddress = SetAlternateDeliveryAddress();

            //Required for every shipment
            shipment.ShipFrom = SetShipFrom(jobShipment);

            //This is only required for all non freight shipments.
            shipment.PaymentInformation = SetPaymentInformation(jobShipment, upsAccount);

            //This is only required for freight shipments. Currently this is not used
            //shipment.FRSPaymentInformation = SetFRSPaymentInformation(jobShipment);
            //This is only required for freight shipments
            //shipment.FreightShipmentInformation = SetFreightShipmentInformation(jobShipment);
            //This is not required and I have no idea what it means
            //shipment.GoodsNotInFreeCirculationIndicator = "";
            //This is not required and I am pretty sure it will not be used.
            //shipment.PromotionalDiscountInformation = new PromotionalDiscountInformationType();
            //shipment.PromotionalDiscountInformation.PromoCode = "";
            //shipment.PromotionalDiscountInformation.PromoAliasCode = "";

            //This is not required, but good to get appropriate rates where possible.
            shipment.ShipmentRatingOptions = SetShipmentRatingOptions(jobShipment);

            //Not used and not required.
            //shipment.MovementReferenceNumber = "";
            //This can only be used if the shipment is not US to US or PR to PR, otherwise, needs to be in package level.
            //if(jobShipment.InvoiceNumber != null || jobShipment.PONumber != null)
            //{
            //    shipment.ReferenceNumber = SetReferenceNumbers(jobShipment);
            //}           
            shipment.Service = SetService(jobShipment);

            //This is not required and not needed. 
            //shipment.InvoiceLineTotal = SetInvoiceLineTotal(jobShipment);
            //This is only required for freight.
            //shipment.NumOfPiecesInShipment = jobShipment.NumberOfCartons.ToString();
            //I don't know what this means and I don't think we will be using it.
            //shipment.USPSEndorsement = "";
            //This is only used for certain international shipments.
            //shipment.MILabelCN22Indicator = "";
            //Not required and I don't think we will be using it.
            //shipment.SubClassification = "";
            //This may be used in the future, but for now cost centers should be provided in the reference fields.
            //shipment.CostCenter = "";
            //Not required, not used.
            //shipment.PackageID = "";
            //Not required and not used.
            //shipment.IrregularIndicator = "";
            //Not required and will not be used.
            //shipment.ShipmentIndicationType = new IndicationType[1];
            //shipment.ShipmentIndicationType[0] = new IndicationType();
            //shipment.ShipmentIndicationType[0].Code = "";
            //shipment.ShipmentIndicationType[0].Description = "";
            //Not required, not used.
            //shipment.MIDualReturnShipmentKey = "";
            //shipment.MIDualReturnShipmentIndicator = "";
            //Not required, not used right now.
            //shipment.RatingMethodRequestedIndicator = "";
            shipment.TaxInformationIndicator = "";
            //Right now we are not using this, but this will most likely be needed when we do international shipments as it contains a lot of the forms, this also incudes notifications, which might be useful.
            //shipment.ShipmentServiceOptions = SetShipmentServiceOptions(jobShipment);

            shipment.Package = SetPackages(jobShipment);
            return shipment;
        }
        private ShipperType SetShipper(JobShipment jobShipment, string upsAccount)
        {
            //I am pretty sure this is supposed to br WBG info assiciated with the account the API is connected to.
            ShipperType shipper = new ShipperType();
            shipper.Name = jobShipment.ReturnCompany;
            shipper.AttentionName = jobShipment.ReturnName;
            //Not required and will not be used.
            //shipper.CompanyDisplayableName = "";
            //Only required for international shipments.
            //shipper.TaxIdentificationNumber = "";
            //Only required for international shipments
            shipper.Phone = new ShipPhoneType();
            shipper.Phone.Number = jobShipment.ReturnPhone;
            //shipper.Phone.Extension = "";
            //This is the account number that the api is associated with, will not be billed unless specified "BillShipper".
            //Need to add logic here to use the account the user is shipping from.
            shipper.ShipperNumber = upsAccount;
            //Not required
            //shipper.FaxNumber = "";
            //Not required
            //shipper.EMailAddress = "";
            shipper.Address = new ShipAddressType();
            shipper.Address.AddressLine = new string[3];
            shipper.Address.AddressLine[0] = jobShipment.ReturnAddress1;
            shipper.Address.AddressLine[1] = jobShipment.ReturnAddress2;
            shipper.Address.AddressLine[2] = jobShipment.ReturnAddress3;
            shipper.Address.City = jobShipment.ReturnCity;
            shipper.Address.StateProvinceCode = jobShipment.ReturnState;
            shipper.Address.PostalCode = jobShipment.ReturnZip;
            shipper.Address.CountryCode = jobShipment.ReturnCountry;
            return shipper;
        }
        private ShipToType SetShipTo(JobShipment jobShipment)
        {
            ShipToType shipTo = new ShipToType();
            shipTo.Name = jobShipment.ShipName;
            shipTo.AttentionName = jobShipment.ShipInNameOf;
            //This is currently not even used
            //shipTo.CompanyDisplayableName = "";
            //Not required
            //shipTo.TaxIdentificationNumber = "";
            shipTo.Phone = new ShipPhoneType();
            shipTo.Phone.Number = jobShipment.ShipPhone;
            //Not required
            //shipTo.Phone.Extension = "";
            //Not required
            //shipTo.FaxNumber = "";
            //Not required
            //shipTo.EMailAddress = "";
            shipTo.Address = new ShipToAddressType();
            shipTo.Address.AddressLine = new string[3];
            shipTo.Address.AddressLine[0] = jobShipment.ShipAddress1;
            shipTo.Address.AddressLine[1] = jobShipment.ShipAddress2;
            shipTo.Address.AddressLine[2] = jobShipment.ShipAddress3;
            shipTo.Address.City = jobShipment.ShipCity;
            shipTo.Address.StateProvinceCode = jobShipment.ShipState;
            shipTo.Address.PostalCode = jobShipment.ShipZip;
            shipTo.Address.CountryCode = jobShipment.ShipCountry;
            //Not required
            //shipTo.Address.ResidentialAddressIndicator = "";
            //Not required. Not sure of the use.
            //shipTo.LocationID = "";
            return shipTo;
        }
        //This whole method is not required. Will look into it if needed.
        //private AlternateDeliveryAddressType SetAlternateDeliveryAddress()
        //{
        //    AlternateDeliveryAddressType alternateDeliveryAddress = new AlternateDeliveryAddressType();
        //    alternateDeliveryAddress.Name = "";
        //    alternateDeliveryAddress.AttentionName = "";
        //    alternateDeliveryAddress.UPSAccessPointID = "";
        //    alternateDeliveryAddress.Address = new ADLAddressType();
        //    alternateDeliveryAddress.Address.AddressLine = new string[3];
        //    alternateDeliveryAddress.Address.AddressLine[0] = "";
        //    alternateDeliveryAddress.Address.AddressLine[1] = "";
        //    alternateDeliveryAddress.Address.AddressLine[2] = "";
        //    alternateDeliveryAddress.Address.City = "";
        //    alternateDeliveryAddress.Address.StateProvinceCode = "";
        //    alternateDeliveryAddress.Address.PostalCode = "";
        //    alternateDeliveryAddress.Address.CountryCode = "";
        //    alternateDeliveryAddress.Address.ResidentialAddressIndicator = "";
        //    alternateDeliveryAddress.Address.POBoxIndicator = "";
        //    return alternateDeliveryAddress;
        //}
        private ShipFromType SetShipFrom(JobShipment jobShipment)
        {
            string returnCompany = "";
            string returnAddress = "";
            string returnCity = "";
            string returnState = "";
            string returnZip = "";
            string returnPhone = "";
            returnCompany = "Shipping";
            if (jobShipment.Location == "Burnside")
            {
                returnAddress = "Shipping Address";
                returnCity = "Shipping City";
                returnState = "Shipping State";
                returnZip = "Shipping Zip";
                returnPhone = "Shipping Phone";
            }
            if (jobShipment.Location == "Kent")
            {
                returnAddress = "Shipping Address";
                returnCity = "Shipping City";
                returnState = "Shipping State";
                returnZip = "Shipping Zip";
                returnPhone = "Shipping Phone";
            }
            if (jobShipment.Location == "Portland Warehouse")
            {
                returnAddress = "Shipping Address";
                returnCity = "Shipping City";
                returnState = "Shipping State";
                returnZip = "Shipping Zip";
                returnPhone = "Shipping Phone";
            }
            if (jobShipment.Location == "Corporate")
            {
                returnAddress = "Shipping Address";
                returnCity = "Shipping City";
                returnState = "Shipping State";
                returnZip = "Shipping Zip";
                returnPhone = "Shipping Phone";
            }
            if (jobShipment.Location == "Chino")
            {
                returnAddress = "Shipping Address";
                returnCity = "Shipping City";
                returnState = "Shipping State";
                returnZip = "Shipping Zip";
                returnPhone = "Shipping Phone";
            }
            ShipFromType shipFrom = new ShipFromType();
            shipFrom.Name = returnCompany;
            //shipFrom.AttentionName = "";
            //Not used at this time.
            //shipFrom.CompanyDisplayableName = "";
            //Only required for international shipments, maybe.
            //shipFrom.TaxIdentificationNumber = "";
            //Only required for international shipments, maybe.
            //shipFrom.TaxIDType = new TaxIDCodeDescType();
            //shipFrom.TaxIDType.Code = "";
            //shipFrom.TaxIDType.Description = "";
            shipFrom.Phone = new ShipPhoneType();
            shipFrom.Phone.Number = returnPhone;
            //Not required.
            //shipFrom.Phone.Extension = "";
            //Not required.
            //shipFrom.FaxNumber = "";
            shipFrom.Address = new ShipAddressType();
            shipFrom.Address.AddressLine = new string[3];
            shipFrom.Address.AddressLine[0] = returnAddress;
            //shipFrom.Address.AddressLine[1] = "";
            //shipFrom.Address.AddressLine[2] = "";
            shipFrom.Address.City = returnCity;
            shipFrom.Address.StateProvinceCode = returnState;
            shipFrom.Address.PostalCode = returnZip;
            shipFrom.Address.CountryCode = "US";
            return shipFrom;
        }
        private PaymentInfoType SetPaymentInformation(JobShipment jobShipment, string upsAccount)
        {
            PaymentInfoType paymentInformation = new PaymentInfoType();
            paymentInformation.ShipmentCharge = new ShipmentChargeType[1];
            paymentInformation.ShipmentCharge[0] = new ShipmentChargeType();
            paymentInformation.ShipmentCharge[0].Type = "01";
            if (jobShipment.BillShipmentTo == "BillToReceiver")
            {
                paymentInformation.ShipmentCharge[0].BillReceiver = new BillReceiverType();
                paymentInformation.ShipmentCharge[0].BillReceiver.AccountNumber = "";
                paymentInformation.ShipmentCharge[0].BillReceiver.Address = new BillReceiverAddressType();
                paymentInformation.ShipmentCharge[0].BillReceiver.Address.PostalCode = "";
            }
            else
            {
                if (jobShipment.BillShipmentTo == "Shipper")
                {
                    //Need to add logic here to use the account that the user is shipping from.
                    //I believe we will just use our UPS account to pay for shipments if no third party account is specified.
                    paymentInformation.ShipmentCharge[0].BillShipper = new BillShipperType();
                    paymentInformation.ShipmentCharge[0].BillShipper.AccountNumber = upsAccount;
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard = new CreditCardType();
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Type = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Number = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.ExpirationDate = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.SecurityCode = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Address = new CreditCardAddressType();
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Address.AddressLine = new string[3];
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Address.AddressLine[0] = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Address.AddressLine[1] = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Address.AddressLine[2] = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Address.City = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Address.StateProvinceCode = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Address.PostalCode = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.CreditCard.Address.CountryCode = "";
                    //paymentInformation.ShipmentCharge[0].BillShipper.AlternatePaymentMethod = "";
                }
                if (jobShipment.BillShipmentTo == "3rd Party")
                {
                    paymentInformation.ShipmentCharge[0].BillThirdParty = new BillThirdPartyChargeType();
                    paymentInformation.ShipmentCharge[0].BillThirdParty.AccountNumber = jobShipment.ThirdPartyBilling;
                    paymentInformation.ShipmentCharge[0].BillThirdParty.Address = new AccountAddressType();
                    paymentInformation.ShipmentCharge[0].BillThirdParty.Address.PostalCode = jobShipment.BillAccountZip;
                    paymentInformation.ShipmentCharge[0].BillThirdParty.Address.CountryCode = "US";
                }
            }
            //I don't think we will ever use this.    
            //paymentInformation.ShipmentCharge[0].ConsigneeBilledIndicator = "";
            //I believe we will only need this for international shipments if at all.
            //paymentInformation.SplitDutyVATIndicator = "";
            return paymentInformation;
        }
        //We are not using this method currently.
        //private FRSPaymentInfoType SetFRSPaymentInformation(JobShipment jobShipment)
        //{
        //    FRSPaymentInfoType frsPaymentInfoType = new FRSPaymentInfoType();
        //    frsPaymentInfoType.Type = new PaymentType();
        //    frsPaymentInfoType.Type.Code = "";
        //    frsPaymentInfoType.Type.Description = "";
        //    frsPaymentInfoType.AccountNumber = "";
        //    frsPaymentInfoType.Address = new AccountAddressType();
        //    frsPaymentInfoType.Address.PostalCode = "";
        //    frsPaymentInfoType.Address.CountryCode = "";
        //    return frsPaymentInfoType;
        //}
        //We are not using this method currently.
        //private FreightShipmentInformationType SetFreightShipmentInformation(JobShipment jobShipment)
        //{
        //    FreightShipmentInformationType freightShipmentInformationType = new FreightShipmentInformationType();
        //    freightShipmentInformationType.FreightDensityInfo = new FreightDensityInfoType();
        //    freightShipmentInformationType.FreightDensityInfo.AdjustedHeightIndicator = "";
        //    freightShipmentInformationType.FreightDensityInfo.AdjustedHeight = new AdjustedHeightType();
        //    freightShipmentInformationType.FreightDensityInfo.AdjustedHeight.Value = "";
        //    freightShipmentInformationType.FreightDensityInfo.AdjustedHeight.UnitOfMeasurement = new ShipUnitOfMeasurementType();
        //    freightShipmentInformationType.FreightDensityInfo.AdjustedHeight.UnitOfMeasurement.Code = "";
        //    freightShipmentInformationType.FreightDensityInfo.AdjustedHeight.UnitOfMeasurement.Description = "";
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits = new HandlingUnitsType[1];
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0] = new HandlingUnitsType();
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Quantity = "";
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Type = new ShipUnitOfMeasurementType();
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Type.Code = "";
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Type.Description = "";
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Dimensions = new HandlingUnitsDimensionsType();
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Dimensions.UnitOfMeasurement = new ShipUnitOfMeasurementType();
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Dimensions.UnitOfMeasurement.Code = "";
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Dimensions.UnitOfMeasurement.Description = "";
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Dimensions.Length = "";
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Dimensions.Width = "";
        //    freightShipmentInformationType.FreightDensityInfo.HandlingUnits[0].Dimensions.Height = "";
        //    freightShipmentInformationType.DensityEligibleIndicator = "";
        //    return freightShipmentInformationType;
        //}
        private RateInfoType SetShipmentRatingOptions(JobShipment jobShipment)
        {
            RateInfoType shipmentRatingOptions = new RateInfoType();
            //Keep this on to get negotiated rates back in the server response.
            shipmentRatingOptions.NegotiatedRatesIndicator = "";
            //This returns freight rates from the server. Not currently used.
            //shipmentRatingOptions.FRSShipmentIndicator = "";
            //Not sure if this is important, can be turned off if unecessary.
            shipmentRatingOptions.RateChartIndicator = "";
            //This returns weird freight rates from the server. Not currently used.
            //shipmentRatingOptions.TPFCNegotiatedRatesIndicator = "";
            //Not sure if this is important, can be turned off if unecessary.
            shipmentRatingOptions.UserLevelDiscountIndicator = "";
            return shipmentRatingOptions;
        }
        private ReferenceNumberType[] SetReferenceNumbers(JobShipment jobShipment)
        {
            ReferenceNumberType[] referenceNumbers = new ReferenceNumberType[2];
            //This is not required and is only needed if you want the reference in the barcode.
            //referenceNumbers[0].BarCodeIndicator = "";
            //Need to come back and get the list of codes.
            if (jobShipment.InvoiceNumber != null && jobShipment.PONumber != null)
            {
                referenceNumbers[0] = new ReferenceNumberType();
                referenceNumbers[0].Code = "IK";
                referenceNumbers[0].Value = jobShipment.InvoiceNumber;
                referenceNumbers[1] = new ReferenceNumberType();
                referenceNumbers[1].Code = "PO";
                referenceNumbers[1].Value = jobShipment.PONumber;
            }
            else
            {
                if (jobShipment.InvoiceNumber != null)
                {
                    referenceNumbers = new ReferenceNumberType[1];
                    referenceNumbers[0] = new ReferenceNumberType();
                    referenceNumbers[0].Code = "IK";
                    referenceNumbers[0].Value = jobShipment.InvoiceNumber;
                }
                if (jobShipment.PONumber != null)
                {
                    referenceNumbers = new ReferenceNumberType[1];
                    referenceNumbers[0] = new ReferenceNumberType();
                    referenceNumbers[0].Code = "PO";
                    referenceNumbers[0].Value = jobShipment.PONumber;
                }
            }

            return referenceNumbers;
        }
        private ServiceType SetService(JobShipment jobShipment)
        {
            ServiceType service = new ServiceType();
            service.Code = jobShipment.ServiceLevel;
            //Not required and not really needed.
            //service.Description = "";            
            return service;
        }
        //We are not using this method currently.
        //private CurrencyMonetaryType SetInvoiceLineTotal(JobShipment jobShipment)
        //{
        //    CurrencyMonetaryType invoiceLineTotal = new CurrencyMonetaryType();
        //    invoiceLineTotal.CurrencyCode = "";
        //    invoiceLineTotal.MonetaryValue = "";
        //    return invoiceLineTotal;
        //}
        //Right now, we are not using this, but may use it when we start international shipments.
        //private ShipmentTypeShipmentServiceOptions SetShipmentServiceOptions(JobShipment jobShipment)
        //{
        //    ShipmentTypeShipmentServiceOptions shipmentServiceOptions = new ShipmentTypeShipmentServiceOptions();
        //    shipmentServiceOptions.SaturdayDeliveryIndicator = "";
        //    shipmentServiceOptions.SaturdayPickupIndicator = "";
        //    shipmentServiceOptions.COD = new CODType();
        //    shipmentServiceOptions.COD.CODFundsCode = "";
        //    shipmentServiceOptions.COD.CODAmount = new CurrencyMonetaryType();
        //    shipmentServiceOptions.COD.CODAmount.CurrencyCode = "";
        //    shipmentServiceOptions.COD.CODAmount.MonetaryValue = "";
        //    shipmentServiceOptions.AccessPointCOD = new ShipmentServiceOptionsAccessPointCODType();
        //    shipmentServiceOptions.AccessPointCOD.CurrencyCode = "";
        //    shipmentServiceOptions.AccessPointCOD.MonetaryValue = "";
        //    shipmentServiceOptions.DeliverToAddresseeOnlyIndicator = "";
        //    shipmentServiceOptions.DirectDeliveryOnlyIndicator = "";
        //    shipmentServiceOptions.Notification = new NotificationType[3];
        //    shipmentServiceOptions.Notification[0] = new NotificationType();
        //    shipmentServiceOptions.Notification[0].NotificationCode = "";
        //    shipmentServiceOptions.Notification[0].EMail = new EmailDetailsType();
        //    shipmentServiceOptions.Notification[0].EMail.EMailAddress = new string[5];
        //    shipmentServiceOptions.Notification[0].EMail.EMailAddress[0] = "";
        //    shipmentServiceOptions.Notification[0].EMail.EMailAddress[1] = "";
        //    shipmentServiceOptions.Notification[0].EMail.EMailAddress[2] = "";
        //    shipmentServiceOptions.Notification[0].EMail.EMailAddress[3] = "";
        //    shipmentServiceOptions.Notification[0].EMail.EMailAddress[4] = "";
        //    shipmentServiceOptions.Notification[0].EMail.UndeliverableEMailAddress = "";
        //    shipmentServiceOptions.Notification[0].EMail.FromEMailAddress = "";
        //    shipmentServiceOptions.Notification[0].EMail.FromName = "";
        //    shipmentServiceOptions.Notification[0].EMail.Memo = "";
        //    shipmentServiceOptions.Notification[0].EMail.Subject = "";
        //    shipmentServiceOptions.Notification[0].EMail.SubjectCode = "";
        //    shipmentServiceOptions.Notification[0].VoiceMessage = new ShipmentServiceOptionsNotificationVoiceMessageType();
        //    shipmentServiceOptions.Notification[0].VoiceMessage.PhoneNumber = "";
        //    shipmentServiceOptions.Notification[0].TextMessage = new ShipmentServiceOptionsNotificationTextMessageType();
        //    shipmentServiceOptions.Notification[0].TextMessage.PhoneNumber = "";
        //    shipmentServiceOptions.Notification[0].Locale = new LocaleType();
        //    shipmentServiceOptions.Notification[0].Locale.Language = "";
        //    shipmentServiceOptions.Notification[0].Locale.Dialect = "";
        //    shipmentServiceOptions.LabelDelivery = new LabelDeliveryType();
        //    shipmentServiceOptions.LabelDelivery.EMail = new EmailDetailsType();
        //    shipmentServiceOptions.LabelDelivery.EMail.EMailAddress = new string[1];
        //    shipmentServiceOptions.LabelDelivery.EMail.EMailAddress[0] = "";
        //    shipmentServiceOptions.LabelDelivery.EMail.UndeliverableEMailAddress = "";
        //    shipmentServiceOptions.LabelDelivery.EMail.FromEMailAddress = "";
        //    shipmentServiceOptions.LabelDelivery.EMail.FromName = "";
        //    shipmentServiceOptions.LabelDelivery.EMail.Memo = "";
        //    shipmentServiceOptions.LabelDelivery.EMail.Subject = "";
        //    shipmentServiceOptions.LabelDelivery.EMail.SubjectCode = "";
        //    shipmentServiceOptions.LabelDelivery.LabelLinksIndicator = "";
        //    shipmentServiceOptions.InternationalForms = new InternationalFormType();
        //    shipmentServiceOptions.InternationalForms.FormType = new string[6];
        //    shipmentServiceOptions.InternationalForms.FormType[0] = "";
        //    shipmentServiceOptions.InternationalForms.UserCreatedForm = new string[0];
        //    //The rest of this is for interantional forms options, I am done with this method.        
        //    return shipmentServiceOptions;
        //}
        private PackageType[] SetPackages(JobShipment jobShipment)
        {
            //This apparently allows up to 200 packages, but I believe there are some limitations afer 20.
            PackageType[] packages = new PackageType[jobShipment.NumberOfCartons];
            for (int i = 0; i < jobShipment.NumberOfCartons; i++)
            {
                packages[i] = new PackageType();
                //Not required, not used.
                //packages[i].Description = "";
                packages[i].Packaging = new PackagingType();
                //This is hard coded for now to customer supplied packaging. Down the road, we may want to let the user choose.
                packages[i].Packaging.Code = "02";
                //Not required, not used.
                //packages[i].Packaging.Description = "";
                //This section should not be required for the shipments we are doing.
                //packages[i].Dimensions = new DimensionsType();
                //packages[i].Dimensions.UnitOfMeasurement = new ShipUnitOfMeasurementType();
                //packages[i].Dimensions.UnitOfMeasurement.Code = "IN";
                //packages[i].Dimensions.UnitOfMeasurement.Description = "";
                //packages[i].Dimensions.Length = "";
                //packages[i].Dimensions.Width = "";
                //packages[i].Dimensions.Height = "";
                //This section is not the weight of the package and is not required.
                //packages[i].DimWeight = new PackageWeightType();
                //packages[i].DimWeight.UnitOfMeasurement = new ShipUnitOfMeasurementType();
                //packages[i].DimWeight.UnitOfMeasurement.Code = "LBS";
                //packages[i].DimWeight.UnitOfMeasurement.Description = "";
                //packages[i].DimWeight.Weight = "";
                packages[i].PackageWeight = new PackageWeightType();
                packages[i].PackageWeight.UnitOfMeasurement = new ShipUnitOfMeasurementType();
                packages[i].PackageWeight.UnitOfMeasurement.Code = "LBS";
                //Not required, not used.
                //packages[i].PackageWeight.UnitOfMeasurement.Description = "";
                packages[i].PackageWeight.Weight = jobShipment.CartonWeights[i];
                //Not required and should not be needed.
                //packages[i].LargePackageIndicator = "";
                //This is only valid if the shipment is US to US or PR to PR, otherwise use the reference fields at the shipment level.
                if (jobShipment.InvoiceNumber != null || jobShipment.PONumber != null)
                {
                    packages[i].ReferenceNumber = SetReferenceNumbers(jobShipment);
                }
                //Not required and will not be needed.
                //packages[i].AdditionalHandlingIndicator = "";
                //Currently we do not use package services, but may be needed down the road.
                //packages[i].PackageServiceOptions = SetPackageServiceOptions(jobShipment.Cartons[i], packages[i]);
                //This is not required and probably will not be used.
                //packages[i].Commodity = new CommodityType();
                //packages[i].Commodity.FreightClass = "";
                //packages[i].Commodity.NMFC = new NMFCType();
                //packages[i].Commodity.NMFC.PrimeCode = "";
                //packages[i].Commodity.NMFC.SubCode = "";
                //This will probably never be used unless sending hazardous chemicals.
                //packages[i].HazMatPackageInformation = new HazMatPackageInformationType();
                //packages[i].HazMatPackageInformation.AllPackedInOneIndicator = "";
                //packages[i].HazMatPackageInformation.OverPackedIndicator = "";
                //packages[i].HazMatPackageInformation.QValue = "";
            }
            return packages;
        }
        //Currently we do not use any special package service options, may need down the road.
        //private PackageServiceOptionsType SetPackageServiceOptions(JobCarton carton, PackageType package)
        //{
        //    PackageServiceOptionsType packageServiceOptions = new PackageServiceOptionsType();
        //    packageServiceOptions.DeliveryConfirmation = new DeliveryConfirmationType();
        //    packageServiceOptions.DeliveryConfirmation.DCISType = "";
        //    packageServiceOptions.DeliveryConfirmation.DCISNumber = "";
        //    packageServiceOptions.DeclaredValue = new PackageDeclaredValueType();
        //    packageServiceOptions.DeclaredValue.Type = new DeclaredValueType();
        //    packageServiceOptions.DeclaredValue.Type.Code = "";
        //    packageServiceOptions.DeclaredValue.Type.Description = "";
        //    packageServiceOptions.DeclaredValue.CurrencyCode = "";
        //    packageServiceOptions.DeclaredValue.MonetaryValue = "";
        //    packageServiceOptions.COD = new PSOCODType();
        //    packageServiceOptions.COD.CODFundsCode = "";
        //    packageServiceOptions.COD.CODAmount = new CurrencyMonetaryType();
        //    packageServiceOptions.COD.CODAmount.CurrencyCode = "";
        //    packageServiceOptions.COD.CODAmount.MonetaryValue = "";
        //    packageServiceOptions.AccessPointCOD = new PackageServiceOptionsAccessPointCODType();
        //    packageServiceOptions.AccessPointCOD.CurrencyCode = "";
        //    packageServiceOptions.AccessPointCOD.MonetaryValue = "";
        //    packageServiceOptions.VerbalConfirmation = new VerbalConfirmationType();
        //    packageServiceOptions.VerbalConfirmation.ContactInfo = new ContactInfoType();
        //    packageServiceOptions.VerbalConfirmation.ContactInfo.Name = "";
        //    packageServiceOptions.VerbalConfirmation.ContactInfo.Phone = new ShipPhoneType();
        //    packageServiceOptions.VerbalConfirmation.ContactInfo.Phone.Number = "";
        //    packageServiceOptions.VerbalConfirmation.ContactInfo.Phone.Extension = "";
        //    packageServiceOptions.ShipperReleaseIndicator = "";
        //    packageServiceOptions.Notification = new PSONotificationType();
        //    packageServiceOptions.Notification.NotificationCode = "";
        //    packageServiceOptions.Notification.EMail = new EmailDetailsType();
        //    packageServiceOptions.Notification.EMail.EMailAddress = new string[1];
        //    packageServiceOptions.Notification.EMail.EMailAddress[0] = "";
        //    packageServiceOptions.Notification.EMail.UndeliverableEMailAddress = "";
        //    packageServiceOptions.Notification.EMail.FromEMailAddress = "";
        //    packageServiceOptions.Notification.EMail.FromName = "";
        //    packageServiceOptions.Notification.EMail.Memo = "";
        //    packageServiceOptions.Notification.EMail.Subject = "";
        //    packageServiceOptions.Notification.EMail.SubjectCode = "";
        //    packageServiceOptions.HazMat = new HazMatType[3];
        //    packageServiceOptions.HazMat[0] = new HazMatType();
        //    packageServiceOptions.HazMat[0].PackagingTypeQuantity = "";
        //    packageServiceOptions.HazMat[0].RecordIdentifier1 = "";
        //    packageServiceOptions.HazMat[0].RecordIdentifier2 = "";
        //    packageServiceOptions.HazMat[0].RecordIdentifier3 = "";
        //    packageServiceOptions.HazMat[0].SubRiskClass = "";
        //    packageServiceOptions.HazMat[0].aDRItemNumber = "";
        //    packageServiceOptions.HazMat[0].aDRPackingGroupLetter = "";
        //    packageServiceOptions.HazMat[0].TechnicalName = "";
        //    packageServiceOptions.HazMat[0].HazardLabelRequired = "";
        //    packageServiceOptions.HazMat[0].ClassDivisionNumber = "";
        //    packageServiceOptions.HazMat[0].ReferenceNumber = "";
        //    packageServiceOptions.HazMat[0].Quantity = "";
        //    packageServiceOptions.HazMat[0].UOM = "";
        //    packageServiceOptions.HazMat[0].PackagingType = "";
        //    packageServiceOptions.HazMat[0].IDNumber = "";
        //    packageServiceOptions.HazMat[0].ProperShippingName = "";
        //    packageServiceOptions.HazMat[0].AdditionalDescription = "";
        //    packageServiceOptions.HazMat[0].PackagingGroupType = "";
        //    packageServiceOptions.HazMat[0].PackagingInstructionCode = "";
        //    packageServiceOptions.HazMat[0].EmergencyPhone = "";
        //    packageServiceOptions.HazMat[0].EmergencyContact = "";
        //    packageServiceOptions.HazMat[0].ReportableQuantity = "";
        //    packageServiceOptions.HazMat[0].RegulationSet = "";
        //    packageServiceOptions.HazMat[0].TransportationMode = "";
        //    packageServiceOptions.HazMat[0].CommodityRegulatedLevelCode = "";
        //    packageServiceOptions.HazMat[0].TransportCategory = "";
        //    packageServiceOptions.HazMat[0].TunnelRestrictionCode = "";
        //    packageServiceOptions.HazMat[0].ChemicalRecordIdentifier = "";
        //    packageServiceOptions.DryIce = new DryIceType();
        //    packageServiceOptions.DryIce.RegulationSet = "";
        //    packageServiceOptions.DryIce.DryIceWeight = new DryIceWeightType();
        //    packageServiceOptions.DryIce.DryIceWeight.UnitOfMeasurement = new ShipUnitOfMeasurementType();
        //    packageServiceOptions.DryIce.DryIceWeight.UnitOfMeasurement.Code = "";
        //    packageServiceOptions.DryIce.DryIceWeight.UnitOfMeasurement.Description = "";
        //    packageServiceOptions.DryIce.DryIceWeight.Weight = "";
        //    packageServiceOptions.DryIce.MedicalUseIndicator = "";
        //    packageServiceOptions.UPSPremiumCareIndicator = "";
        //    packageServiceOptions.ProactiveIndicator = "";
        //    packageServiceOptions.PackageIdentifier = "";
        //    packageServiceOptions.ClinicalTrialsID = "";
        //    return packageServiceOptions;
        //}
        private LabelSpecificationType SetLabelSpecification(JobShipment jobShipment)
        {
            //Need to add printer logic here.
            string lang = GetPrinterLanguage(jobShipment.PrinterName);
            LabelSpecificationType labelSpecification = new LabelSpecificationType();
            labelSpecification.LabelImageFormat = new LabelImageFormatType();
            //"EPL", "ZPL", "GIF", "SPL", "STARPL"
            labelSpecification.LabelImageFormat.Code = lang; //prod
            //labelSpecification.LabelImageFormat.Code = "GIF";
            //Not required, not used.
            //labelSpecification.LabelImageFormat.Description = "";
            //This is only needed for GIF, when set live, this can be removed.
            //labelSpecification.HTTPUserAgent = "Mozilla/4.5";
            labelSpecification.LabelStockSize = new LabelStockSizeType();
            labelSpecification.LabelStockSize.Height = "6";
            labelSpecification.LabelStockSize.Width = "4";
            //Not required, not used.
            //labelSpecification.Instruction = new InstructionCodeDescriptionType[1];
            //labelSpecification.Instruction[0].Code = "";
            //labelSpecification.Instruction[0].Description = "";
            //Not required, unless a different language than english is needed.
            //labelSpecification.CharacterSet = "";
            return labelSpecification;
        }
        private ReceiptSpecificationType SetReceiptSpecification(JobShipment jobShipment)
        {
            ReceiptSpecificationType receiptSpecification = new ReceiptSpecificationType();
            receiptSpecification.ImageFormat = new ReceiptImageFormatType();
            //"EPL", "SPL", "ZPL", "STARPL", "HTML"
            string lang = GetPrinterLanguage(jobShipment.PrinterName);
            receiptSpecification.ImageFormat.Code = lang;
            receiptSpecification.ImageFormat.Description = "";
            return receiptSpecification;
        }
        private static UPSSecurity GetSecurityCredentials()
        {
            UPSSecurity upss = new UPSSecurity()
            {
                //This is the API Key
                ServiceAccessToken = new UPSSecurityServiceAccessToken()
                {
                    AccessLicenseNumber = "UPS Access Token"
                },
                //This is the credentials for the account that can use the above API Key
                UsernameToken = new UPSSecurityUsernameToken()
                {
                    Username = "UPS Username",
                    Password = "UPS Password"
                }
            };

            return upss;
        }
        private string GetServiceLevel(string shipViaService)
        {
            switch (shipViaService)
            {
                case "GRND":
                    return "03";
                case "2DAY":
                    return "02";
                case "3DAY":
                    return "12";
                case "NDA":
                    return "01";
                case "NDAAM":
                    return "14";
            }
            return "";
        }
        private int StoreUPSShipmentInDB(UPSShipment upsShipment, string userId)
        {
            int shipmentId = 0;
            try
            {
                using (SqlConnection sqlconn = new SqlConnection(connName))
                {
                    using (SqlCommand cmd = new SqlCommand("INSERT INTO UPS_Shipments(MasterTrackingNumber, EPMSOrderNumber, EPMSShipmentNumber, ThirdPartyAccount, ThirdPartyZip, ShipTime, Status, Weight, StandardCost, StandardTax, StandardTotal, NegotiatedCost, NegotiatedTax, NegotiatedTotal, NumberOfPackages, [User], Ref1, Ref2) " +
                                                            "OUTPUT INSERTED.Id " +
                                                            "VALUES(@MasterTrackingNumber, @EPMSOrderNumber, @EPMSShipmentNumber, @ThirdPartyAccount, @ThirdPartyZip, @ShipTime, @Status, @Weight, @StandardCost, @StandardTax, @StandardTotal, @NegotiatedCost, @NegotiatedTax, @NegotiatedTotal, @NumberOfPackages, @User, @Ref1, @Ref2)", sqlconn))
                    {
                        sqlconn.Open();
                        cmd.Parameters.Add("@User", SqlDbType.UniqueIdentifier).Value = new Guid(userId);
                        cmd.Parameters.Add("@MasterTrackingNumber", SqlDbType.VarChar).Value = upsShipment.MasterTrackingNumber;
                        cmd.Parameters.Add("@EPMSOrderNumber", SqlDbType.VarChar).Value = upsShipment.EPMSOrderNumber;
                        cmd.Parameters.Add("@EPMSShipmentNumber", SqlDbType.Int).Value = upsShipment.EPMSShipmentNumber;
                        cmd.Parameters.Add("@Ref1", SqlDbType.VarChar).Value = upsShipment.Ref1 ?? "";
                        cmd.Parameters.Add("@Ref2", SqlDbType.VarChar).Value = upsShipment.Ref2 ?? "";
                        if (upsShipment.ThirdPartyAccount != null)
                        {
                            cmd.Parameters.Add("@ThirdPartyAccount", SqlDbType.VarChar).Value = upsShipment.ThirdPartyAccount;
                            cmd.Parameters.Add("@ThirdPartyZip", SqlDbType.VarChar).Value = upsShipment.ThirdPartyZip;
                        }
                        else
                        {
                            cmd.Parameters.Add("@ThirdPartyAccount", SqlDbType.VarChar).Value = DBNull.Value;
                            cmd.Parameters.Add("@ThirdPartyZip", SqlDbType.VarChar).Value = DBNull.Value;
                        }
                        cmd.Parameters.Add("@ShipTime", SqlDbType.DateTime).Value = upsShipment.ShipTime;
                        cmd.Parameters.Add("@Status", SqlDbType.VarChar).Value = upsShipment.Status;
                        cmd.Parameters.Add("@Weight", SqlDbType.Float).Value = upsShipment.Weight;
                        if (upsShipment.StandardTotal != null)
                        {
                            cmd.Parameters.Add("@StandardCost", SqlDbType.Float).Value = upsShipment.StandardCost;
                            cmd.Parameters.Add("@StandardTax", SqlDbType.Float).Value = upsShipment.StandardTax;
                            cmd.Parameters.Add("@StandardTotal", SqlDbType.Float).Value = upsShipment.StandardTotal;
                        }
                        else
                        {
                            cmd.Parameters.Add("@StandardCost", SqlDbType.Float).Value = DBNull.Value;
                            cmd.Parameters.Add("@StandardTax", SqlDbType.Float).Value = DBNull.Value;
                            cmd.Parameters.Add("@StandardTotal", SqlDbType.Float).Value = DBNull.Value;
                        }
                        if (upsShipment.NegotiatedTotal != null)
                        {
                            cmd.Parameters.Add("@NegotiatedCost", SqlDbType.Float).Value = upsShipment.NegotiatedCost;
                            cmd.Parameters.Add("@NegotiatedTax", SqlDbType.Float).Value = upsShipment.NegotiatedTax;
                            cmd.Parameters.Add("@NegotiatedTotal", SqlDbType.Float).Value = upsShipment.NegotiatedTotal;
                        }
                        else
                        {
                            cmd.Parameters.Add("@NegotiatedCost", SqlDbType.Float).Value = DBNull.Value;
                            cmd.Parameters.Add("@NegotiatedTax", SqlDbType.Float).Value = DBNull.Value;
                            cmd.Parameters.Add("@NegotiatedTotal", SqlDbType.Float).Value = DBNull.Value;
                        }
                        cmd.Parameters.Add("@NumberOfPackages", SqlDbType.Int).Value = upsShipment.NumberOfPackages;
                        shipmentId = (int)cmd.ExecuteScalar();
                    }
                }
            }
            catch (Exception ex)
            {
                //Need to add logging here. 
                Console.WriteLine(ex.Message);
            }
            return shipmentId;
        }
        private void StoreUPSPAckageinDB(UPSPackage upsPackage)
        {
            try
            {
                using (SqlConnection sqlconn = new SqlConnection(connName))
                {
                    using (SqlCommand cmd = new SqlCommand("INSERT INTO UPS_Packages(ShipmentId, LabelFileName, LabelFileBlob, TrackingNumber, ShipTime, Status, Weight, StandardCost, StandardTax, StandardTotal, NegotiatedCost, NegotiatedTax, NegotiatedTotal)" +
                                                            "VALUES(@ShipmentId, @LabelFileName, @LabelFileBlob, @TrackingNumber, @ShipTime, @Status, @Weight, @StandardCost, @StandardTax, @StandardTotal, @NegotiatedCost, @NegotiatedTax, @NegotiatedTotal)", sqlconn))
                    {
                        sqlconn.Open();
                        cmd.Parameters.Add("@ShipmentId", SqlDbType.Int).Value = upsPackage.ShipmentId;
                        cmd.Parameters.Add("@LabelFileName", SqlDbType.VarChar).Value = upsPackage.LabelFileName;
                        cmd.Parameters.Add("@LabelFileBlob", SqlDbType.VarBinary).Value = upsPackage.LabelFileBlob;
                        cmd.Parameters.Add("@TrackingNumber", SqlDbType.VarChar).Value = upsPackage.TrackingNumber;
                        cmd.Parameters.Add("@ShipTime", SqlDbType.DateTime).Value = upsPackage.ShipTime;
                        cmd.Parameters.Add("@Status", SqlDbType.VarChar).Value = upsPackage.Status;
                        cmd.Parameters.Add("@Weight", SqlDbType.Float).Value = upsPackage.Weight;
                        if (upsPackage.StandardTotal != null)
                        {
                            cmd.Parameters.Add("@StandardCost", SqlDbType.Float).Value = upsPackage.StandardCost;
                            cmd.Parameters.Add("@StandardTax", SqlDbType.Float).Value = upsPackage.StandardTax;
                            cmd.Parameters.Add("@StandardTotal", SqlDbType.Float).Value = upsPackage.StandardTotal;
                        }
                        else
                        {
                            cmd.Parameters.Add("@StandardCost", SqlDbType.Float).Value = DBNull.Value;
                            cmd.Parameters.Add("@StandardTax", SqlDbType.Float).Value = DBNull.Value;
                            cmd.Parameters.Add("@StandardTotal", SqlDbType.Float).Value = DBNull.Value;
                        }
                        if (upsPackage.NegotiatedTotal != null)
                        {
                            cmd.Parameters.Add("@NegotiatedCost", SqlDbType.Float).Value = upsPackage.NegotiatedCost;
                            cmd.Parameters.Add("@NegotiatedTax", SqlDbType.Float).Value = upsPackage.NegotiatedTax;
                            cmd.Parameters.Add("@NegotiatedTotal", SqlDbType.Float).Value = upsPackage.NegotiatedTotal;
                        }
                        else
                        {
                            cmd.Parameters.Add("@NegotiatedCost", SqlDbType.Float).Value = DBNull.Value;
                            cmd.Parameters.Add("@NegotiatedTax", SqlDbType.Float).Value = DBNull.Value;
                            cmd.Parameters.Add("@NegotiatedTotal", SqlDbType.Float).Value = DBNull.Value;
                        }
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                //Need to add logging here. 
                Console.WriteLine(ex.Message);
            }
        }
        private void UpdateEPMSWithShipmentInfo(UPSShipment upsShipment)
        {
            cDataServer server = new cDataServer();
            cJobHeader header = new cJobHeader();
            header.Load("ORDER", upsShipment.EPMSOrderNumber);
            header.DeliveryDate = upsShipment.ShipTime;
            header.Save();

            cShipment shipment = new cShipment();
            shipment.Load(upsShipment.EPMSShipmentNumber);
            shipment.ScheduledShipDate = upsShipment.ShipTime;
            shipment.Shipped = true;
            foreach (cPackage package in shipment.objPackages)
            {
                package.DeliveryDate = upsShipment.ShipTime;
                package.ShipDate = upsShipment.ShipTime;
                package.Save(ref server);
                package.Dispose();
            }
            shipment.Save();
            server.Dispose();
            header.Dispose();
            shipment.Dispose();
        }        
        private void CreateEPMSPackage(UPSShipment upsShipment, UPSPackage upsPackage, JobShipment shipment)
        {
            cPackage package = new cPackage();
            package.ComponentNumber = 0;
            package.CreateDatim = DateTime.Now;
            package.DeliveryDate = upsShipment.ShipTime;
            package.EntryDate = DateTime.Now;
            package.EntryTime = DateTime.Now;
            if (shipment.BillShipmentTo == "3rd Party" && shipment.PortalId == "texasbankers-print")
            {
                package.FreightCost = Convert.ToDecimal(upsPackage.StandardTotal);
            }                
            else
            {
                if (shipment.BillShipmentTo == "3rd Party")
                    package.FreightCost = 0;
                else
                {             
                    if(shipment.Location == "Corporate")
                    {
                        package.FreightCost = Convert.ToDecimal(upsPackage.NegotiatedTotal) + (Convert.ToDecimal(upsPackage.NegotiatedTotal) * (decimal)0.25);
                    } else
                    {
                        package.FreightCost = Convert.ToDecimal(upsPackage.StandardTotal);
                    } 
                }
                    
            }                        
            package.JobNumber = upsShipment.EPMSOrderNumber;
            package.ShipmentNumber = upsShipment.EPMSShipmentNumber;
            package.ShipDate = upsShipment.ShipTime;
            package.TrackingNumber = upsPackage.TrackingNumber;
            package.Weight = upsPackage.Weight;
            JobsController.AddPackageToExistingOrder(upsShipment.EPMSOrderNumber, upsShipment.EPMSShipmentNumber, package);
            package.Dispose();
        }
        private UPSShipment SetUPSShipment(ShipmentRequest shipmentRequest, ShipmentResponse shipmentResponse, JobShipment shipment, WBGLibrary.UPSRate.RateResponse rate, string userId)
        {

            UPSShipment upsShipment = new UPSShipment();
            upsShipment.MasterTrackingNumber = shipmentResponse.ShipmentResults.ShipmentIdentificationNumber;
            upsShipment.EPMSOrderNumber = shipment.JobNumber;
            upsShipment.EPMSShipmentNumber = Convert.ToInt32(shipment.ShipmentNumber);
            upsShipment.Ref1 = shipment.InvoiceNumber ?? "";
            upsShipment.Ref2 = shipment.PONumber ?? "";
            if (shipment.BillShipmentTo == "3rd Party")
            {
                upsShipment.ThirdPartyAccount = shipment.ThirdPartyBilling;
                upsShipment.ThirdPartyZip = shipment.BillAccountZip;

            }
            else
            {
                upsShipment.ThirdPartyAccount = null;
                upsShipment.ThirdPartyZip = null;
            }
            upsShipment.ShipTime = DateTime.Now;
            upsShipment.Status = "Shipped";
            int shipmentWeight = 0;
            foreach (PackageType upsPackage in shipmentRequest.Shipment.Package)
            {
                shipmentWeight = shipmentWeight + Convert.ToInt32(upsPackage.PackageWeight.Weight);
            }
            upsShipment.Weight = shipmentWeight;
            if (Convert.ToDecimal(shipmentResponse.ShipmentResults.ShipmentCharges.TotalCharges.MonetaryValue) > 0)
            {            
                if (shipmentResponse.ShipmentResults.ShipmentCharges.TaxCharges != null)
                {
                    upsShipment.StandardTotal = Convert.ToDecimal(shipmentResponse.ShipmentResults.ShipmentCharges.TotalChargesWithTaxes.MonetaryValue);
                    upsShipment.StandardTax = Convert.ToDecimal(shipmentResponse.ShipmentResults.ShipmentCharges.TaxCharges);
                }
                else
                {
                    upsShipment.StandardTotal = Convert.ToDecimal(shipmentResponse.ShipmentResults.ShipmentCharges.TotalCharges.MonetaryValue);
                    upsShipment.StandardTax = 0;
                }
                upsShipment.StandardCost = upsShipment.StandardTotal - upsShipment.StandardTax;
            }
            else
            {
                if(shipment.PortalId == "texasbankers-print")
                {
                    if(rate.RatedShipment[0].TaxCharges != null)
                    {
                        upsShipment.StandardTotal = Convert.ToDecimal(rate.RatedShipment[0].TotalChargesWithTaxes.MonetaryValue);
                        upsShipment.StandardTax = Convert.ToDecimal(rate.RatedShipment[0].TaxCharges);
                    } else
                    {
                        upsShipment.StandardTotal = Convert.ToDecimal(rate.RatedShipment[0].TotalCharges.MonetaryValue);
                        upsShipment.StandardTax = 0;
                    }
                    upsShipment.StandardCost = upsShipment.StandardTotal - upsShipment.StandardTax;
                } else
                {
                    upsShipment.StandardCost = null;
                    upsShipment.StandardTax = null;
                    upsShipment.StandardTotal = null;
                }                
            }
            if(shipmentResponse.ShipmentResults.NegotiatedRateCharges != null)
            {
                if (Convert.ToDecimal(shipmentResponse.ShipmentResults.NegotiatedRateCharges.TotalCharge.MonetaryValue) > 0)
                {
                    if (shipmentResponse.ShipmentResults.NegotiatedRateCharges.TaxCharges != null)
                    {
                        upsShipment.NegotiatedTotal = Convert.ToDecimal(shipmentResponse.ShipmentResults.NegotiatedRateCharges.TotalChargesWithTaxes.MonetaryValue);
                        upsShipment.NegotiatedTax = Convert.ToDecimal(shipmentResponse.ShipmentResults.NegotiatedRateCharges.TaxCharges);
                    }
                    else
                    {
                        upsShipment.NegotiatedTotal = Convert.ToDecimal(shipmentResponse.ShipmentResults.NegotiatedRateCharges.TotalCharge.MonetaryValue);
                        upsShipment.NegotiatedTax = 0;
                    }
                    upsShipment.NegotiatedCost = upsShipment.NegotiatedTotal - upsShipment.NegotiatedTax;
                }
                else
                {
                    upsShipment.NegotiatedCost = null;
                    upsShipment.NegotiatedTax = null;
                    upsShipment.NegotiatedTotal = null;
                }
            } else
            {
                upsShipment.NegotiatedCost = null;
                upsShipment.NegotiatedTax = null;
                upsShipment.NegotiatedTotal = null;
            }
            upsShipment.NumberOfPackages = shipmentResponse.ShipmentResults.PackageResults.Length;
            upsShipment.Id = StoreUPSShipmentInDB(upsShipment, userId);
            UpdateEPMSWithShipmentInfo(upsShipment);
            return upsShipment;
        }
        private List<UPSPackage> SetUPSPackages(ShipmentRequest shipmentRequest, ShipmentResponse shipmentResponse, UPSShipment upsShipment, JobShipment shipment)
        {
            List<UPSPackage> packages = new List<UPSPackage>();

            for (var i = 0; i < shipmentResponse.ShipmentResults.PackageResults.Length; i++)
            {
                string filePath = @"someFilePath";
                string fileExtension = shipmentRequest.LabelSpecification.LabelImageFormat.Code.ToLower();
                string fileName = shipmentResponse.ShipmentResults.PackageResults[i].TrackingNumber + "." + fileExtension;

                UPSPackage upsPackage = new UPSPackage();
                upsPackage.ShipmentId = upsShipment.Id;
                upsPackage.LabelFileName = fileName;
                upsPackage.LabelFileBlob = Convert.FromBase64String(shipmentResponse.ShipmentResults.PackageResults[i].ShippingLabel.GraphicImage);
                upsPackage.TrackingNumber = shipmentResponse.ShipmentResults.PackageResults[i].TrackingNumber;
                upsPackage.ShipTime = DateTime.Now;
                upsPackage.Status = "Shipped";
                upsPackage.Weight = Convert.ToSingle(shipmentRequest.Shipment.Package[i].PackageWeight.Weight);
                if (upsShipment.StandardTotal != null)
                {
                    upsPackage.StandardCost = upsShipment.StandardCost / shipmentResponse.ShipmentResults.PackageResults.Length;
                    upsPackage.StandardTax = upsShipment.StandardTax / shipmentResponse.ShipmentResults.PackageResults.Length;
                    upsPackage.StandardTotal = upsShipment.StandardTotal / shipmentResponse.ShipmentResults.PackageResults.Length;                    
                }
                else
                {
                    upsPackage.StandardCost = null;
                    upsPackage.StandardTax = null;
                    upsPackage.StandardTotal = null;
                }
                if (upsShipment.NegotiatedTotal != null)
                {
                    upsPackage.NegotiatedCost = upsShipment.NegotiatedCost / shipmentResponse.ShipmentResults.PackageResults.Length;
                    upsPackage.NegotiatedTax = upsShipment.NegotiatedTax / shipmentResponse.ShipmentResults.PackageResults.Length;
                    upsPackage.NegotiatedTotal = upsShipment.NegotiatedTotal / shipmentResponse.ShipmentResults.PackageResults.Length;
                }
                else
                {
                    upsPackage.NegotiatedCost = null;
                    upsPackage.NegotiatedTax = null;
                    upsPackage.NegotiatedTotal = null;
                }
                StoreUPSPAckageinDB(upsPackage);

                File.WriteAllBytes(filePath + fileName, Convert.FromBase64String(shipmentResponse.ShipmentResults.PackageResults[i].ShippingLabel.GraphicImage));

                CreateEPMSPackage(upsShipment, upsPackage, shipment);
                
                byte[] labelbytes = Convert.FromBase64String(shipmentResponse.ShipmentResults.PackageResults[i].ShippingLabel.GraphicImage);
                string labelString = Encoding.UTF8.GetString(labelbytes);
                RawPrinterHelper.SendStringToPrinter(shipment.PrinterName, labelString);//Prod
                packages.Add(upsPackage);
            }
            return packages;
        }
        private string GetPrinterLanguage(string printerName)
        {
            string lang = "";
            using (SqlConnection sqlconn = new SqlConnection(connName))
            {
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM Printers WHERE PrinterName = @PrinterName", sqlconn))
                {
                    sqlconn.Open();
                    cmd.Parameters.Add("@PrinterName", SqlDbType.VarChar).Value = printerName;
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            lang = rdr["Language"].ToString();
                        }
                    }
                }
            }
            return lang;
        }
    }
}
