using Enterprise.DataServer;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Services.Protocols;
using WBGLibrary;
using WBGLibrary.FedexShip;
using WBGLibrary.FedexTrack;
using GemBox.Spreadsheet;
using Enterprise.Business.OrderEntryBL;
using System.Net.Mail;

namespace WBG_EPMSAPI.Controllers
{
    [System.Web.Http.RoutePrefix("api/Fedex")]
    public class FedexController : ApiController
    {
        string connName = @"Connection String";        
        string connNameTwo = @"Other Connection String";
        cDataServer cDataServer = new cDataServer();
        //This call creates a shipment and prints a label.
        [System.Web.Http.Route("Ship")]
        [System.Web.Http.HttpPost]
        public WBGLibrary.Response<JobShipment> Ship([FromBody]WBGLibrary.GenericClass<JobShipment> GC)
        {
            WBGLibrary.Response<JobShipment> response = new WBGLibrary.Response<JobShipment>();
            JobShipment shipment = GC.Model;                        
            shipment.errorText = new List<string>();
            shipment.LabelFiles = new List<ShippingLabel>();
            foreach (JobCarton carton in shipment.Cartons)
            {
                shipment = Send(shipment, carton, new Guid(GC.myLogin.User));
            }
            //This will need to be enabled once we are ready to go live.
            if (shipment.errorText.Count == 0)
            {
                shipment = UpdateJobWithShippingInfo(shipment);
                if(shipment.epmsCustomerNumber != "customerNum")
                {
                    JobsController.CloseOutJob(shipment.JobNumber);                                    
                }
            }
            response.ReturnedResult = shipment;
            response.Successful = true;
            return response;
        }
        //This call gets the country codes table
        [System.Web.Http.Route("GetCountryCodes")]
        [System.Web.Http.HttpPost]
        public WBGLibrary.ResponseList<SelectListItem> GetCountryCodes([FromBody]WBGLibrary.GenericClass GC)
        {
            WBGLibrary.ResponseList<SelectListItem> response = new WBGLibrary.ResponseList<SelectListItem>();
            List<SelectListItem> list = new List<SelectListItem>();
            GC.myLogin.SystemTrace.mySystems.Add(new mySystem { Name = this.GetType().Namespace, Class = this.GetType().Name, Function = LogTrace.GetCurrentMethod() });
            using (SqlConnection sqlconn = new SqlConnection(connNameTwo))
            {
                using (SqlCommand cmd = new SqlCommand("SELECT fullName, twoChar FROM WI_Util.dbo._ISO_Country_Codes", sqlconn))
                {
                    sqlconn.Open();
                    try
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                if (rdr["fullName"] != null && rdr["fullName"].ToString() != "")
                                {
                                    SelectListItem item = new SelectListItem();
                                    item.Text = rdr["fullName"].ToString();
                                    item.Value = rdr["twoChar"].ToString();
                                    list.Add(item);
                                }
                            }
                        }
                    }
                    catch(Exception ex) {
                        response.Error = ex.Message;
                    }                    
                }
            }
            response.ReturnedResults = list;
            response.Successful = true;
            return response;
        }
        //This call gets all fedex packages that have not been picked up
        [System.Web.Http.Route("GetCurrentPackages")]
        [System.Web.Http.HttpPost]
        public ResponseList<FedexPackage> GetCurrentPackages([FromBody]GenericClass GC)
        {
            ResponseList<FedexPackage> response = new ResponseList<FedexPackage>();
            GC.myLogin.SystemTrace.mySystems.Add(new mySystem { Name = this.GetType().Namespace, Class = this.GetType().Name, Function = LogTrace.GetCurrentMethod() });
            response.ReturnedResults = GetUpdatedCurrentShipments();
            response.Successful = true;
            return response;
        }
        //This call gets a list of fedex packages for a single job
        [System.Web.Http.Route("GetPackagesByJobId")]
        [System.Web.Http.HttpPost]
        public ResponseList<FedexPackage> GetPackagesByJobId([FromBody]GenericClass GC)
        {
            ResponseList<FedexPackage> response = new ResponseList<FedexPackage>();
            GC.myLogin.SystemTrace.mySystems.Add(new mySystem { Name = this.GetType().Namespace, Class = this.GetType().Name, Function = LogTrace.GetCurrentMethod() });
            using (SqlConnection sqlconn = new SqlConnection(connNameTwo))
            {
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM WI_Util.dbo.Fedex_Labels WHERE OrderNumber = @OrderNumber", sqlconn))
                {
                    sqlconn.Open();
                    cmd.Parameters.Add("@OrderNumber", SqlDbType.VarChar).Value = GC.Str;
                    try
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                response.ReturnedResults.Add(new FedexPackage {
                                    Id = Convert.ToInt32(rdr["Id"]),
                                    FileName = rdr["FileName"].ToString(),
                                    FileBlob = rdr["FileBlob"].ToString(),
                                    TrackingNumber = rdr["TrackingNumber"].ToString(),
                                    MasterTrackingNumber = rdr["MasterTrackingNumber"].ToString(),
                                    OrderNumber = rdr["OrderNumber"].ToString(),                                                                           
                                    AccountNumber = rdr["AccountNumber"].ToString(),
                                    TrackingIDType = rdr["TrackingIDType"].ToString(),
                                    ShipTime = Convert.ToDateTime(rdr["ShipTime"]),
                                    Status = rdr["Status"].ToString()

                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        response.Error = ex.Message;
                    }
                }
            }
            response.Successful = true;
            return response;
        }       
        //This call gets the EPMS shipment using the Job Number
        [System.Web.Http.Route("GetShipmentsByJobID")]
        [System.Web.Http.HttpPost]
        public ResponseList<JobShipment> GetShipmentsByJobID([FromBody]GenericClass<JobShipment> GC)
        {            
            ResponseList<JobShipment> response = new ResponseList<JobShipment>();
            GC.myLogin.SystemTrace.mySystems.Add(new mySystem { Name = this.GetType().Namespace, Class = this.GetType().Name, Function = LogTrace.GetCurrentMethod() });            
            if (GC.Model != null)
            {
                try
                {
                    using (SqlConnection sqlconn = new SqlConnection(connName))
                    {
                        using (SqlCommand cmd = new SqlCommand("SELECT Shipment.Shipped, Shipment.UserDefined1, Shipment.UserDefined2, Shipment.UserDefined3, Shipment.UserDefined4, Shipment.UserDefined5, OrderHeader.CustAccount, Shipment.ShipmentNumber, Shipment.ShipName, Shipment.ShipAddress1, Shipment.ShipAddress2, Shipment.ShipCity, Shipment.ShipState, Shipment.ShipZip, Shipment.ShipCountry, Shipment.ShipInNameOf, Shipment.ShipAddress3, Shipment.ShipPhone, Shipment.Email,Shipment.ShipVia, Shipment.ShipViaService, Shipment.ThirdPartyBilling, Shipment.UserDefined2, OrderHeader.JobDescription FROM Shipment INNER JOIN OrderHeader ON Shipment.JobNumber = OrderHeader.JobNumber WHERE Shipment.JobNumber = @JobNumber", sqlconn))
                        {
                            cmd.Parameters.Add("@JobNumber", SqlDbType.VarChar, 50).Value = GC.Model.JobNumber;
                            sqlconn.Open();
                            using (SqlDataReader rdr = cmd.ExecuteReader())
                            {                                
                                while (rdr.Read())
                                {
                                    if(rdr["ShipmentNumber"].ToString() != null && rdr["ShipmentNumber"].ToString() != string.Empty)
                                    {
                                        if(rdr["ShipAddress1"].ToString() != "SEE DISTRO")
                                        {
                                            JobShipment shipment = new JobShipment();
                                            shipment.Location = GC.Model.Location;
                                            shipment.errorText = new List<string>();
                                            shipment.epmsCustomerNumber = rdr["CustAccount"].ToString();
                                            shipment.ShipmentNumber = rdr["ShipmentNumber"].ToString();
                                            shipment.JobNumber = GC.Model.JobNumber;
                                            shipment.ShipName = rdr["ShipName"].ToString();
                                            shipment.ShipInNameOf = rdr["ShipInNameOf"].ToString();
                                            shipment.ShipAddress1 = rdr["ShipAddress1"].ToString();
                                            shipment.ShipAddress2 = rdr["ShipAddress2"].ToString();
                                            shipment.ShipCity = rdr["ShipCity"].ToString();
                                            shipment.ShipState = rdr["ShipState"].ToString();
                                            shipment.ShipZip = rdr["ShipZip"].ToString();
                                            shipment.ShipCountry = rdr["ShipCountry"].ToString();
                                            shipment.ShipPhone = rdr["ShipPhone"].ToString();
                                            shipment.ShipEmail = rdr["Email"].ToString();
                                            shipment.ServiceLevel = rdr["ShipViaService"].ToString();
                                            shipment.ThirdPartyBilling = rdr["ThirdPartyBilling"].ToString();
                                            shipment.CarrierID = rdr["ShipVia"].ToString();

                                            shipment = GetPackagesByShippingID(shipment);
                                            if (Convert.ToInt32(rdr["Shipped"].ToString()) == 1)
                                            {
                                                shipment.errorText.Add("This shipment has aleady been shipped. If this is incorrect, please contact the person who entered this order into EPMS.");
                                            }
                                            if (shipment.epmsCustomerNumber == "customerNum")
                                            {
                                                shipment.InvoiceNumber = rdr["UserDefined2"].ToString();
                                                shipment = AlaskaServiceTypes(shipment);
                                                shipment.CustomerReference = GC.Model.JobNumber;
                                            }
                                            if (shipment.epmsCustomerNumber == "customerNum")
                                            {
                                                cJobHeader original = new cJobHeader();
                                                original.Load("ORDER", shipment.JobNumber);
                                                shipment.OutsideOrderNumber = original.PONumber;
                                                shipment = TSGServiceTypes(shipment);
                                                shipment.ShipCountry = "US";
                                                shipment.ShipPhone = "9999999999";
                                            }
                                            if(shipment.CustomerReference == "" || shipment.CustomerReference == null)
                                            {
                                                if(rdr["JobDescription"].ToString().Length > 40)
                                                {
                                                    shipment.CustomerReference = rdr["JobDescription"].ToString().Substring(0,40);
                                                } else
                                                {
                                                    shipment.CustomerReference = rdr["JobDescription"].ToString();
                                                }                                                
                                            }
                                            shipment = GetReturnAddress(shipment);
                                            if (String.IsNullOrEmpty(shipment.ReturnAddress1))
                                            {
                                                string[] cityStateZip = rdr["UserDefined3"]?.ToString().Split(',');
                                                if (!(cityStateZip == null || cityStateZip.Length != 3 || cityStateZip[0] == ""))
                                                {
                                                    shipment.ReturnCompany = rdr["UserDefined1"]?.ToString() ?? "";
                                                    shipment.ReturnAddress1 = rdr["UserDefined2"]?.ToString() ?? "";
                                                    shipment.ReturnCity = cityStateZip?[0];
                                                    shipment.ReturnState = cityStateZip?[1];
                                                    shipment.ReturnZip = cityStateZip?[2];
                                                    shipment.ReturnPhone = rdr["UserDefined5"]?.ToString() ?? "";
                                                }
                                                else
                                                {
                                                    shipment.ReturnCompany = "Shipping";
                                                    if (GC.Model.Location == "Burnside")
                                                    {
                                                        shipment.ReturnAddress1 = "Default Address";
                                                        shipment.ReturnCity = "Default City";
                                                        shipment.ReturnState = "Default State";
                                                        shipment.ReturnZip = "Default Zip";
                                                        shipment.ReturnPhone = "Default Phone";
                                                    }
                                                    if (GC.Model.Location == "Kent")
                                                    {
                                                        shipment.ReturnAddress1 = "Default Address";
                                                        shipment.ReturnCity = "Default City";
                                                        shipment.ReturnState = "Default State";
                                                        shipment.ReturnZip = "Default Zip";
                                                        shipment.ReturnPhone = "Default Phone";
                                                    }
                                                    if (GC.Model.Location == "Portland Warehouse")
                                                    {
                                                        shipment.ReturnAddress1 = "Default Address";
                                                        shipment.ReturnCity = "Default City";
                                                        shipment.ReturnState = "Default State";
                                                        shipment.ReturnZip = "Default Zip";
                                                        shipment.ReturnPhone = "Default Phone";
                                                    }
                                                    if (GC.Model.Location == "Corporate")
                                                    {
                                                        shipment.ReturnName = "Rhonda";
                                                        shipment.ReturnAddress1 = "Default Address";
                                                        shipment.ReturnCity = "Default City";
                                                        shipment.ReturnState = "Default State";
                                                        shipment.ReturnZip = "Default Zip";
                                                        shipment.ReturnPhone = "Default Phone";
                                                    }
                                                    if (GC.Model.Location == "Chino")
                                                    {
                                                        shipment.ReturnAddress1 = "Default Address";
                                                        shipment.ReturnCity = "Default City";
                                                        shipment.ReturnState = "Default State";
                                                        shipment.ReturnZip = "Default Zip";
                                                        shipment.ReturnPhone = "Default Phone";
                                                    }
                                                }
                                            }
                                            if (String.IsNullOrEmpty(shipment.ReturnPhone)) shipment.ReturnPhone = "9999999999";
                                            if (String.IsNullOrEmpty(shipment.ShipPhone)) shipment.ShipPhone = "9999999999";
                                            if (shipment.CarrierID.ToLower() == "fedx3" || shipment.CarrierID.ToLower() == "fedex" || shipment.CarrierID.ToLower() == "op")
                                            {
                                                if (shipment.CarrierID == "FEDX3")
                                                {
                                                    switch (rdr["ShipViaService"]?.ToString() ?? "")                                                        
                                                    {
                                                        case "2D-AM":
                                                            shipment.ServiceLevel = "FEDEX_2_DAY_AM";
                                                            break;
                                                        case "2DAY":
                                                            shipment.ServiceLevel = "FEDEX_2_DAY";
                                                            break;
                                                        case "2DF":
                                                            shipment = new JobShipment();
                                                            shipment.errorText = new List<string>();
                                                            shipment.errorText.Add("This is a freight order and is not compatible with this system.");
                                                            break;
                                                        case "FIRST":
                                                            shipment.ServiceLevel = "FIRST_OVERNIGHT";
                                                            break;
                                                        case "GRND":
                                                            shipment.ServiceLevel = "FEDEX_GROUND";
                                                            break;
                                                        case "IPR":
                                                            shipment = new JobShipment();
                                                            shipment.errorText = new List<string>();
                                                            shipment.errorText.Add("This is an international order and is not compatible with this system.");
                                                            break;
                                                        case "POVR":
                                                            shipment.ServiceLevel = "PRIORITY_OVERNIGHT";
                                                            break;
                                                        case "SOVR":
                                                            shipment.ServiceLevel = "STANDARD_OVERNIGHT";
                                                            break;
                                                    }
                                                }
                                                if (shipment.CarrierID == "OP")
                                                {
                                                    switch (rdr["ShipViaService"]?.ToString() ?? "")
                                                    {
                                                        case "2303":
                                                            shipment.ServiceLevel = "FEDEX_2_DAY_AM";
                                                            break;
                                                        case "2005":
                                                            shipment.ServiceLevel = "FEDEX_2_DAY";
                                                            break;
                                                        case "2008":
                                                            shipment = new JobShipment();
                                                            shipment.errorText = new List<string>();
                                                            shipment.errorText.Add("This is a freight order and is not compatible with this system.");
                                                            break;
                                                        case "2302":
                                                            shipment.ServiceLevel = "FIRST_OVERNIGHT";
                                                            break;
                                                        case "2101":
                                                            shipment.ServiceLevel = "FEDEX_GROUND";
                                                            break;
                                                        case "2301":
                                                            shipment = new JobShipment();
                                                            shipment.errorText = new List<string>();
                                                            shipment.errorText.Add("This is an international order and is not compatible with this system.");
                                                            break;
                                                        case "2003":
                                                            shipment.ServiceLevel = "PRIORITY_OVERNIGHT";
                                                            break;
                                                        case "2004":
                                                            shipment.ServiceLevel = "STANDARD_OVERNIGHT";
                                                            break;
                                                        case "1916":
                                                            shipment.ServiceLevel = "Will Call";
                                                            break;
                                                    }
                                                }
                                                if (shipment.CarrierID == "FEDEX")
                                                {
                                                    switch (rdr["ShipViaService"]?.ToString() ?? "")
                                                    {
                                                        case "2DAY":
                                                            shipment.ServiceLevel = "FEDEX_2_DAY";
                                                            break;
                                                        case "COLL":
                                                            shipment = new JobShipment();
                                                            shipment.errorText = new List<string>();
                                                            shipment.errorText.Add("This is a collect order and is not compatible with this system.");
                                                            break;
                                                        case "FRT":
                                                            shipment = new JobShipment();
                                                            shipment.errorText = new List<string>();
                                                            shipment.errorText.Add("This is a freight order and is not compatible with this system.");
                                                            break;
                                                        case "NDA":
                                                            shipment.ServiceLevel = "FEDEX_GROUND";
                                                            break;
                                                        case "2303":
                                                            shipment.ServiceLevel = "FEDEX_2_DAY_AM";
                                                            break;
                                                        case "2005":
                                                            shipment.ServiceLevel = "FEDEX_2_DAY";
                                                            break;
                                                        case "2008":
                                                            shipment = new JobShipment();
                                                            shipment.errorText = new List<string>();
                                                            shipment.errorText.Add("This is a freight order and is not compatible with this system.");
                                                            break;
                                                        case "2302":
                                                            shipment.ServiceLevel = "FIRST_OVERNIGHT";
                                                            break;
                                                        case "2101":
                                                            shipment.ServiceLevel = "FEDEX_GROUND";
                                                            break;
                                                        case "2301":
                                                            shipment = new JobShipment();
                                                            shipment.errorText = new List<string>();
                                                            shipment.errorText.Add("This is an international order and is not compatible with this system.");
                                                            break;
                                                        case "2003":
                                                            shipment.ServiceLevel = "PRIORITY_OVERNIGHT";
                                                            break;
                                                        case "2004":
                                                            shipment.ServiceLevel = "STANDARD_OVERNIGHT";
                                                            break;
                                                        case "1916":
                                                            shipment.ServiceLevel = "Will Call";
                                                            break;
                                                    }
                                                }
                                                response.ReturnedResults.Add(shipment);
                                            } else
                                            {
                                                shipment = new JobShipment();
                                                shipment.errorText = new List<string>();
                                                shipment.errorText.Add("This order is not meant to be shipped Fedex. Please check the shipping method. If this is an error, it should be changed in EPMS by the CSR.");
                                                response.ReturnedResults.Add(shipment);
                                            }
                                        } else
                                        {
                                            response.ReturnedResults = GetMultipleShipments(GC.Model.JobNumber, GC.Model.Location);
                                        }                                                                            
                                    }                                    
                                }
                            }
                            sqlconn.Close();
                            response.Successful = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    response.Successful = false;
                }                
            }
            return response;
        }        
        //This call deletes a single fedex package
        [System.Web.Http.Route("DeleteShipment")]
        [System.Web.Http.HttpPost]
        public Response<myString> DeleteShipment([FromBody]GenericClass GC)
        {
            Response<myString> response = new Response<myString>();
            GC.myLogin.SystemTrace.mySystems.Add(new mySystem { Name = this.GetType().Namespace, Class = this.GetType().Name, Function = LogTrace.GetCurrentMethod() });
            FedexPackage package = GetPackagesById(Convert.ToInt32(GC.Str));
            response.ReturnedResult.rtnStr = DeleteShipment(package.TrackingNumber, package.TrackingIDType);
            response.Successful = true;
            return response;
        }
        //This call gets all fedex packages that have been picked up by Fedex or are cancelled.
        [System.Web.Http.Route("GetUpdatedNonCurrentShipments")]
        [System.Web.Http.HttpPost]
        public ResponseList<FedexPackage> GetUpdatedNonCurrentShipments([FromBody]GenericClass GC)
        {
            ResponseList<FedexPackage> response = new ResponseList<FedexPackage>();
            GC.myLogin.SystemTrace.mySystems.Add(new mySystem { Name = this.GetType().Namespace, Class = this.GetType().Name, Function = LogTrace.GetCurrentMethod() });
            List<FedexPackage> currentFedexPackages = GetCurrentShipments();
            UpdateShipments(currentFedexPackages);
            response.ReturnedResults = GetCompletedShipments();
            response.Successful = true;
            return response;
        }               
        private List<FedexPackage> GetUpdatedCurrentShipments()
        {
            List<FedexPackage> packages = GetCurrentShipments();            
            UpdateShipments(packages);
            packages = GetCurrentShipments();
            return packages;
        }
        private List<FedexPackage> GetCurrentShipments()
        {
            List<FedexPackage> packages = new List<FedexPackage>();
            using (SqlConnection sqlconn = new SqlConnection(connNameTwo))
            {
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM WI_Util.dbo.Fedex_Labels WHERE Status = 'Current'", sqlconn))
                {
                    sqlconn.Open();
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            packages.Add(new FedexPackage
                            {
                                Id = Convert.ToInt32(rdr["Id"]),
                                FileName = rdr["FileName"].ToString(),
                                FileBlob = rdr["FileBlob"].ToString(),
                                TrackingNumber = rdr["TrackingNumber"].ToString(),
                                MasterTrackingNumber = rdr["MasterTrackingNumber"].ToString(),
                                OrderNumber = rdr["OrderNumber"].ToString(),
                                AccountNumber = rdr["AccountNumber"].ToString(),
                                TrackingIDType = rdr["TrackingIDType"].ToString(),
                                ShipTime = Convert.ToDateTime(rdr["ShipTime"]),
                                Status = rdr["Status"].ToString()

                            });
                        }
                    }
                }
            }
            return packages;
        }
        private List<FedexPackage> GetCompletedShipments()
        {
            List<FedexPackage> packages = new List<FedexPackage>();
            using (SqlConnection sqlconn = new SqlConnection(connNameTwo))
            {
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM WI_Util.dbo.Fedex_Labels WHERE Status <> 'Current'", sqlconn))
                {
                    sqlconn.Open();
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            packages.Add(new FedexPackage
                            {
                                Id = Convert.ToInt32(rdr["Id"]),
                                FileName = rdr["FileName"].ToString(),
                                FileBlob = rdr["FileBlob"].ToString(),
                                TrackingNumber = rdr["TrackingNumber"].ToString(),
                                MasterTrackingNumber = rdr["MasterTrackingNumber"].ToString(),
                                OrderNumber = rdr["OrderNumber"].ToString(),
                                AccountNumber = rdr["AccountNumber"].ToString(),
                                TrackingIDType = rdr["TrackingIDType"].ToString(),
                                ShipTime = Convert.ToDateTime(rdr["ShipTime"]),
                                Status = rdr["Status"].ToString()

                            });
                        }
                    }
                }
            }
            return packages;
        }
        //------------------------------------- Fedex Tracking Methods-------------------------------------------
        private void UpdateShipments(List<FedexPackage> packages)
        {            
            foreach(FedexPackage package in packages)
            {

                bool completed = HasPackageBeenPickedUp(package.TrackingNumber);
                if(completed)
                {
                    using (SqlConnection sqlconn = new SqlConnection(connNameTwo))
                    {
                        using (SqlCommand cmd = new SqlCommand("UPDATE WI_UTIL.dbo.Fedex_Labels SET Status = 'Completed' WHERE TrackingNumber = @TrackingNumber", sqlconn))
                        {
                            sqlconn.Open();
                            cmd.Parameters.Add("@TrackingNumber", SqlDbType.VarChar).Value = package.TrackingNumber;
                            try
                            {
                                cmd.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.Message);
                            }
                        }
                    }
                }
            }
        }
        private static bool HasPackageBeenPickedUp(string trackingNumber)
        {
            TrackReply reply = TrackPackage(trackingNumber);
            TrackEvent[] events = reply.CompletedTrackDetails[0].TrackDetails[0].Events;
            //This is just for testing for now, when there is live data, every tracking detail should contain events.
            if (events == null)
            {
                try
                {
                    if (reply.CompletedTrackDetails[0].TrackDetails[0].DatesOrTimes != null)
                    {
                        if (reply.CompletedTrackDetails[0].TrackDetails[0].DatesOrTimes.Where(x => x.Type == TrackingDateOrTimestampType.ACTUAL_DELIVERY).Count() > 0)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    } else
                    {
                        if(reply.CompletedTrackDetails[0].TrackDetails[0].Notification.Code != null)
                        {
                            if (reply.CompletedTrackDetails[0].TrackDetails[0].Notification.Code == "9040")
                            {
                                string connNameTwo = @"Connection String";
                                using (SqlConnection sqlconn = new SqlConnection(connNameTwo))
                                {
                                    using (SqlCommand cmd = new SqlCommand("UPDATE WI_UTIL.dbo.Fedex_Labels SET Status = 'Cancelled' WHERE TrackingNumber = @TrackingNumber", sqlconn))
                                    {
                                        sqlconn.Open();
                                        cmd.Parameters.Add("@TrackingNumber", SqlDbType.VarChar).Value = trackingNumber;
                                        try
                                        {
                                            cmd.ExecuteNonQuery();
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine(ex.Message);
                                        }
                                    }
                                }
                                return false;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        return false;
                    }         
                    
                    
                } catch(Exception ex)
                {
                    return false;
                }
                      
            } else
            {
                if (events.Any(x => x.EventType == "PU") || events.Any(x => x.EventType == "DL"))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            
        }
        private static TrackReply TrackPackage(string trackingNumber)
        {
            TrackRequest request = new TrackRequest();
            //
            request.WebAuthenticationDetail = new WBGLibrary.FedexTrack.WebAuthenticationDetail();
            request.WebAuthenticationDetail.UserCredential = new WBGLibrary.FedexTrack.WebAuthenticationCredential();
            request.WebAuthenticationDetail.UserCredential.Key = "API Key";
            request.WebAuthenticationDetail.UserCredential.Password = "API Password";
            request.WebAuthenticationDetail.ParentCredential = new WBGLibrary.FedexTrack.WebAuthenticationCredential();
            request.WebAuthenticationDetail.ParentCredential.Key = "API Key";
            request.WebAuthenticationDetail.ParentCredential.Password = "API Password";
            //
            request.ClientDetail = new WBGLibrary.FedexTrack.ClientDetail();
            request.ClientDetail.AccountNumber = "API Account Num";
            request.ClientDetail.MeterNumber = "API Meter Num";
            //
            request.Version = new WBGLibrary.FedexTrack.VersionId();
            //
            // Tracking information
            request.SelectionDetails = new TrackSelectionDetail[1] { new TrackSelectionDetail() };
            request.SelectionDetails[0].PackageIdentifier = new TrackPackageIdentifier();
            request.SelectionDetails[0].PackageIdentifier.Value = trackingNumber;

            request.SelectionDetails[0].PackageIdentifier.Type = TrackIdentifierType.TRACKING_NUMBER_OR_DOORTAG;

            //
            // Include detailed scans is optional.
            // If omitted, set to false
            request.ProcessingOptions = new TrackRequestProcessingOptionType[1];
            request.ProcessingOptions[0] = TrackRequestProcessingOptionType.INCLUDE_DETAILED_SCANS;

            TrackService service = new TrackService();
            TrackReply reply = service.track(request);
            return reply;
        }
        //-------------------------------------EPMS Package Methods-----------------------------------------------
        private List<JobShipment> GetMultipleShipments(string jobNum, string location)
        {
            List<JobShipment> shipments = new List<JobShipment>();
            cJobHeader orderHeader = new cJobHeader();
            orderHeader.Load("ORDER", jobNum);
            cShipments epmsShip = new cShipments();
            epmsShip.Load(jobNum, false);
            cShipment epmsShipment = new cShipment();
            foreach (cShipment singleShip in epmsShip)
            {
                epmsShipment = singleShip;
            }
            cPackages epmsPack = new cPackages();
            epmsPack.Load(true, epmsShipment.ShipmentNumber);
            cPackage epmsPackage = new cPackage();
            foreach (cPackage singlePack in epmsPack)
            {
                epmsPackage = singlePack;
            }
            try
            {
                SpreadsheetInfo.SetLicense("License Key");
                var wb = ExcelFile.Load(@"file Path");
                var ws = wb.Worksheets[0];
                int rowNum = 0;

                foreach (ExcelRow currentRow in ws.Rows.ToList())
                {
                    rowNum++;
                    if (rowNum == 14)
                    {
                        Console.WriteLine(rowNum);
                    }
                    if (ws.Cells[rowNum, 0].Value != null)
                    {
                        JobShipment shipment = new JobShipment();
                        shipment.errorText = new List<string>();
                        shipment.Location = location;
                        shipment.epmsCustomerNumber = orderHeader.CustAccount;
                        shipment.ShipmentNumber = epmsShipment.ShipmentNumber.ToString();
                        shipment.JobNumber = jobNum;
                        shipment.ShipName = ws.Cells[rowNum, 6].Value.ToString().Trim();
                        if(ws.Cells[rowNum, 8].Value != null)
                        {
                            shipment.ShipInNameOf = ws.Cells[rowNum, 8].Value.ToString().Trim();
                        }                        
                        shipment.ShipAddress1 = ws.Cells[rowNum, 7].Value.ToString().Trim();
                        shipment.ShipCity = ws.Cells[rowNum, 9].Value.ToString().Trim();
                        shipment.ShipState = ws.Cells[rowNum, 10].Value.ToString().Trim();
                        shipment.ShipZip = ws.Cells[rowNum, 11].Value.ToString().Trim();
                        shipment.ShipCountry = "US";
                        shipment.ShipPhone = ws.Cells[rowNum, 12].Value.ToString().Trim().Replace("-", "");
                        shipment.ServiceLevel = epmsPackage.ShipViaService;
                        shipment.ThirdPartyBilling = epmsShipment.ThirdPartyBilling;
                        shipment.CarrierID = epmsPackage.ShipMethod;
                        shipment = GetPackagesByShippingID(shipment);
                        shipment.PONumber = ws.Cells[rowNum, 1].Value.ToString().Trim();
                        if (epmsShipment.Shipped)
                        {
                            shipment.errorText.Add("This shipment has aleady been shipped. If this is incorrect, please contact the person who entered this order into EPMS.");
                        }
                        shipment.OutsideOrderNumber = orderHeader.PONumber;
                        shipment = TSGServiceTypes(shipment);
                        shipment = GetReturnAddress(shipment);
                        if (shipment.CarrierID == "FEDX3" || shipment.CarrierID == "OP")
                        {
                            shipments.Add(shipment);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return shipments;
        }
        private JobShipment GetPackagesByShippingID(JobShipment shipment)
        {
            shipment.JobPackages = new List<JobPackage>();
            using (SqlConnection sqlConn = new SqlConnection(connName))
            {
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM Package WHERE ShipmentNumber=@ShipmentNumber", sqlConn))
                {
                    cmd.Parameters.Add("@ShipmentNumber", SqlDbType.VarChar, 50).Value = shipment.ShipmentNumber;
                    sqlConn.Open();
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            JobPackage jps = new JobPackage();
                            jps.ItemID = String.IsNullOrEmpty(rdr["UserDefined1"].ToString()) ? "~" : rdr["UserDefined1"].ToString();
                            if(shipment.epmsCustomerNumber == "196362")
                            {
                                shipment.PONumber = rdr["UserDefined2"].ToString();
                            }
                            jps.Description = String.IsNullOrEmpty(rdr["Description"].ToString()) ? "~" : rdr["Description"].ToString();
                            jps.Quantity =Convert.ToInt32(rdr["Quantity"].ToString());
                            jps.TotalQtyShipped = Convert.ToInt32(rdr["TotalQtyShipped"].ToString());
                            jps.FreightCost = float.Parse(rdr["FreightCost"].ToString());
                            jps.Weight = float.Parse(rdr["Weight"].ToString());
                            jps.PackageID = rdr["PackageID"].ToString();
                            shipment.JobPackages.Add(jps);
                        }
                    }
                    sqlConn.Close();
                }

                var cnt = 0;
                foreach (JobPackage pkg in shipment.JobPackages)
                {
                    cnt++;                    
                    sqlConn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT MaterialCode, UnitDescription FROM Material WHERE MaterialCode IN(SELECT FinGoodCode FROM OrderComponent WHERE JobNumber= @JobNumber AND ComponentNumber = @PkgLineNumber)", sqlConn))
                    {
                        cmd.Parameters.Add("@JobNumber", SqlDbType.VarChar, 50).Value = shipment.JobNumber;
                        cmd.Parameters.Add("@PkgLineNumber", SqlDbType.VarChar, 50).Value = cnt;
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                pkg.OutsideItemID = rdr["MaterialCode"].ToString();
                            }
                        }
                    }
                    sqlConn.Close();
                }
            }
            return shipment;
        }
        private JobShipment GetReturnAddress(JobShipment shipment)
        {
            using (SqlConnection sqlConn = new SqlConnection(connNameTwo))
            {
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM WI_UTIL.dbo.ReturnAddresses WHERE CustomerId = @CustomerId AND Location = @Location", sqlConn))
                {
                    cmd.Parameters.Add("@CustomerId", SqlDbType.Int).Value = shipment.epmsCustomerNumber;
                    cmd.Parameters.Add("@Location", SqlDbType.VarChar, 50).Value = shipment.Location;
                    sqlConn.Open();
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            shipment.ReturnName = String.IsNullOrEmpty(rdr["ShipFromName"].ToString()) ? "" : rdr["ShipFromName"].ToString();
                            shipment.ReturnCompany = String.IsNullOrEmpty(rdr["CompanyName"].ToString()) ? "" : rdr["CompanyName"].ToString();
                            shipment.ReturnAddress1 = String.IsNullOrEmpty(rdr["Address1"].ToString()) ? "" : rdr["Address1"].ToString();
                            shipment.ReturnAddress2 = String.IsNullOrEmpty(rdr["Address2"].ToString()) ? "" : rdr["Address2"].ToString();
                            shipment.ReturnAddress3 = String.IsNullOrEmpty(rdr["Address3"].ToString()) ? "" : rdr["Address3"].ToString();
                            shipment.ReturnCity = String.IsNullOrEmpty(rdr["City"].ToString()) ? "" : rdr["City"].ToString();
                            shipment.ReturnState = String.IsNullOrEmpty(rdr["State"].ToString()) ? "" : rdr["State"].ToString();
                            shipment.ReturnZip = String.IsNullOrEmpty(rdr["Zip"].ToString()) ? "" : rdr["Zip"].ToString();
                            shipment.ReturnCountry = String.IsNullOrEmpty(rdr["Country"].ToString()) ? "" : rdr["Country"].ToString();
                            shipment.ReturnPhone = String.IsNullOrEmpty(rdr["Phone"].ToString()) ? "" : rdr["Phone"].ToString();
                        }
                    }
                    sqlConn.Close();
                }
            }
            return shipment;
        }
        //-------------------------------------Create Fedex Shipment Methods--------------------------------------
        private static JobShipment Send(JobShipment shipment, JobCarton carton, Guid userId)
        {
            // Set this to true to process a COD shipment and print a COD return Label
            bool isCodShipment = false;
            ProcessShipmentRequest request = CreateShipmentRequest(isCodShipment, shipment, carton);            
            ShipService service = new ShipService();                    
            //
            try
            {
                // Call the ship web service passing in a ProcessShipmentRequest and returning a ProcessShipmentReply
                ProcessShipmentReply reply = service.processShipment(request);
                //
                if ((reply.HighestSeverity != WBGLibrary.FedexShip.NotificationSeverityType.ERROR) && (reply.HighestSeverity != WBGLibrary.FedexShip.NotificationSeverityType.FAILURE))
                {
                    if(shipment.NumberOfCartons > 1)
                    {
                        if (carton.SequenceNumber == "1")
                        {
                            shipment.MasterTracking = reply.CompletedShipmentDetail.MasterTrackingId;
                            carton.TrackingNumber = reply.CompletedShipmentDetail.MasterTrackingId.TrackingNumber;
                            shipment.MasterTrackingNumber = reply.CompletedShipmentDetail.MasterTrackingId.TrackingNumber;
                        }
                        else
                        {
                            shipment.Cartons.Where(x => x.SequenceNumber == carton.SequenceNumber).FirstOrDefault().TrackingNumber = reply.CompletedShipmentDetail.CompletedPackageDetails.FirstOrDefault().TrackingIds.FirstOrDefault().TrackingNumber;
                        }
                    } else
                    {
                        shipment.MasterTrackingNumber = reply.CompletedShipmentDetail.CompletedPackageDetails.FirstOrDefault().TrackingIds.FirstOrDefault().TrackingNumber;
                        carton.TrackingNumber = reply.CompletedShipmentDetail.CompletedPackageDetails.FirstOrDefault().TrackingIds.FirstOrDefault().TrackingNumber;
                    }
                    shipment = ShowShipmentReply(isCodShipment, reply, shipment, carton, userId);
                } else
                {
                    foreach(var notifications in reply.Notifications)
                    {
                        shipment.errorText.Add(notifications.Message);
                    }                    
                }
            }
            catch (SoapException e)
            {
                shipment.errorText.Add(e.Detail.InnerText);
            }
            catch (Exception e)
            {
                shipment.errorText.Add(e.Message);
            }
            return shipment;
        }
        private static ProcessShipmentRequest CreateShipmentRequest(bool isCodShipment, JobShipment shipment, JobCarton carton)
        {
            // Build the ShipmentRequest
            ProcessShipmentRequest request = new ProcessShipmentRequest();
            //
            SetShipmentDetails(request, shipment, carton);
            //
            //TODO: Add shipment level info into here. 
            SetPackageLineItems(isCodShipment, request, carton, shipment);
            //
            return request;
        }
        private static WBGLibrary.FedexShip.WebAuthenticationDetail SetWebAuthenticationDetail()
        {
            WBGLibrary.FedexShip.WebAuthenticationDetail wad = new WBGLibrary.FedexShip.WebAuthenticationDetail();
            wad.UserCredential = new WBGLibrary.FedexShip.WebAuthenticationCredential();
            //wad.UserCredential.Key = "API Key"; // Dev
            wad.UserCredential.Key = "API Key"; //Prod
            //wad.UserCredential.Password = "API Password"; // Dev
            wad.UserCredential.Password = "API Password"; //Prod
            wad.ParentCredential = new WBGLibrary.FedexShip.WebAuthenticationCredential();
            //wad.ParentCredential.Key = "API Key"; // Dev
            wad.ParentCredential.Key = "API Key"; // Prod
            //wad.ParentCredential.Password = "API Password"; // Dev   
            wad.ParentCredential.Password = "API Password"; //Prod
            return wad;
        }
        private static void SetShipmentDetails(ProcessShipmentRequest request, JobShipment shipment, JobCarton carton)
        {
            request.WebAuthenticationDetail = SetWebAuthenticationDetail();
            //
            request.ClientDetail = new WBGLibrary.FedexShip.ClientDetail();
            //request.ClientDetail.AccountNumber = "API Account Num"; // Dev
            request.ClientDetail.AccountNumber = "API Account Num"; //Prod
            //request.ClientDetail.MeterNumber = "API Meter Num"; // Dev
            request.ClientDetail.MeterNumber = "API Meter Num"; //Prod
            //
            request.TransactionDetail = new WBGLibrary.FedexShip.TransactionDetail();
            request.TransactionDetail.CustomerTransactionId = "***Express Domestic Ship Request using VC#***"; // The client will get the same value back in the response
            //
            request.Version = new WBGLibrary.FedexShip.VersionId();
            //
            request.RequestedShipment = new RequestedShipment();
            if (carton.SequenceNumber != "1")
            {
                request.RequestedShipment.MasterTrackingId = shipment.MasterTracking;
            }
            request.RequestedShipment.ShipTimestamp = DateTime.Now; // Ship date and time
            request.RequestedShipment.DropoffType = DropoffType.REGULAR_PICKUP;
            WBGLibrary.FedexShip.ServiceType service = (WBGLibrary.FedexShip.ServiceType)Enum.Parse(typeof(WBGLibrary.FedexShip.ServiceType), shipment.ServiceLevel);
            request.RequestedShipment.ServiceType = service; // Service types are STANDARD_OVERNIGHT, PRIORITY_OVERNIGHT, ...
            WBGLibrary.FedexShip.PackagingType packaging = (WBGLibrary.FedexShip.PackagingType)Enum.Parse(typeof(WBGLibrary.FedexShip.PackagingType), shipment.Packaging);
            request.RequestedShipment.PackagingType = packaging; // Packaging type FEDEX_BOK, FEDEX_PAK, FEDEX_TUBE, YOUR_PACKAGING, ...
            //
            if (carton.SequenceNumber == "1")
            {
                request.RequestedShipment.TotalWeight = new WBGLibrary.FedexShip.Weight(); // Total weight information
                request.RequestedShipment.TotalWeight.Value = (decimal)shipment.Weight;
                request.RequestedShipment.TotalWeight.Units = WBGLibrary.FedexShip.WeightUnits.LB;
            }
            //
            request.RequestedShipment.PackageCount = shipment.NumberOfCartons.ToString();
            //
            SetSender(request, shipment);
            //
            SetRecipient(request, shipment);
            //
            SetPayment(request, shipment);
            //
            SetLabelDetails(request, shipment);
            //
            if (shipment.ServiceLevel == "INTERNATIONAL_PRIORITY")
            {
                SetCustomsClearanceDetails(request, shipment);
            }
        }
        private static void SetSender(ProcessShipmentRequest request, JobShipment shipment)
        {
            //For Now this will need to be hard coded until we have a way to store the return address in EPMS
            request.RequestedShipment.Shipper = new Party();
            request.RequestedShipment.Shipper.Contact = new WBGLibrary.FedexShip.Contact();
            request.RequestedShipment.Shipper.Contact.PersonName = shipment.ReturnName;
            request.RequestedShipment.Shipper.Contact.CompanyName = shipment.ReturnCompany;
            request.RequestedShipment.Shipper.Contact.PhoneNumber = shipment.ReturnPhone;
            request.RequestedShipment.Shipper.Address = new WBGLibrary.FedexShip.Address();
            request.RequestedShipment.Shipper.Address.StreetLines = shipment.ReturnAddress.ToArray();
            request.RequestedShipment.Shipper.Address.City = shipment.ReturnCity;
            request.RequestedShipment.Shipper.Address.StateOrProvinceCode = shipment.ReturnState;
            request.RequestedShipment.Shipper.Address.PostalCode = shipment.ReturnZip;
            request.RequestedShipment.Shipper.Address.CountryCode = shipment.ReturnCountry;
        }
        private static void SetRecipient(ProcessShipmentRequest request, JobShipment shipment)
        {
            request.RequestedShipment.Recipient = new Party();
            // TINS is optional(Tax Payer Identification) When needed turn this on.
            //request.RequestedShipment.Recipient.Tins = new TaxpayerIdentification[1];
            //request.RequestedShipment.Recipient.Tins[0] = new TaxpayerIdentification();
            //request.RequestedShipment.Recipient.Tins[0].TinType = TinType.BUSINESS_NATIONAL;
            //request.RequestedShipment.Recipient.Tins[0].Number = "XXX"; // Replace "XXX" with the TIN number
            request.RequestedShipment.Recipient.Contact = new WBGLibrary.FedexShip.Contact();
            request.RequestedShipment.Recipient.Contact.PersonName = shipment.ShipInNameOf;
            request.RequestedShipment.Recipient.Contact.CompanyName = shipment.ShipName;
            request.RequestedShipment.Recipient.Contact.PhoneNumber = shipment.ShipPhone;
            request.RequestedShipment.Recipient.Address = new WBGLibrary.FedexShip.Address();
            //This may need some logic
            request.RequestedShipment.Recipient.Address.StreetLines = shipment.ShipAddress.ToArray();
            request.RequestedShipment.Recipient.Address.City = shipment.ShipCity;
            request.RequestedShipment.Recipient.Address.StateOrProvinceCode = shipment.ShipState;
            request.RequestedShipment.Recipient.Address.PostalCode = shipment.ShipZip;
            request.RequestedShipment.Recipient.Address.CountryCode = shipment.ShipCountry;
            //I wonder if this can be an international shipment?
            if (request.RequestedShipment.ServiceType == WBGLibrary.FedexShip.ServiceType.FEDEX_GROUND)
            {
                //Figure out how to get the full name of the country based on the code. 
                request.RequestedShipment.Recipient.Address.CountryName = "United States of America";
            }
            //This will need some logic added or will always be set to false?
            request.RequestedShipment.Recipient.Address.Residential = false;
        }
        private static void SetPayment(ProcessShipmentRequest request, JobShipment shipment)
        {
            request.RequestedShipment.ShippingChargesPayment = new Payment();
            PaymentType paymentType = (PaymentType)Enum.Parse(typeof(PaymentType), shipment.BillShipmentTo);
            request.RequestedShipment.ShippingChargesPayment.PaymentType = paymentType;
            request.RequestedShipment.ShippingChargesPayment.Payor = new Payor();
            request.RequestedShipment.ShippingChargesPayment.Payor.ResponsibleParty = new Party();
            //request.RequestedShipment.ShippingChargesPayment.Payor.ResponsibleParty.AccountNumber = "Account Num"; // Dev
            request.RequestedShipment.ShippingChargesPayment.Payor.ResponsibleParty.AccountNumber = shipment.ThirdPartyBilling; //Prod
            if (request.RequestedShipment.ServiceType == WBGLibrary.FedexShip.ServiceType.FEDEX_GROUND)
            {
            }
            request.RequestedShipment.ShippingChargesPayment.Payor.ResponsibleParty.Contact = new WBGLibrary.FedexShip.Contact();
            request.RequestedShipment.ShippingChargesPayment.Payor.ResponsibleParty.Address = new WBGLibrary.FedexShip.Address();
            request.RequestedShipment.ShippingChargesPayment.Payor.ResponsibleParty.Address.CountryCode = "US";
        }
        private static void SetLabelDetails(ProcessShipmentRequest request, JobShipment shipment)
        {
            request.RequestedShipment.LabelSpecification = new LabelSpecification();
            string lang = GetPrinterLanguage(shipment.PrinterName);
            //request.RequestedShipment.LabelSpecification.ImageType = ShippingDocumentImageType.PDF; // Dev
            if (lang.ToUpper() == "EPL")
            {
                request.RequestedShipment.LabelSpecification.ImageType = ShippingDocumentImageType.EPL2; // Prod                
            }
            else if (lang.ToUpper() == "ZPL")
            {
                request.RequestedShipment.LabelSpecification.ImageType = ShippingDocumentImageType.ZPLII; // Prod
            }
            request.RequestedShipment.LabelSpecification.ImageTypeSpecified = true;
            request.RequestedShipment.LabelSpecification.LabelFormatType = LabelFormatType.COMMON2D;
            //request.RequestedShipment.LabelSpecification.LabelStockType = LabelStockType.PAPER_LETTER; // Dev
            request.RequestedShipment.LabelSpecification.LabelStockType = LabelStockType.STOCK_4X6; // Prod
            request.RequestedShipment.LabelSpecification.LabelStockTypeSpecified = true;
            request.RequestedShipment.LabelSpecification.LabelPrintingOrientation = LabelPrintingOrientationType.TOP_EDGE_OF_TEXT_FIRST;
            request.RequestedShipment.LabelSpecification.LabelPrintingOrientationSpecified = true;
        }
        private static void SetPackageLineItems(bool isCodShipment, ProcessShipmentRequest request, JobCarton carton, JobShipment shipment)
        {
            request.RequestedShipment.RequestedPackageLineItems = new RequestedPackageLineItem[1];
            request.RequestedShipment.RequestedPackageLineItems[0] = new RequestedPackageLineItem();
            //
            request.RequestedShipment.RequestedPackageLineItems[0].SequenceNumber = carton.SequenceNumber;
            // Package weight information
            request.RequestedShipment.RequestedPackageLineItems[0].Weight = new WBGLibrary.FedexShip.Weight();
            request.RequestedShipment.RequestedPackageLineItems[0].Weight.Value = (decimal)carton.Weight;
            request.RequestedShipment.RequestedPackageLineItems[0].Weight.Units = WBGLibrary.FedexShip.WeightUnits.LB;
            // insured value, this may be optional?
            //request.RequestedShipment.RequestedPackageLineItems[0].InsuredValue = new Money();
            //request.RequestedShipment.RequestedPackageLineItems[0].InsuredValue.Amount = 100;
            //request.RequestedShipment.RequestedPackageLineItems[0].InsuredValue.Currency = "USD";
            if (shipment.ShipRef != null && shipment.ShipRef.Count != 0)
            {
                request.RequestedShipment.RequestedPackageLineItems[0].CustomerReferences = new CustomerReference[shipment.ShipRef.Count];
                for (int i = 0; i < shipment.ShipRef.Count; i++)
                {
                    CustomerReference custRef = new CustomerReference();
                    CustomerReferenceType refType = (CustomerReferenceType)Enum.Parse(typeof(CustomerReferenceType), shipment.ShipRef[i].Name);
                    custRef.CustomerReferenceType = refType;
                    custRef.Value = shipment.ShipRef[i].Value;
                    request.RequestedShipment.RequestedPackageLineItems[0].CustomerReferences[i] = custRef;
                }
            }
            if (shipment.ServiceLevel.Contains("FREIGHT"))
            {
                request.RequestedShipment.RequestedPackageLineItems[0].Dimensions = new WBGLibrary.FedexShip.Dimensions();
                request.RequestedShipment.RequestedPackageLineItems[0].Dimensions.Length = carton.Length;
                request.RequestedShipment.RequestedPackageLineItems[0].Dimensions.Width = carton.Width;
                request.RequestedShipment.RequestedPackageLineItems[0].Dimensions.Height = carton.Height;
                request.RequestedShipment.RequestedPackageLineItems[0].Dimensions.Units = WBGLibrary.FedexShip.LinearUnits.IN;
            }
            if (isCodShipment)
            {
                SetCOD(request);
            }
        }
        private static void SetCustomsClearanceDetails(ProcessShipmentRequest request, JobShipment shipment)
        {
            request.RequestedShipment.CustomsClearanceDetail = new CustomsClearanceDetail();
            request.RequestedShipment.CustomsClearanceDetail.DutiesPayment = new Payment();
            PaymentType paymentType = (PaymentType)Enum.Parse(typeof(PaymentType), shipment.BillShipmentTo);
            request.RequestedShipment.CustomsClearanceDetail.DutiesPayment.PaymentType = paymentType;
            request.RequestedShipment.CustomsClearanceDetail.DutiesPayment.Payor = new Payor();
            request.RequestedShipment.CustomsClearanceDetail.DutiesPayment.Payor.ResponsibleParty = new Party();
            request.RequestedShipment.CustomsClearanceDetail.DutiesPayment.Payor.ResponsibleParty.AccountNumber = "Account Num"; // When we make this live, this will be the third party account number
            request.RequestedShipment.CustomsClearanceDetail.DutiesPayment.Payor.ResponsibleParty.Contact = new WBGLibrary.FedexShip.Contact();
            request.RequestedShipment.CustomsClearanceDetail.DutiesPayment.Payor.ResponsibleParty.Address = new WBGLibrary.FedexShip.Address();
            request.RequestedShipment.CustomsClearanceDetail.DutiesPayment.Payor.ResponsibleParty.Address.CountryCode = shipment.ShipCountry; //This should be the third party's country code, if it is set to third party
            request.RequestedShipment.CustomsClearanceDetail.DocumentContent = InternationalDocumentContentType.NON_DOCUMENTS; //I don't think this will ever need to change.
            //
            request.RequestedShipment.CustomsClearanceDetail.CustomsValue = new WBGLibrary.FedexShip.Money();
            decimal totalValue = 0;
            foreach (JobPackage package in shipment.JobPackages)
            {
                decimal packageValue = Convert.ToDecimal(package.Value) * Convert.ToDecimal(package.Quantity);
                totalValue = totalValue + packageValue;
            }
            request.RequestedShipment.CustomsClearanceDetail.CustomsValue.Amount = totalValue;// This value should be able to be received from EPMS, this should be the value of the commodities put together
            request.RequestedShipment.CustomsClearanceDetail.CustomsValue.Currency = "USD";        
            request.RequestedShipment.CustomsClearanceDetail.CommercialInvoice = new CommercialInvoice();
            request.RequestedShipment.CustomsClearanceDetail.CommercialInvoice.Purpose = PurposeOfShipmentType.SOLD;
            request.RequestedShipment.CustomsClearanceDetail.CommercialInvoice.TermsOfSale = "DDP";
            //
            SetCommodityDetails(request, shipment);
            try
            {
                request.RequestedShipment.ShippingDocumentSpecification = new ShippingDocumentSpecification();
                request.RequestedShipment.ShippingDocumentSpecification.ShippingDocumentTypes = new RequestedShippingDocumentType[1];
                request.RequestedShipment.ShippingDocumentSpecification.ShippingDocumentTypes[0] = RequestedShippingDocumentType.PRO_FORMA_INVOICE;                
                request.RequestedShipment.ShippingDocumentSpecification.CommercialInvoiceDetail = new CommercialInvoiceDetail();
                request.RequestedShipment.ShippingDocumentSpecification.CommercialInvoiceDetail.Format = new ShippingDocumentFormat();
                request.RequestedShipment.ShippingDocumentSpecification.CommercialInvoiceDetail.Format.ImageType = ShippingDocumentImageType.PDF;
                request.RequestedShipment.ShippingDocumentSpecification.CommercialInvoiceDetail.Format.ImageTypeSpecified = true;
                request.RequestedShipment.ShippingDocumentSpecification.CommercialInvoiceDetail.Format.StockType = ShippingDocumentStockType.PAPER_LETTER;
                request.RequestedShipment.ShippingDocumentSpecification.CommercialInvoiceDetail.Format.StockTypeSpecified = true;            
                request.RequestedShipment.SpecialServicesRequested = new ShipmentSpecialServicesRequested();
                request.RequestedShipment.SpecialServicesRequested.EtdDetail = new EtdDetail();
                request.RequestedShipment.SpecialServicesRequested.EtdDetail.RequestedDocumentCopies = new RequestedShippingDocumentType[1];
                request.RequestedShipment.SpecialServicesRequested.EtdDetail.RequestedDocumentCopies[0] = RequestedShippingDocumentType.PRO_FORMA_INVOICE;              
            } catch(Exception ex)
            {
                shipment.errorText.Add(ex.Message);
            }            
        }
        private static void SetCommodityDetails(ProcessShipmentRequest request, JobShipment shipment)
        {
            request.RequestedShipment.CustomsClearanceDetail.Commodities = new WBGLibrary.FedexShip.Commodity[shipment.JobPackages.Count];
            for (int i = 0; i < shipment.JobPackages.Count; i++)
            {
                WBGLibrary.FedexShip.Commodity commodity = new WBGLibrary.FedexShip.Commodity();
                commodity.Name = "Item";
                commodity.NumberOfPieces = shipment.NumberOfCartons.ToString();
                commodity.Description = shipment.JobPackages[i].Description;
                commodity.CountryOfManufacture = "US"; //I am pretty sure this will always be US for now.
                //
                commodity.Weight = new WBGLibrary.FedexShip.Weight();
                decimal weight;
                var result = decimal.TryParse(shipment.JobPackages[i].Weight.ToString(), out weight);
                commodity.Weight.Value = weight;
                commodity.Weight.Units = WBGLibrary.FedexShip.WeightUnits.LB;
                //
                decimal quantity;
                result = decimal.TryParse(shipment.JobPackages[i].Quantity.ToString(), out quantity);
                commodity.Quantity = quantity;
                commodity.QuantitySpecified = true;
                commodity.QuantityUnits = "EA";
                //
                commodity.UnitPrice = new WBGLibrary.FedexShip.Money();
                commodity.UnitPrice.Amount = Convert.ToDecimal(shipment.JobPackages[i].Value);
                commodity.UnitPrice.Currency = "USD";
                try
                {
                    request.RequestedShipment.CustomsClearanceDetail.Commodities[i] = commodity;
                }
                catch (Exception ex)
                {
                    shipment.errorText.Add(ex.Message);
                }

            }
        }
        private static void SetCOD(ProcessShipmentRequest request)
        {
            request.RequestedShipment.SpecialServicesRequested = new ShipmentSpecialServicesRequested();
            request.RequestedShipment.SpecialServicesRequested.SpecialServiceTypes = new ShipmentSpecialServiceType[1];
            request.RequestedShipment.SpecialServicesRequested.SpecialServiceTypes[0] = ShipmentSpecialServiceType.COD;
            //
            request.RequestedShipment.SpecialServicesRequested.CodDetail = new CodDetail();
            request.RequestedShipment.SpecialServicesRequested.CodDetail.CollectionType = CodCollectionType.GUARANTEED_FUNDS;
            request.RequestedShipment.SpecialServicesRequested.CodDetail.CodCollectionAmount = new WBGLibrary.FedexShip.Money();
            request.RequestedShipment.SpecialServicesRequested.CodDetail.CodCollectionAmount.Amount = 250.00M;
            request.RequestedShipment.SpecialServicesRequested.CodDetail.CodCollectionAmount.Currency = "USD";
        }
        private static JobShipment ShowShipmentReply(bool isCodShipment, ProcessShipmentReply reply, JobShipment shipment, JobCarton carton, Guid userId)
        {
            // Details for each package
            foreach (CompletedPackageDetail packageDetail in reply.CompletedShipmentDetail.CompletedPackageDetails)
            {
                shipment = ShowShipmentLabels(isCodShipment, reply.CompletedShipmentDetail, packageDetail, shipment, carton, userId);
            }
            return shipment;
        }
        private static JobShipment ShowShipmentLabels(bool isCodShipment, CompletedShipmentDetail completedShipmentDetail, CompletedPackageDetail packageDetail, JobShipment shipment, JobCarton carton, Guid userId)
        {
            if (null != packageDetail.Label.Parts[0].Image)
            {
                //Save outbound shipping label
                //string LabelPath = @"file Path"; // Dev
                string trackingNumber = packageDetail.TrackingIds[0].TrackingNumber;
                //SaveLabel(LabelPath + trackingNumber + ".pdf", packageDetail.Label.Parts[0].Image); // Dev
                var labelType = packageDetail.Label.ImageType.ToString();
                MemoryStream ms = new MemoryStream(packageDetail.Label.Parts[0].Image);
                string masterTrackingNumber = shipment.MasterTrackingNumber;
                string orderNumber = shipment.JobNumber;
                if(shipment.ShipCountry != "US")
                {
                    FileStream shipDocFile = new FileStream(@"filePath" + packageDetail.TrackingIds[0].TrackingNumber + "_ProFormaInvoice.pdf", FileMode.Create);
                    shipDocFile.Write(completedShipmentDetail.ShipmentDocuments[0].Parts[0].Image, 0, completedShipmentDetail.ShipmentDocuments[0].Parts[0].Image.Length);
                    shipDocFile.Close();
                }
                //Store Label in DB        
                if (labelType == "PDF")
                {
                    string LabelFileName = packageDetail.TrackingIds[0].TrackingNumber + ".pdf";
                    FedexController sc = new FedexController();
                    sc.StoreLabelInDB(ms, LabelFileName, trackingNumber, masterTrackingNumber, orderNumber, completedShipmentDetail, packageDetail, shipment, carton, userId);
                    ShippingLabel label = new ShippingLabel();
                    label.FileBlob = Convert.ToBase64String(packageDetail.Label.Parts[0].Image);
                    label.FileName = LabelFileName;
                    label.TrackingNumber = trackingNumber;
                    shipment.LabelFiles.Add(label);
                }
                else if (labelType == "ZPLII" || labelType == "EPL2")
                {
                    string LabelFileName = packageDetail.TrackingIds[0].TrackingNumber + ".zpl";
                    FedexController sc = new FedexController();
                    sc.StoreLabelInDB(ms, LabelFileName, trackingNumber, masterTrackingNumber, orderNumber, completedShipmentDetail, packageDetail, shipment, carton, userId);
                    ShippingLabel label = new ShippingLabel();
                    label.FileBlob = Convert.ToBase64String(packageDetail.Label.Parts[0].Image);
                    label.FileName = LabelFileName;
                    label.TrackingNumber = trackingNumber;
                    //Print label               
                    try
                    {
                        //Prod
                        ASCIIEncoding enc = new ASCIIEncoding();
                        string converted = enc.GetString(packageDetail.Label.Parts[0].Image);
                        RawPrinterHelper.SendStringToPrinter(shipment.PrinterName, converted);
                    }
                    catch (Exception ex)
                    {
                        shipment.errorText.Add(ex.Message);
                    }
                }
            }
            return shipment;
        }
        private static void SaveLabel(string labelFileName, byte[] labelBuffer)
        {
            // Save label buffer to file
            FileStream LabelFile = new FileStream(labelFileName, FileMode.Create);
            LabelFile.Write(labelBuffer, 0, labelBuffer.Length);
            LabelFile.Close();
            // Display label in Acrobat
            //DisplayLabel(labelFileName);
        }
        private static void DisplayLabel(string labelFileName)
        {
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo(labelFileName);
            info.UseShellExecute = true;
            info.Verb = "open";
            System.Diagnostics.Process.Start(info);
        }
        private JobShipment UpdateJobWithShippingInfo(JobShipment shipment)
        {
            cDataServer server = new cDataServer();
            cJobHeader header = new cJobHeader();
            header.Load("ORDER", shipment.JobNumber);
            header.DeliveryDate = DateTime.Now;
            header.Save();
            cShipment cshipment = new cShipment();
            cshipment.Load(Convert.ToInt32(shipment.ShipmentNumber));
            cshipment.ScheduledShipDate = DateTime.Now;
            cshipment.Shipped = true;
            foreach (cPackage package in cshipment.objPackages)
            {
                package.DeliveryDate = DateTime.Now;
                package.ShipDate = DateTime.Now;
                package.Save(ref server);
                package.Dispose();
            }
            cshipment.Save();
            server.Dispose();
            header.Dispose();
            cshipment.Dispose();
            foreach(JobCarton carton in shipment.Cartons)
            {
                cPackage cpackage = new cPackage();
                cpackage.ComponentNumber = 0;
                cpackage.CreateDatim = DateTime.Now;
                cpackage.DeliveryDate = DateTime.Now;
                cpackage.EntryDate = DateTime.Now;
                cpackage.EntryTime = DateTime.Now;
                cpackage.JobNumber = shipment.JobNumber;
                cpackage.ShipmentNumber = Convert.ToInt32(shipment.ShipmentNumber);
                cpackage.ShipDate = DateTime.Now;
                cpackage.TrackingNumber = carton.TrackingNumber;
                cpackage.Weight = Convert.ToInt16(carton.Weight);
                JobsController.AddPackageToExistingOrder(shipment.JobNumber, Convert.ToInt32(shipment.ShipmentNumber), cpackage);
                cpackage.Dispose();
            }
            return shipment;
        }
        private void StoreLabelInDB(MemoryStream file, string fileName, string trackingNumber, string masterTrackingNumber, string orderNumber, CompletedShipmentDetail completedShipmentDetail, CompletedPackageDetail packageDetail, JobShipment shipment, JobCarton carton, Guid userId)
        {
            try
            {
                using (SqlConnection sqlconn = new SqlConnection(connNameTwo))
                {
                    using (SqlCommand cmd = new SqlCommand("INSERT INTO WI_UTIL.dbo.Fedex_Labels VALUES(@FileName, @FileBlob, @TrackingNumber, @MasterTrackingNumber, @OrderNumber, @AccountNumber, @TrackingIDType, @ShipTime, @Status, @Weight, @UserId)", sqlconn))
                    {
                        sqlconn.Open();
                        cmd.Parameters.Add("@FileName", SqlDbType.VarChar).Value = fileName;
                        cmd.Parameters.Add("@FileBlob", SqlDbType.VarBinary).Value = file;
                        cmd.Parameters.Add("@TrackingNumber", SqlDbType.VarChar).Value = trackingNumber;
                        cmd.Parameters.Add("@MasterTrackingNumber", SqlDbType.VarChar).Value = masterTrackingNumber;
                        cmd.Parameters.Add("@OrderNumber", SqlDbType.VarChar).Value = orderNumber;
                        cmd.Parameters.Add("@AccountNumber", SqlDbType.VarChar).Value = shipment.ThirdPartyBilling;
                        cmd.Parameters.Add("@TrackingIDType", SqlDbType.VarChar).Value = packageDetail.TrackingIds[0].TrackingIdType;
                        cmd.Parameters.Add("@ShipTime", SqlDbType.DateTime).Value = DateTime.Now;
                        cmd.Parameters.Add("@Status", SqlDbType.VarChar).Value = "Current";
                        cmd.Parameters.Add("@Weight", SqlDbType.Float).Value = carton.Weight;
                        cmd.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;           
                        cmd.ExecuteNonQuery();                    
                    }
                }
            }
            catch (Exception ex)
            {
                //Send email
            }
        }
        private static JobShipment AlaskaServiceTypes(JobShipment shipment)
        {
            switch (shipment.ServiceLevel)
            {
                case "2101":
                    shipment.ServiceLevel = "FEDEX_GROUND";
                    break;
                case "2005":
                    shipment.ServiceLevel = "FEDEX_2_DAY";
                    break;
                case "2004":
                    shipment.ServiceLevel = "STANDARD_OVERNIGHT";
                    break;
                case "2003":
                    shipment.ServiceLevel = "PRIORITY_OVERNIGHT";
                    break;
                case "2301":
                    shipment.ServiceLevel = "INTERNATIONAL_PRIORITY";
                    break;
                case "2008":
                    shipment.ServiceLevel = "FEDEX_2_DAY_FREIGHT";
                    break;
                case "GRND":
                    shipment.ServiceLevel = "FEDEX_GROUND";
                    break;
                case "2DAY":
                    shipment.ServiceLevel = "FEDEX_2_DAY";
                    break;
                case "SOVR":
                    shipment.ServiceLevel = "STANDARD_OVERNIGHT";
                    break;
                case "POVR":
                    shipment.ServiceLevel = "PRIORITY_OVERNIGHT";
                    break;
                case "IPR":
                    shipment.ServiceLevel = "INTERNATIONAL_PRIORITY";
                    break;
                case "2DF":
                    shipment.ServiceLevel = "FEDEX_2_DAY_FREIGHT";
                    break;
                default:
                    shipment.ServiceLevel = shipment.ServiceLevel;
                    break;
            }
            return shipment;
        }
        private static JobShipment TSGServiceTypes(JobShipment shipment)
        {
            switch (shipment.ServiceLevel)
            {
                case "GRND":
                    shipment.ServiceLevel = "FEDEX_GROUND";
                    break;
                case "2DAY":
                    shipment.ServiceLevel = "FEDEX_2_DAY";
                    break;
                case "SOVR":
                    shipment.ServiceLevel = "STANDARD_OVERNIGHT";
                    break;
                case "POVR":
                    shipment.ServiceLevel = "PRIORITY_OVERNIGHT";
                    break;
                case "IPR":
                    shipment.ServiceLevel = "INTERNATIONAL_PRIORITY";
                    break;
                case "2DF":
                    shipment.ServiceLevel = "FEDEX_2_DAY_FREIGHT";
                    break;
                default:
                    shipment.ServiceLevel = shipment.ServiceLevel;
                    break;
            }
            return shipment;
        }
        //-------------------------------------Delete Fedex Shipment Methods-----------------------------------------------------
        private string DeleteShipment(string trackingNumber, string trackingIdType)
        {
            DeleteShipmentRequest request = new DeleteShipmentRequest();
            request.WebAuthenticationDetail = SetWebAuthenticationDetail();
            request.WebAuthenticationDetail.UserCredential = new WBGLibrary.FedexShip.WebAuthenticationCredential();
            request.WebAuthenticationDetail.UserCredential.Key = "API Key";
            request.WebAuthenticationDetail.UserCredential.Password = "API Password";                                                                                           //
            request.ClientDetail = new WBGLibrary.FedexShip.ClientDetail();
            request.ClientDetail.AccountNumber = "API Account Num"; 
            request.ClientDetail.MeterNumber = "API Meter Num";                                                         //
            request.Version = new WBGLibrary.FedexShip.VersionId();
            //
            request.TrackingId = new TrackingId();
            WBGLibrary.FedexShip.TrackingIdType type = (WBGLibrary.FedexShip.TrackingIdType)Enum.Parse(typeof(WBGLibrary.FedexShip.TrackingIdType), trackingIdType);
            request.TrackingId.TrackingIdType = type; // Replace with the tracking id type from a ProcessShipment Reply
            request.TrackingId.TrackingIdTypeSpecified = true;
            request.TrackingId.TrackingNumber = trackingNumber;                                                    //
            request.DeletionControl = DeletionControlType.DELETE_ONE_PACKAGE;
            ShipService service = new ShipService();
            ShipmentReply reply = service.deleteShipment(request);
            if(reply.HighestSeverity == WBGLibrary.FedexShip.NotificationSeverityType.SUCCESS)
            {
                using (SqlConnection sqlconn = new SqlConnection(connNameTwo))
                {
                    using (SqlCommand cmd = new SqlCommand("UPDATE WI_UTIL.dbo.Fedex_Labels SET Status = 'Cancelled' WHERE TrackingNumber = @TrackingNumber", sqlconn))
                    {
                        sqlconn.Open();
                        cmd.Parameters.Add("@TrackingNumber", SqlDbType.VarChar).Value = trackingNumber;
                        try
                        {
                            cmd.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
                return "success";
            } else
            {
                return reply.Notifications[0].Message;
            }                        
        }
        private FedexPackage GetPackagesById(int Id)
        {
            FedexPackage package = new FedexPackage();
            using (SqlConnection sqlconn = new SqlConnection(connNameTwo))
            {
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM WI_Util.dbo.Fedex_Labels WHERE Id = @Id", sqlconn))
                {
                    sqlconn.Open();
                    cmd.Parameters.Add("@Id", SqlDbType.VarChar).Value = Id.ToString();
                    try
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                package.Id = Convert.ToInt32(rdr["Id"]);
                                package.FileName = rdr["FileName"].ToString();
                                package.FileBlob = rdr["FileBlob"].ToString();
                                package.TrackingNumber = rdr["TrackingNumber"].ToString();
                                package.MasterTrackingNumber = rdr["MasterTrackingNumber"].ToString();
                                package.OrderNumber = rdr["OrderNumber"].ToString();
                                package.AccountNumber = rdr["AccountNumber"].ToString();
                                package.TrackingIDType = rdr["TrackingIDType"].ToString();
                                package.ShipTime = Convert.ToDateTime(rdr["ShipTime"]);
                                package.Status = rdr["Status"].ToString();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
            return package;
        }
        private static string GetPrinterLanguage(string printerName)
        {
            string connName = @"Connection String";
            string lang = "";
            using (SqlConnection sqlconn = new SqlConnection(connName))
            {
                using (SqlCommand cmd = new SqlCommand("SELECT * FROM ZebraPrinters WHERE PrinterName = @PrinterName", sqlconn))
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