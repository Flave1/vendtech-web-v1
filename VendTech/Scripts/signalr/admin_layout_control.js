"use strict";

import { connection } from './admin_connection.js';

connection.on("UpdateAdminNotificationCount", function (message) {
    updateNotification();
});


window.onload = function () {
    updateNotification();
}
function updateNotification() {
    $.ajax({
        url: '/Admin/Home/GetNotification',
        success: function (data) {
            let remainingAppUser = 0;
            let remainingDepositRelease = 0;
            let notificationCount = 0;
            if (data.result.RemainingAppUser != null) {
                remainingAppUser = Number(data.result.RemainingAppUser);
                $('#remainingAppUser').html(remainingAppUser);
                $('#newusers_span').show();
            }
            else {
                remainingAppUser = 0;
                $('#remainingAppUser').html(remainingAppUser);
                $('#newusers_span').hide();
            }

            if (data.result.RemainingDepositRelease != null) {
                remainingDepositRelease = Number(data.result.RemainingDepositRelease);
                $('#remainingDepositRelease').html(remainingDepositRelease);
                $('#newdeposits_span').show();
            }
            else {
                remainingDepositRelease = 0;
                $('#remainingDepositRelease').html(remainingDepositRelease);
                $('#newdeposits_span').hide();
            }

            notificationCount = (remainingAppUser + remainingDepositRelease);
            $('#notificationCount').html(notificationCount);
        }
    })
}





