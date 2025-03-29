using Quartz;
using System;
using System.Web.Mvc;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;

namespace VendTech.BLL.Jobs
{
    public class CommonShedulesJob : IJob
    {
     
        public void Execute(IJobExecutionContext context)
        {
            var _errorManager = DependencyResolver.Current.GetService<IErrorLogManager>();
            var _userManager = DependencyResolver.Current.GetService<IUserManager>();
            var _idGenrator = DependencyResolver.Current.GetService<TransactionIdGenerator>();


            try
            {
                _idGenrator.EmptyTransactionId();
                _userManager.DisposeUserNotifications();
                _errorManager.LogExceptionToDatabase(new Exception("CommonShedulesJobs Run"));
            }
            catch (Exception ex)
            {
                _errorManager.LogExceptionToDatabase(new Exception("CommonShedulesJobs Error", ex));
            }
        }
    }
}
