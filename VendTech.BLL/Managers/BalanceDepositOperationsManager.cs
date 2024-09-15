using System.Threading.Tasks;
using System;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.DAL;
using System.Data.Entity;
using VendTech.DAL.DomainBuilders;
using VendTech.BLL.Common;

namespace VendTech.BLL.Managers
{
    public class BalanceDepositOperationsManager : IBalanceDepositOperationsManager
    {
        private readonly VendtechEntities _context;

        public BalanceDepositOperationsManager(VendtechEntities entities)
        {
            _context = entities;
        }

        async Task<Deposit> IBalanceDepositOperationsManager.CreateDeposit(DepositDTOV2 depositDto, long currentUserId, bool isCreditDeposit)
        {
            try
            {
                var deposit = await CreateNewDeposit(depositDto, isCreditDeposit);

                //Creating Log entry in deposit logs table
                var dbDepositLog = new DepositLog();
                dbDepositLog.UserId = currentUserId;
                dbDepositLog.DepositId = deposit.DepositId;
                dbDepositLog.PreviousStatus = (int)DepositPaymentStatusEnum.Released;
                dbDepositLog.NewStatus = (int)DepositPaymentStatusEnum.Released;
                dbDepositLog.CreatedAt = DateTime.UtcNow;
                _context.DepositLogs.Add(dbDepositLog);
                await _context.SaveChangesAsync();

                
                return deposit;
            }
            catch (Exception e)
            {
                throw new ArgumentException(e.Message);
            }
        }

        private async Task<Deposit> CreateNewDeposit(DepositDTOV2 depositDto, bool isCreditDeposit)
        {
            var newDeposit = new DepositBuilder()
                        .WithCheckNumberOrSlipId(depositDto.CheckNumberOrSlipId)
                        .WithTransactionId(Utilities.NewDepositTransactionId())
                        .WithStatus((int)DepositPaymentStatusEnum.Released)
                        .WithNextReminderDate(depositDto.NextReminderDate)
                        .WithValueDateStamp(depositDto.ValueDateStamp)
                        .WithChequeBankName(depositDto.ChequeBankName)
                        .WithBankAccountId(depositDto.BankAccountId)
                        .WithNameOnCheque(depositDto.NameOnCheque)
                        .WithPaymentType(depositDto.PaymentType)
                        .WithValueDate(depositDto.ValueDate)
                        .WithUpdatedAt(depositDto.UpdatedAt)
                        .WithComments(depositDto.Comments)
                        .WithIsAudit(depositDto.IsAudit)
                        .WithAmount(depositDto.Amount)
                        .WithUserId(depositDto.UserId)
                        .WithPOSId(depositDto.POSId)
                        .WithAgencyCommission(0)
                        .WithIsDeleted(false)
                        .Build();

            await CalculatePercentageAmount(newDeposit, isCreditDeposit);
            _context.Deposits.Add(newDeposit);
            _context.SaveChanges();
            return newDeposit;
        }

        private async Task<Deposit> CalculatePercentageAmount(Deposit deposit, bool isCreditDeposit)
        {
            var pos = await _context.POS.FirstOrDefaultAsync(d => d.VendorId == deposit.UserId);
            if(pos != null)
            {
                if (isCreditDeposit)
                {
                    decimal commission = 0;
                    var percentage = pos.Commission.Percentage;
                    commission = deposit.Amount * percentage / 100;

                    deposit.BalanceBefore = pos.Balance;
                    pos.Balance = pos.Balance == null ? commission + deposit.Amount : pos.Balance.Value + commission + deposit.Amount;
                    deposit.NewBalance = pos.Balance;
                    deposit.PercentageAmount = deposit.Amount + commission;
                }
                else
                {
                    deposit.BalanceBefore = pos.Balance;
                    pos.Balance = pos.Balance == null ? deposit.Amount : pos.Balance + deposit.Amount;
                    deposit.NewBalance = pos.Balance;
                    deposit.PercentageAmount = deposit.Amount;
                }
                return deposit;
            }
            return deposit;
        }
    }
}

