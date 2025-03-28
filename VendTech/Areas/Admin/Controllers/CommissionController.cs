﻿using System.Web.Mvc;
using VendTech.Attributes;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;

namespace VendTech.Areas.Admin.Controllers
{
    public class CommissionController : AdminBaseV2Controller
    {
        #region Variable Declaration
        private readonly ICommissionManager _commissionManager;
        private readonly IEmailTemplateManager _templateManager;
        #endregion

        public CommissionController(ICommissionManager commissionManager, IErrorLogManager errorLogManager, IEmailTemplateManager templateManager)
            : base(errorLogManager)
        {
            _commissionManager = commissionManager;
            _templateManager = templateManager;
        }

        public ActionResult ManageCommissions()
        {
            ViewBag.SelectedTab = SelectedAdminTab.Platforms;
            return View(_commissionManager.GetCommissions());
        }
        [AjaxOnly, HttpPost]
        public JsonResult DeleteCommission(int id)
        {
            return JsonResult(_commissionManager.DeleteCommission(id));
        }
        [AjaxOnly, HttpPost]
        public JsonResult SaveCommission(SaveCommissionModel model)
        {
            return JsonResult(_commissionManager.SaveCommission(model));
        }
    }
}