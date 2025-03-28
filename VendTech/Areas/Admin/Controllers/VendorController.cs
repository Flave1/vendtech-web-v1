﻿using System;
using System.Collections.Generic;
using System.Web.Configuration;
using System.Web.Mvc;
using VendTech.Attributes;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;

namespace VendTech.Areas.Admin.Controllers
{
    public class VendorController : AdminBaseV2Controller
    {
        #region Variable Declaration
        private new readonly IUserManager _userManager;
        private readonly IVendorManager _vendorManager;
        private readonly IAgencyManager _agentManager;
        private readonly ICommissionManager _commissionManager;
        private readonly IEmailTemplateManager _emailTemplateManager;
        private readonly IPOSManager _posManager;
        private readonly IAuthenticateManager _authenticateManager;
        #endregion

        public VendorController(
            IUserManager userManager,
            IErrorLogManager errorLogManager,
            IVendorManager vendorManager,
            IAgencyManager agentManager,
            ICommissionManager commissionManager,
            IEmailTemplateManager emailTemplateManager,
            IPOSManager posManager,
            IAuthenticateManager authenticateManager)
            : base(errorLogManager)
        {
            _userManager = userManager;
            _vendorManager = vendorManager;
            _agentManager = agentManager;
            _commissionManager = commissionManager;
            _emailTemplateManager = emailTemplateManager;
            _posManager = posManager;
            _authenticateManager = authenticateManager;

        }

        #region User Management

        [HttpGet]
        public ActionResult ManageVendors(string vendor = "")
        {
            ViewBag.SelectedTab = SelectedAdminTab.Vendors;
            if (!string.IsNullOrEmpty(vendor))
            {
                var model = PagingModel.DefaultModel("CreatedAt", "Desc");
                model.SearchField = "agency";
                model.Search = vendor;
                var list = _vendorManager.GetVendorsPagedList(model);
                return View("ManageVendorsV2", list);
            }
            var list1 = _vendorManager.GetVendorsPagedList(PagingModel.DefaultModel("CreatedAt", "Desc"));
            return View("ManageVendorsV2", list1);
        }

        [AjaxOnly, HttpPost]
        public JsonResult GetVendorsPagingList(PagingModel model)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Vendors;
            var modal = _vendorManager.GetVendorsPagedList(model);
            List<string> resultString = new List<string>();
            resultString.Add(RenderRazorViewToString("Partials/_vendorListing", modal));
            resultString.Add(modal.TotalCount.ToString());
            return JsonResult(resultString);
        }

        [HttpPost]
        public ActionResult AddEditVendor(SaveVendorModel model)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Vendors;
            bool isAddCase = model.VendorId == 0;
            var result = _vendorManager.SaveVendor(model);
            if (result.Status == ActionStatus.Successfull && isAddCase)
            {
                //var emailTemplate = _emailTemplateManager.GetEmailTemplateByTemplateType(TemplateTypes.NewAppUserRegistration);
                //string body = emailTemplate.TemplateContent;
                //body = body.Replace("%UserName%", model.Email);
                //body = body.Replace("%Password%", model.Password);
                //body = body.Replace("%AppLink%", WebConfigurationManager.AppSettings["AppLink"].ToString());
                //body = body.Replace("%WebLink%", WebConfigurationManager.AppSettings["BaseUrl"].ToString());
                //Utilities.SendEmail(model.Email, emailTemplate.EmailSubject, body);
            }
            return JsonResult(result);
        }
        [HttpGet]
        public ActionResult AddEditVendor(string id = null)
        {

            var countries = _authenticateManager.GetCountries();
            var countryDrpData = new List<SelectListItem>();

            foreach (var item in countries)
            {
                countryDrpData.Add(new SelectListItem { Text = item.Name, Value = item.CountryId.ToString() });
            }
            ViewBag.countries = countryDrpData;
            ViewBag.Cities = _authenticateManager.GetCities();
            ViewBag.Users = _userManager.GetAppUsersSelectList();
            ViewBag.SelectedTab = SelectedAdminTab.Vendors;
            var model = new SaveVendorModel();
            ViewBag.Agents = _agentManager.GetAgentsSelectList();
            ViewBag.Pos = _posManager.GetPOSSelectList();
            var commissions = _commissionManager.GetCommissions();
            var drpCommissions = new List<SelectListItem>();
            foreach (var item in commissions)
            {
                drpCommissions.Add(new SelectListItem { Text = item.Value.ToString(), Value = item.CommissionId.ToString() });
            }
            ViewBag.AgentTypes = Utilities.EnumToList(typeof(AgentTypeEnum));
            ViewBag.commissions = drpCommissions;
            if (!string.IsNullOrEmpty(id))
            {
                var val = Convert.ToInt64(Utilities.Base64Decode(id));
                model = _vendorManager.GetVendorDetail(val);
            }
            return View("AddEditVendorV2", model);
        }
        [AjaxOnly, HttpPost]
        public JsonResult DeleteVendor(long vendorId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Users;
            return JsonResult(_vendorManager.DeleteVendor(vendorId));
        }
        [HttpGet]
        public ActionResult Detail(string id)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Vendors;
            ViewBag.VendorId = id;
            var model = new SaveVendorModel();
            ViewBag.Agents = _agentManager.GetAgentsSelectList();
            ViewBag.Pos = _posManager.GetPOSSelectList();
            var commissions = _commissionManager.GetCommissions();
            var drpCommissions = new List<SelectListItem>();
            foreach (var item in commissions)
            {
                drpCommissions.Add(new SelectListItem { Text = item.Value.ToString(), Value = item.CommissionId.ToString() });
            }
            ViewBag.AgentTypes = Utilities.EnumToList(typeof(AgentTypeEnum));
            ViewBag.commissions = drpCommissions;

            var val = Convert.ToInt64(Utilities.Base64Decode(id));
            model = _vendorManager.GetVendorDetail(val);
            return View(model);
        }
        [HttpGet]
        public ActionResult Pos(string id)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Vendors;
            ViewBag.VendorId = id;
            var vendor = _vendorManager.GetVendorDetail(Convert.ToInt64(Utilities.Base64Decode(id)));
            ViewBag.VendorName = vendor.Vendor;
            var users = _posManager.GetPOSPagedList(PagingModel.DefaultModel("CreatedAt", "Desc"), 0, Convert.ToInt64(Utilities.Base64Decode(id)), true);
            return View("PosV2", users);
        }

        [AjaxOnly, HttpPost]
        public JsonResult GetPosPagingList(PagingModel model)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Vendors;
            var modal = _posManager.GetPOSPagedList(model, 0, Convert.ToInt64(Utilities.Base64Decode(model.VendorId)), true);
            List<string> resultString = new List<string>();
            resultString.Add(RenderRazorViewToString("Partials/_posListing", modal));
            resultString.Add(modal.TotalCount.ToString());
            return JsonResult(resultString);
        }
        #endregion
    }
}