﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using VendTech.Attributes;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;

namespace VendTech.Areas.Admin.Controllers
{
    public class DepositController : AdminBaseV2Controller
    {
        #region Variable Declaration
        private new readonly IUserManager _userManager;
        private readonly IEmailTemplateManager _templateManager;
        private readonly IDepositManager _depositManager;
        private readonly ICommissionManager _commissionManager;
        private readonly IVendorManager _vendorManager;
        private readonly IPOSManager _posManager;
        private readonly IBankAccountManager _bankAccountManager;
        private readonly IPaymentTypeManager _paymentTypeManager;
        #endregion

        public DepositController(IUserManager userManager, ICommissionManager commissionManager, IErrorLogManager errorLogManager, IEmailTemplateManager templateManager, IDepositManager depositManager, IVendorManager vendor, IBankAccountManager bankAccountManager, IPOSManager posManager, IPaymentTypeManager paymentTypeManager)
            : base(errorLogManager)
        {
            _userManager = userManager;
            _templateManager = templateManager;
            _depositManager = depositManager;
            _commissionManager = commissionManager;
            _vendorManager = vendor;
            _bankAccountManager = bankAccountManager;
            _posManager = posManager;
            _paymentTypeManager = paymentTypeManager;
        }

        #region User Management

        [HttpGet]
        public ActionResult ManageDeposits(long? posId = null, long? vendorId = null)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            ViewBag.DepositTypes = _paymentTypeManager.GetPaymentTypeSelectList();
            var vendors = _vendorManager.GetVendorsSelectList();
            var user = new SaveVendorModel();
            if (posId.HasValue)
            {
                user = _vendorManager.GetVendorDetailByPosId(posId.Value);
                ViewBag.vendorId = user.VendorId;
                ViewBag.posId_ = posId;
                ViewBag.Balance = _vendorManager.GetVendorPendingDepositTotal(posId.Value);
                ViewBag.percentage = _posManager.GetPosCommissionPercentage(posId.Value); 
            }
            else
            {
                ViewBag.vendorId = 0;
                ViewBag.Balance = _vendorManager.GetVendorPendingDepositTotal(0);
            }
            //else
            //{
            //    ViewBag.PosId = _vendorManager.GetPosSelectList().Select(p=>new {p.Text,p.Value,p.Selected }).ToList();
            //}

            ViewBag.PosId = new SelectList(_vendorManager.GetPosSelectList(), "Value", "Text", posId);


            ViewBag.ChkBankName = new SelectList(_bankAccountManager.GetBankNames_API().ToList(), "BankName", "BankName");

            var bankAccounts = _bankAccountManager.GetBankAccounts();
            ViewBag.bankAccounts = bankAccounts.ToList().Select(p => new SelectListItem { Text = p.BankName.ToUpper(), Value = p.BankAccountId.ToString() }).ToList();
            ViewBag.vendors = vendors;
            var deposits = new PagingResult<DepositListingModel>();

            // This is commented because client want all  POS on page load;
            //if (posId.HasValue && posId.Value > 0)
            //{
            deposits = _depositManager.GetAllPendingDepositPagedList(PagingModel.DefaultModel("CreatedAt", "Desc"), vendorId: posId.HasValue ? posId.Value : 0);
            //}
            return View("ManageDepositsV2", deposits);
        }

        [AjaxOnly, HttpPost]
        public JsonResult GetDepositsPagingList(PagingModel model)
        {
            model.SortOrder = "createdat";
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            long vendorid = 0;
            if (!string.IsNullOrEmpty(model.VendorId))
            {
                vendorid = Convert.ToInt64(model.VendorId);
            }
            var modal = _depositManager.GetDepositPagedList(model, false, vendorid);
            List<string> resultString = new List<string>();
            resultString.Add(RenderRazorViewToString("Partials/_depositListing", modal));
            resultString.Add(modal.TotalCount.ToString());
            return JsonResult(resultString);
        }

        [HttpGet]
        public ActionResult DepositLogs()
        {
            //ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var deposits = _depositManager.GetDepositLogsPagedList(PagingModel.DefaultModel("CreatedAt", "Desc"));
            return View(deposits);
        }

        [AjaxOnly, HttpPost]
        public JsonResult GetDepositLogsPagingList(PagingModel model)
        {
            //ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var modal = _depositManager.GetDepositLogsPagedList(model);
            List<string> resultString = new List<string>();
            resultString.Add(RenderRazorViewToString("Partials/_depositLogListing", modal));
            resultString.Add(modal.TotalCount.ToString());
            return JsonResult(resultString);
        }
        [AjaxOnly, HttpPost]
        public async Task<JsonResult> ApproveDeposit(long depositId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var result = await _depositManager.ChangeDepositStatus(depositId, DepositPaymentStatusEnum.ApprovedByAccountant);
            return JsonResult(result.Results);
        }
        [AjaxOnly, HttpPost]
        public async Task<JsonResult> RejectDeposit(long depositId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            return JsonResult(await _depositManager.ChangeDepositStatus(depositId, DepositPaymentStatusEnum.RejectedByAccountant));
        }

        [AjaxOnly, HttpPost]
        public async Task<JsonResult> ApproveReleaseDeposit(long depositId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits; 
            return JsonResult(await _depositManager.ChangeDepositStatus(depositId, DepositPaymentStatusEnum.Released));
        }
        [AjaxOnly, HttpPost]
        public async Task<JsonResult> RejectReleaseDeposit(long depositId)
        {
            ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            return JsonResult(await _depositManager.ChangeDepositStatus(depositId, DepositPaymentStatusEnum.Rejected));
        }

        public ActionResult AddDeposit()
        {
            var model = new DepositModel();
            var commissions = _commissionManager.GetCommissions();
            var drpCommissions = new List<SelectListItem>();
            if (commissions.Any())
                foreach (var item in commissions)
                {
                    drpCommissions.Add(new SelectListItem { Text = item.Value.ToString(), Value = item.CommissionId.ToString() });
                }
            ViewBag.commissions = drpCommissions;

            ViewBag.AppUsers = _userManager.GetAppUsersSelectList();
            ViewBag.DepositTypes = _paymentTypeManager.GetPaymentTypeSelectList();
            return View(model);
        }
        [AjaxOnly, HttpPost]
        public JsonResult AddDeposit(DepositModel model)
        {
            model.UserId = model.VendorId;
            var result = _depositManager.SaveDepositRequest(model);


            var pos = _posManager.GetSinglePos(result.Object.POSId);
            if(pos != null)
            {
                var emailTemplate = _templateManager.GetEmailTemplateByTemplateType(TemplateTypes.DepositRequestNotification);
                if(emailTemplate.Receivers.Count > 0)
                    foreach (var email in emailTemplate.Receivers)
                    {
                        var userAccount = _userManager.GetUserDetailByEmail(email);
                        if (emailTemplate != null)
                        {
                            string body = emailTemplate.TemplateContent;
                            body = body.Replace("%AdminUserName%", userAccount?.Name +" "+ userAccount?.SurName);
                            body = body.Replace("%VendorName%", pos.User.Vendor);
                            body = body.Replace("%POSID%", pos.SerialNumber);
                            body = body.Replace("%REF%", result.Object.CheckNumberOrSlipId);
                            body = body.Replace("%Amount%", Utilities.FormatAmount(result.Object.Amount));
                            body = body.Replace("%CurrencyCode%", Utilities.GetCountry().CurrencyCode);
                            Utilities.SendEmail(email, emailTemplate.EmailSubject, body);
                        }
                    }
            }
            
            return JsonResult(new ActionOutput {Message = result.Message, Status = result.Status });
        }

        [HttpGet]
        public ActionResult GetVendorPosSelectList(long userId)
        {
            //ViewBag.SelectedTab = SelectedAdminTab.Deposits;
            var posList = _posManager.GetVendorPos(userId);
            return Json(new { posList }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet, Public]
        public ActionResult GetVendorHistoryReports(long vendor)
        {
            try
            { 
                var deposits = _depositManager.GetLastTenDepositPagedList(PagingModel.DefaultModel("CreatedAt", "Desc"), vendor);
                return PartialView("Partials/_depositListing", deposits);
            }
            catch (Exception)
            {
                return PartialView("Partials/_depositListing", new PagingResult<DepositListingModel>());
            }
        }

        [AjaxOnly, HttpPost, Public]
        public ActionResult GetDepositDetails(RequestObject tokenobject)
        {
            var result = _depositManager.GetDepositDetail(Convert.ToInt64(tokenobject.token_string), true);
            if (result.Object == null)
                return Json(new { Success = false, Code = 302, Msg = result.Message });
            return PartialView("_depositReceipt", result.Object);
        }
        #endregion
    }
}