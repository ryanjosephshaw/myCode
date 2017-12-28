using Kendo.Mvc.Extensions;
using Kendo.Mvc.UI;
using System;
using System.Collections.Generic;
using System.Web.Mvc;
using WI20Library.APIConnector;
using WBGLibrary;
using WI20Library;
using Microsoft.AspNet.Identity;
using System.Linq;

namespace WI20LTE.Controllers
{
    public class EPMSDashboardController : Controller
    {
        private string Domain = WI20Library.AppSettings.GetAppSetting("WBGConnect").ToString();
        private const string APIRoot = "/api/Dashboard/";
        PopulateModel PM = new PopulateModel();
        public List<EPMSPress> GetPresses()
        {
            List<EPMSPress> presses = new List<EPMSPress>();
            WI20Library.Login Login = new WI20Library.Login();
            SetLogin.Set(ref Login, this, WBGLibrary.LogTrace.GetCurrentMethod(), User.Identity.GetUserId());
            string sURL = Domain + APIRoot + "GetPresses";
            presses = PM.GetModelTList<EPMSPress>(sURL, Login, GetUserPlantId());
            return presses;
        }
        public ActionResult Index()
        {
            return View();
        }
        [ValidateInput(false)]
        public ActionResult GetJobs([DataSourceRequest]DataSourceRequest request, string options, string searchbox)
        {
            List<EPMSDashboard> Model = new List<EPMSDashboard>();
            WI20Library.Login Login = new WI20Library.Login();
            SetLogin.Set(ref Login, this, WBGLibrary.LogTrace.GetCurrentMethod(), User.Identity.GetUserId());
            EPMSDashOptions dashOptions = new EPMSDashOptions();
            dashOptions.GridOptions = options;
            dashOptions.UserId = new Guid(User.Identity.Name);
            dashOptions.PlantId = GetUserPlantId();
            string sURL = Domain + APIRoot + "GetJobs";
            Model = PM.GetModelTList<EPMSDashboard, EPMSDashOptions>(sURL, dashOptions, Login);
            List<EPMSDashboard> filtered = new List<EPMSDashboard>();
            if (searchbox != "" && searchbox != null)
            {
                filtered = Model.Where(x => x.RUSH.ToUpper().Contains(searchbox.ToUpper()) || x.PlantID.ToUpper().Contains(searchbox.ToUpper()) || x.JobNumber.ToUpper().Contains(searchbox.ToUpper()) || x.DueDate.ToShortDateString().Contains(searchbox.ToUpper()) || x.CNum.ToUpper().Contains(searchbox.ToUpper()) || x.PressCC.ToUpper().Contains(searchbox.ToUpper()) || x.Customer.ToUpper().Contains(searchbox.ToUpper()) || x.JobDescription.ToUpper().Contains(searchbox.ToUpper()) || x.LastCC.ToUpper().Contains(searchbox.ToUpper()) || x.NextCC.ToUpper().Contains(searchbox.ToUpper()) || x.OS.ToUpper().Contains(searchbox.ToUpper()) || x.ShipVia.ToUpper().Contains(searchbox.ToUpper()) || x.DeliveryDate.ToShortDateString().Contains(searchbox.ToUpper()) || x.ShipInstructions.ToUpper().Contains(searchbox.ToUpper()) || x.ProdType.Equals(searchbox)).ToList();
            }
            else
            {
                filtered = Model;
            }
            return Json(filtered.ToDataSourceResult(request), JsonRequestBehavior.AllowGet);
        }
        public ActionResult StoreOptions(string gridViewName, string gridName, string gridOptions)
        {
            WI20Library.Login Login = new WI20Library.Login();
            SetLogin.Set(ref Login, this, WI20Library.LogTrace.GetCurrentMethod(), User.Identity.GetUserId());
            string surl = WI20Library.AppSettings.GetAppSetting("WI20API").ToString() + "/api/GridOptions/SaveGridOptions";
            var options = new GridOptions()
            {
                gridName = gridName,
                gridOptions = gridOptions,
                gridViewName = gridViewName
            };
            var response = PM.GetModelT<GridOptions, GridOptions>(surl, options, Login);
            return Content("");
        }
        public ActionResult GetOptions(string gridName, string gridViewName)
        {
            WI20Library.Login Login = new WI20Library.Login();
            SetLogin.Set(ref Login, this, WI20Library.LogTrace.GetCurrentMethod(), User.Identity.GetUserId());
            string surl = WI20Library.AppSettings.GetAppSetting("WI20API").ToString() + "/api/GridOptions/GetGridOptions";
            GridOptions gridOptions = new GridOptions()
            {
                gridName = gridName,
                gridOptions = gridViewName
            };
            var response = PM.GetModelT<GridOptions, GridOptions>(surl, gridOptions, Login);
            return Content(response.gridOptions);
        }
        public ActionResult GetViews(string gridName)
        {
            WI20Library.Login Login = new WI20Library.Login();
            SetLogin.Set(ref Login, this, WI20Library.LogTrace.GetCurrentMethod(), User.Identity.GetUserId());
            string surl = WI20Library.AppSettings.GetAppSetting("WI20API").ToString() + "/api/GridOptions/GetViews";
            var response = PM.GetModelTList<GridOptions>(surl, Login, gridName);
            return Json(response, JsonRequestBehavior.AllowGet);
        }
        public ActionResult Search([DataSourceRequest]DataSourceRequest request, string searchbox)
        {
            List<EPMSDashboard> Model = new List<EPMSDashboard>();
            WI20Library.Login Login = new WI20Library.Login();
            string sURL = Domain + APIRoot + "GetJobs";
            Model = PM.GetModelTList<EPMSDashboard>(sURL, Login);
            List<EPMSDashboard> filtered = new List<EPMSDashboard>();
            if (searchbox != "")
            {
                filtered = Model.Where(x => x.RUSH.ToUpper().Contains(searchbox.ToUpper()) || x.PlantID.ToUpper().Contains(searchbox.ToUpper()) || x.JobNumber.ToUpper().Contains(searchbox.ToUpper()) || x.DueDate.ToShortDateString().Contains(searchbox.ToUpper()) || x.CNum.ToUpper().Contains(searchbox.ToUpper()) || x.PressCC.ToUpper().Contains(searchbox.ToUpper()) || x.Customer.ToUpper().Contains(searchbox.ToUpper()) || x.JobDescription.ToUpper().Contains(searchbox.ToUpper()) || x.LastCC.ToUpper().Contains(searchbox.ToUpper()) || x.NextCC.ToUpper().Contains(searchbox.ToUpper()) || x.OS.ToUpper().Contains(searchbox.ToUpper()) || x.ShipVia.ToUpper().Contains(searchbox.ToUpper()) || x.DeliveryDate.ToShortDateString().Contains(searchbox.ToUpper()) || x.ShipInstructions.ToUpper().Contains(searchbox.ToUpper()) || x.ProdType.Equals(searchbox)).ToList();
            }
            else
            {
                filtered = Model;
            }
            List<EPMSDashboard> ordered = filtered.OrderBy(x => x.DueDate).ToList();
            return Json(ordered.ToDataSourceResult(request), JsonRequestBehavior.AllowGet);
        }
        public string GetUserPlantId()
        {
            string plantId = "";
            if (User.IsInAnyRole("Burnside"))
                plantId = "03";
            if (User.IsInAnyRole("Kent"))
                plantId = "02";
            if (User.IsInAnyRole("Chino"))
                plantId = "07";
            if (User.IsInAnyRole("Corporate", "Portland Warehouse"))
                plantId = "01";
            return plantId;
        }
        public ActionResult Configuration()
        {
            List<EPMSPress> presses = GetPresses();
            return PartialView(presses);
        }
        public ActionResult ClearSettings(string gridViewName)
        {
            WI20Library.Login Login = new WI20Library.Login();
            SetLogin.Set(ref Login, this, WBGLibrary.LogTrace.GetCurrentMethod(), User.Identity.GetUserId());
            string sURL = WI20Library.AppSettings.GetAppSetting("WI20API").ToString() + "/api/GridOptions/ClearSettings";
            APICalls.GetFromAPI(sURL, gridViewName, Login, new Guid(), "");
            return Content("");
        }
        public ActionResult Customers()
        {
            return View();
        }
        public ActionResult CustomerDetail(DateTime startDate, DateTime endDate, string plant, string cust, string type)
        {
            WI20Library.Login login = new WI20Library.Login();
            EPMSSearchCrit epmssc = new EPMSSearchCrit();
            epmssc.SearchCrit = new SearchCriteria();
            epmssc.SearchCrit.StartDate = startDate;
            epmssc.SearchCrit.EndDate = endDate;
            if (!User.IsInAnyRole("All Locations") && (plant == "" || plant == null))
            {
                if (User.IsInAnyRole("Burnside"))
                    plant = "03";
                if (User.IsInAnyRole("Portland Warehouse"))
                    plant = "01";
                if (User.IsInAnyRole("Kent"))
                    plant = "02";
                if (User.IsInAnyRole("Chino"))
                    plant = "07";
            }
            if (cust == "196362")
            {
                epmssc.ItemType = type;
            }
            epmssc.Plant = plant;
            epmssc.CustAccount = cust;
            OrderSummary orders = new OrderSummary();
            string sURL = Domain + APIRoot + "GetSummaryInfo";
            orders = PM.GetModelT<OrderSummary, EPMSSearchCrit>(sURL, epmssc, login, login.ObjID, plant);
            return PartialView(orders);
        }
        public ActionResult GetEPMSCustomers()
        {
            WI20Library.Login Login = new WI20Library.Login();
            SetLogin.Set(ref Login, this, WBGLibrary.LogTrace.GetCurrentMethod(), User.Identity.GetUserId());
            List<EPMSCustomer> epmsCustomers = new List<EPMSCustomer>();
            string sURL = Domain + APIRoot + "GetEPMSCustomers";
            epmsCustomers = PM.GetModelTList<EPMSCustomer>(sURL, Login);
            epmsCustomers = epmsCustomers.OrderBy(x => x.Name).ToList();
            EPMSCustomer cust = new EPMSCustomer();
            cust.Account = "";
            cust.Name = "All Accounts";
            epmsCustomers.Insert(0, cust);
            return Json(epmsCustomers, JsonRequestBehavior.AllowGet);
        }
        public ActionResult TSGDetail(DateTime startDate, DateTime endDate, string plant, string cust, string type)
        {
            WI20Library.Login login = new WI20Library.Login();
            EPMSSearchCrit epmssc = new EPMSSearchCrit();
            epmssc.SearchCrit = new SearchCriteria();
            epmssc.SearchCrit.StartDate = startDate;
            epmssc.SearchCrit.EndDate = endDate;
            if (!User.IsInAnyRole("All Locations") && (plant == "" || plant == null))
            {
                if (User.IsInAnyRole("Burnside"))
                    plant = "03";
                if (User.IsInAnyRole("Portland Warehouse"))
                    plant = "01w";
                if (User.IsInAnyRole("Kent"))
                    plant = "02";
                if (User.IsInAnyRole("Chino"))
                    plant = "07";
                if (User.IsInAnyRole("Corporate"))
                    plant = "01";
            }
            epmssc.Plant = plant;
            epmssc.CustAccount = cust;
            epmssc.ItemType = type;
            OrderSummary orders = new OrderSummary();
            string sURL = Domain + APIRoot + "GetCustomInfo";
            orders = PM.GetModelT<OrderSummary, EPMSSearchCrit>(sURL, epmssc, login, login.ObjID, plant);
            return PartialView(orders);
        }
        public ActionResult PlantComparison(DateTime startDate, DateTime endDate, string cust, string type)
        {
            WI20Library.Login login = new WI20Library.Login();
            EPMSSearchCrit epmssc = new EPMSSearchCrit();
            epmssc.SearchCrit = new SearchCriteria();
            epmssc.SearchCrit.StartDate = startDate;
            epmssc.SearchCrit.EndDate = endDate;
            epmssc.CustAccount = cust;
            epmssc.ItemType = type;
            OrderSummary orders = new OrderSummary();
            string sURL = Domain + APIRoot + "GetPlantComparison";
            orders = PM.GetModelT<OrderSummary, EPMSSearchCrit>(sURL, epmssc, login, login.ObjID, "");
            return PartialView(orders);
        }
        public ActionResult TSGBCDetail(DateTime startDate, DateTime endDate, string plant, string cust, string type)
        {
            WI20Library.Login login = new WI20Library.Login();
            EPMSSearchCrit epmssc = new EPMSSearchCrit();
            epmssc.SearchCrit = new SearchCriteria();
            epmssc.SearchCrit.StartDate = startDate;
            epmssc.SearchCrit.EndDate = endDate;
            if (!User.IsInAnyRole("All Locations") && (plant == "" || plant == null))
            {
                if (User.IsInAnyRole("Burnside"))
                    plant = "03";
                if (User.IsInAnyRole("Portland Warehouse"))
                    plant = "01w";
                if (User.IsInAnyRole("Kent"))
                    plant = "02";
                if (User.IsInAnyRole("Chino"))
                    plant = "07";
                if (User.IsInAnyRole("Corporate"))
                    plant = "01";
            }
            epmssc.Plant = plant;
            epmssc.CustAccount = cust;
            epmssc.ItemType = type;
            OrderSummary orders = new OrderSummary();
            string sURL = Domain + APIRoot + "GetTSGBCInfo";
            orders = PM.GetModelT<OrderSummary, EPMSSearchCrit>(sURL, epmssc, login, login.ObjID, plant);
            return PartialView(orders);
        }
        public ActionResult TSGEnvDetail(DateTime startDate, DateTime endDate, string plant, string cust, string type)
        {
            WI20Library.Login login = new WI20Library.Login();
            EPMSSearchCrit epmssc = new EPMSSearchCrit();
            epmssc.SearchCrit = new SearchCriteria();
            epmssc.SearchCrit.StartDate = startDate;
            epmssc.SearchCrit.EndDate = endDate;
            if (!User.IsInAnyRole("All Locations") && (plant == "" || plant == null))
            {
                if (User.IsInAnyRole("Burnside"))
                    plant = "03";
                if (User.IsInAnyRole("Portland Warehouse"))
                    plant = "01w";
                if (User.IsInAnyRole("Kent"))
                    plant = "02";
                if (User.IsInAnyRole("Chino"))
                    plant = "07";
                if (User.IsInAnyRole("Corporate"))
                    plant = "01";
            }
            epmssc.Plant = plant;
            epmssc.CustAccount = cust;
            epmssc.ItemType = type;
            OrderSummary orders = new OrderSummary();
            string sURL = Domain + APIRoot + "GetTSGEnvInfo";
            orders = PM.GetModelT<OrderSummary, EPMSSearchCrit>(sURL, epmssc, login, login.ObjID, plant);
            return PartialView(orders);
        }
    }
}