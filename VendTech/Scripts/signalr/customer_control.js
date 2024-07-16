"use strict";
import { connection } from './customer_connection.js';


const userId = userBalanceHandler.userId;


connection.on("SendBalanceUpdate", function (user) {
    if (userId.toString() === user) {
        updateBalance(true);
    }
});


window.onload = function () {
    updateBalance(false);
};

function updateBalance(animate = false) {
    $.ajax({
        url: `/Api/User/GetWalletBalance2?userId=${userBalanceHandler.userId}`,
        success: function (res) {
            if (res.status === "true") {
                const balancebox = document.getElementById('balancebox');
                const userBalance = document.getElementById('userBalance');
                const userBalanceBar = document.getElementById('userBalanceBar');
                const pendingBalancebox = document.getElementById('pending-approval');
                const pendingBalance = document.getElementById('pendingAmount');

                if (userBalance) userBalance.innerHTML = res.result.stringBalance;
                if (userBalanceBar) userBalanceBar.innerHTML = res.result.stringBalance;

                if (res.result.isDepositPending) {
                    if (pendingBalancebox) pendingBalancebox.style.display = "block";
                    if (pendingBalance) pendingBalance.innerHTML = res.result.pendingDepositBalance;
                } else {
                    if (pendingBalancebox) pendingBalancebox.style.display = "none";
                }

                updateBalanceIfOnSalesScreen(res.result.balance);

                if (animate && balancebox) {
                    balancebox.classList.add('animatorstyle');
                    setTimeout(() => {
                        balancebox.classList.remove('animatorstyle');
                    }, 10000);
                }
            }
        },
        error: function (err) {
            console.error('Error:', err);
        }
    });
}

function updateBalanceIfOnSalesScreen(amount) {
    if (location.pathname === "/Meter/Recharge" || location.pathname === "/Airtime/Recharge") {
        salesHandler.amount = amount;
    }
}
