using System;

namespace VendTech.DAL.DomainBuilders
{
    public class DepositBuilder
    {
        private readonly Deposit _deposit;

        public DepositBuilder()
        {
            _deposit = new Deposit
            {
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }

        public DepositBuilder(Deposit deposit)
        {
             _deposit = deposit;
        }

        public DepositBuilder WithDepositId(long depositId)
        {
            _deposit.DepositId = depositId;
            return this;
        }

        public DepositBuilder WithUserId(long userId)
        {
            _deposit.UserId = userId;
            return this;
        }

        public DepositBuilder WithPOSId(long posId)
        {
            _deposit.POSId = posId;
            return this;
        }

        public DepositBuilder WithTransactionId(string transactionId)
        {
            _deposit.TransactionId = transactionId;
            return this;
        }

        public DepositBuilder WithPaymentType(int paymentType)
        {
            _deposit.PaymentType = paymentType;
            return this;
        }

        public DepositBuilder WithBalanceBefore(decimal? balanceBefore)
        {
            _deposit.BalanceBefore = balanceBefore;
            return this;
        }

        public DepositBuilder WithAmount(decimal amount)
        {
            _deposit.Amount = amount;
            return this;
        }

        public DepositBuilder WithPercentageAmount(decimal? percentageAmount)
        {
            _deposit.PercentageAmount = percentageAmount;
            return this;
        }

        public DepositBuilder WithNewBalance(decimal? newBalance)
        {
            _deposit.NewBalance = newBalance;
            return this;
        }

        public DepositBuilder WithAgencyCommission(decimal? agencyCommission)
        {
            _deposit.AgencyCommission = agencyCommission;
            return this;
        }

        public DepositBuilder WithCheckNumberOrSlipId(string checkNumberOrSlipId)
        {
            _deposit.CheckNumberOrSlipId = checkNumberOrSlipId;
            return this;
        }

        public DepositBuilder WithComments(string comments)
        {
            _deposit.Comments = comments;
            return this;
        }

        public DepositBuilder WithStatus(int status)
        {
            _deposit.Status = status;
            return this;
        }

        public DepositBuilder WithChequeBankName(string chequeBankName)
        {
            _deposit.ChequeBankName = chequeBankName;
            return this;
        }

        public DepositBuilder WithNameOnCheque(string nameOnCheque)
        {
            _deposit.NameOnCheque = nameOnCheque;
            return this;
        }

        public DepositBuilder WithUpdatedAt(DateTime? updatedAt)
        {
            _deposit.UpdatedAt = updatedAt;
            return this;
        }

        public DepositBuilder WithBankAccountId(int bankAccountId)
        {
            _deposit.BankAccountId = bankAccountId;
            return this;
        }

        public DepositBuilder WithIsAudit(bool isAudit)
        {
            _deposit.isAudit = isAudit;
            return this;
        }

        public DepositBuilder WithValueDate(string valueDate)
        {
            _deposit.ValueDate = valueDate;
            return this;
        }

        public DepositBuilder WithNextReminderDate(DateTime? nextReminderDate)
        {
            _deposit.NextReminderDate = nextReminderDate;
            return this;
        }

        public DepositBuilder WithIsDeleted(bool isDeleted)
        {
            _deposit.IsDeleted = isDeleted;
            return this;
        }

        public DepositBuilder WithValueDateStamp(DateTime? valueDateStamp)
        {
            _deposit.ValueDateStamp = valueDateStamp;
            return this;
        }

        public Deposit Build()
        {
            return _deposit;
        }
    }

}
