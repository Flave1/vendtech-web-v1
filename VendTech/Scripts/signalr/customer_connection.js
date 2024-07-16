"use strict";

const live = "https://www.vendtechsl.com:459/customerHub";
const local = "https://localhost:7246/customerHub";
const dev = "http://subs.vendtechsl.net/customerHub";

var connection = new signalR.HubConnectionBuilder()
    .withUrl(live, {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets
    })
    .configureLogging(signalR.LogLevel.Information)
    .build();


connection.start().then(function () {
    console.log("Customer SignalR connected.");
}).catch(function (err) {
    console.error("SignalR connection error: ", err);
});

export { connection }