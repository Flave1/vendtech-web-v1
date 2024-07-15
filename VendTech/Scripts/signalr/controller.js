"use strict";

const live = "https://www.vendtechsl.com:459/hub";
const local = "https://localhost:7246/hub";
const dev = "http://subs.vendtechsl.net/hub";

const userId = userBalanceHandler.userId;

var connection = new signalR.HubConnectionBuilder()
    .withUrl(live, {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets
    })
    .configureLogging(signalR.LogLevel.Information)
    .build();

connection.on("SendBalanceUpdate", function (user) {
    if (userId === user.toString()) {
        updateBalance(true);
    }
});

connection.start().then(function () {
    console.log("SignalR connected.");
}).catch(function (err) {
    console.error("SignalR connection error: ", err);
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

function testSignalServer() {
    var url = "https://www.vendtechsl.com:459/Balance/update";
    $.ajax({
        url: url,
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ userId: '40251' }),
        success: function (response) {
            console.log("Response", response);
        },
        error: function (xhr, status, error) {
            console.error("Error:", xhr.responseText);
        }
    });
}

function updateBalanceIfOnSalesScreen(amount) {
    if (location.pathname === "/Meter/Recharge" || location.pathname === "/Airtime/Recharge") {
        salesHandler.amount = amount;
    }
}
