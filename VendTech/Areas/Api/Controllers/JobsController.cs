using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using VendTech.Attributes;
using VendTech.BLL.Interfaces;
using VendTech.Framework.Api;

namespace VendTech.Areas.Api.Controllers
{
    public class JobsController : BaseAPIController
    {
        private readonly IUserManager _userManager;
        public JobsController(IUserManager userManager,
            IErrorLogManager errorLogManager)
            : base(errorLogManager)
        {
            _userManager = userManager;
        }

        [HttpPost, CheckAuthorizationAttribute.SkipAuthentication, CheckAuthorizationAttribute.SkipAuthorization]
        [ResponseType(typeof(ResponseBase))]
        public HttpResponseMessage StartAirtimePendingTransactionCheck()
        {
            JobScheduler.Start();
            var aa = _userManager.GetWelcomeMessage();
            return new JsonContent("JOB STARTED SUCCESSFULLY", Status.Success).ConvertToHttpResponseOK();
        }
    }
}
