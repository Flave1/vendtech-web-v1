using Quartz;
using System;
using System.Web.Mvc;
using VendTech.BLL.Interfaces;

namespace VendTech.BLL.Jobs
{
    public class EdsaTransactionSheduleJob : IJob
    {
     
        public void Execute(IJobExecutionContext context)
        {
            var _errorManager = DependencyResolver.Current.GetService<IErrorLogManager>();
            var _salesService = DependencyResolver.Current.GetService<IVendtechExtensionSales>();

            try
            {
                _salesService.CheckPendingTransaction();
            }
            catch (Exception ex)
            {
                _errorManager.LogExceptionToDatabase(new Exception("EdsaTransactionSheduleJob Error", ex));
            }
        }
    }
}
