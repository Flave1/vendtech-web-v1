using Quartz;
using System;
using System.Linq;
using System.Web.Mvc;
using VendTech.BLL.Common;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;

namespace VendTech.BLL.Jobs
{
    public class CommonShedulesJob : IJob
    {
     
        public void Execute(IJobExecutionContext context)
        {
            var _errorManager = DependencyResolver.Current.GetService<IErrorLogManager>();
            var _idGenrator = DependencyResolver.Current.GetService<TransactionIdGenerator>();


            try
            {
                _idGenrator.EmptyTransactionId(); 
                _errorManager.LogExceptionToDatabase(new Exception("Successfully cleared in-memory ids"));
            }
            catch (Exception ex)
            {
                _errorManager.LogExceptionToDatabase(new Exception("Error ocuured trying to clear IDS", ex));
            }
        }
    }
}
