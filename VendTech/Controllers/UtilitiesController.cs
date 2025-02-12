#region Default Namespaces
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
#endregion

#region Custom Namespaces
using VendTech.Attributes;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using System.Web.Script.Serialization;
using VendTech.BLL.Common;
using Newtonsoft.Json;
using VendTech.BLL.PlatformApi;
using System.Threading.Tasks;
using System.Web.Http.Results;
#endregion

namespace VendTech.Controllers
{
    /// <summary>
    /// Home Controller 
    /// Created On: 10/04/2015
    /// </summary>
    public class UtilitiesController : AppUserBaseController
    {
        #region Variable Declaration
        private new readonly IUserManager _userManager;
        private new readonly IPlatformManager _platformManager;

        #endregion

        public UtilitiesController(
            IUserManager userManager, 
            IPlatformManager platformManager, 
            IErrorLogManager errorLogManager)
            : base(errorLogManager)
        {
            _userManager = userManager;
            _platformManager = platformManager;
        }

        public ActionResult Index()
        {
            ViewBag.UserId = LOGGEDIN_USER.UserID;
            ViewBag.walletBalance = _userManager.GetUserWalletBalance(LOGGEDIN_USER.UserID);
            ViewBag.SelectedTab = SelectedAdminTab.BillPayment;
            ViewBag.Pos = _userManager.GetUserDetailsByUserId(LOGGEDIN_USER.UserID).POSNumber;
            var model = new List<PlatformModel>();
            model = _platformManager.GetUserAssignedPlatforms(LOGGEDIN_USER.UserID);
            ViewBag.title = "Bill Payment";
            return View(model);
        }

    }
}