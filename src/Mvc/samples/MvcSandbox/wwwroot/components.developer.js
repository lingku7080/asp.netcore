"use strict";

document.addEventListener("DOMContentLoaded", function (event) {

    var diagnosticsElement = document.getElementById("component-diagnostics");
    if (!diagnosticsElement) {
        return;
    }

    var statusBarElement = document.createElement("div");
    statusBarElement.classList.add("component-diagnostics-statusbar");
    diagnosticsElement.appendChild(statusBarElement);

    var expandElement = document.createElement("button");
    expandElement.classList.add("component-exception-button");
    expandElement.innerText = "^";
    statusBarElement.appendChild(expandElement);

    var headingElement = document.createElement("h3");
    headingElement.classList.add("component-diagnostics-heading");
    statusBarElement.appendChild(headingElement);

    var dismissElement = document.createElement("button");
    dismissElement.classList.add("component-exception-button");
    dismissElement.innerText = "X";
    statusBarElement.appendChild(dismissElement);

    var detailsElement = document.createElement("p");
    detailsElement.classList.add("component-exception-display");
    diagnosticsElement.appendChild(detailsElement);

    expandElement.onclick = function details() {
        diagnosticsElement.style.height = "500px";
        detailsElement.style.height = "450px";
    };

    dismissElement.onclick = function hide() {
        headingElement.innerText = "";
        detailsElement.innerText = "";
        diagnosticsElement.style.height = "0px";
        detailsElement.style.height = "0px";
        statusBarElement.style.height = "0px";
    };

    window.Blazor.unhandledException = function unhandledException(e) {
        headingElement.innerText = "An unhandled exception has occurred.";
        detailsElement.innerText = e;

        diagnosticsElement.style.height = "50px";
        statusBarElement.style.height = "50px";
    };
});

