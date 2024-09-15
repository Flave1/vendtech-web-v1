using System.Threading.Tasks;
using System;
using VendTech.BLL.Interfaces;
using VendTech.BLL.Models;
using VendTech.DAL;
using System.Data.Entity;
using VendTech.DAL.DomainBuilders;
using VendTech.BLL.Common;
using System.Linq;

namespace VendTech.BLL.Managers
{
    public class BalanceDepositOperationsManager : IBalanceDepositOperationsManager
    {
        private readonly VendtechEntities _context;

        public BalanceDepositOperationsManager(VendtechEntities context)
        {
            _context = context;
        }

        public async Task<Deposit> CreateDeposit(DepositDTOV2 depositDto, long currentUserId, bool processPercentage)
        {
            //using (var transaction = _context.Database.BeginTransaction())  // Start transaction
            //{
                try
                {
                    var deposit = await CreateNewDeposit(depositDto, processPercentage);
                    await GenerateDepositLog(deposit, currentUserId);

                    //transaction.Commit();  // Commit the transaction
                    return deposit;
                }
                catch (Exception e)
                {
                    //transaction.Rollback();  // Rollback the transaction in case of an error
                    throw new ArgumentException(e.Message);
                }
                //finally { transaction.Dispose(); }
            //}
        }

        private async Task<Deposit> CreateNewDeposit(DepositDTOV2 newDepositDto, bool processPercentage)
        {
            var deposit = GenerateDeposit(newDepositDto);
            await CalculatePercentageAmount(deposit, processPercentage);
            _context.Deposits.Add(deposit);
            await _context.SaveChangesAsync();


            //GENERATE COMMISSION FOR DEPOSIT
            if (newDepositDto.Amount > 0 && deposit.POS.Commission.Percentage > 0)
            {
                DepositDTOV2 commisionDepositDto = newDepositDto;
                commisionDepositDto.Amount = (deposit.PercentageAmount.Value - deposit.Amount);
                await GenerateCommission(commisionDepositDto, false);
            }


            //GENERATE COMMISSION IF IS AGENCY ADMIN
            long vendorId = newDepositDto.UserId;
            long agencyId = deposit.POS?.User.AgentId.Value ?? 0;
            long vendorAdminId = _context.Agencies.FirstOrDefault(d => d.AgencyId == agencyId)?.Representative ?? 0;
            if (newDepositDto.Amount > 0 && agencyId != Utilities.VENDTECH && agencyId != 0) 
            {
                if (vendorId != vendorAdminId)
                {
                    POS adminPos = _context.POS.FirstOrDefault(d => d.VendorId == vendorAdminId);
                    if(adminPos != null)
                    {
                        DepositDTOV2 commisionDepositDto = newDepositDto;
                        commisionDepositDto.Amount = (deposit.PercentageAmount.Value - deposit.Amount);
                        commisionDepositDto.UserId = vendorAdminId;
                        commisionDepositDto.POSId = adminPos.POSId;
                        await GenerateCommission(commisionDepositDto, false);
                    }
                }
            }

            return deposit;
        }

        private async Task GenerateCommission(DepositDTOV2 depositDto, bool processPercentage)
        {
            depositDto.PaymentType = (int)DepositPaymentTypeEnum.AgencyCommision;
            var deposit = GenerateDeposit(depositDto);
            await CalculatePercentageAmount(deposit, processPercentage);
            _context.Deposits.Add(deposit);
            await _context.SaveChangesAsync();
            await GenerateDepositLog(deposit, deposit.UserId);
            return;
        }

        private async Task<Deposit> CalculatePercentageAmount(Deposit deposit, bool processPercentage)
        {
            var pos = await _context.POS.FirstOrDefaultAsync(d => d.VendorId == deposit.UserId);
            if (pos != null)
            {
                if (processPercentage)
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

        private Deposit GenerateDeposit(DepositDTOV2 depositDto)
        {
            return new DepositBuilder()
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
        }
        
        private async Task GenerateDepositLog(Deposit deposit, long currentUserId)
        {
            //Creating Log entry in deposit logs table
            var dbDepositLog = new DepositLog();
            dbDepositLog.UserId = currentUserId;
            dbDepositLog.DepositId = deposit.DepositId;
            dbDepositLog.PreviousStatus = (int)DepositPaymentStatusEnum.Released;
            dbDepositLog.NewStatus = (int)DepositPaymentStatusEnum.Released;
            dbDepositLog.CreatedAt = DateTime.UtcNow;
            _context.DepositLogs.Add(dbDepositLog);
            await _context.SaveChangesAsync();
        }
    }
}

