//type = "text/javascript" >
//    function preventBack() {
//        window.history.forward();
//    }

//setTimeout("preventBack()", 0);
//window.onunload = function () { null };

var username = document.getElementById("username").value;
var password = document.getElementById("password").value;
//var emailPattern = /\S+@\S+\.\S+/;

function returnToPreviousPage() {
    window.history.back();
}

function validateUsername() {
    if (document.forms["loginform"].username.value == "") {
        alert("De gebruikersnaam is onjuist")
        return false;
    }
    else {
        return true
    }
}

function validatePassword() {
    if (document.forms["loginform"].password.value == "") {
        alert("Wachtwoord is onjuist")
        return false;
    }
    else {
        return true;
    }
}

function validateAll() {
    if (!validateUsername() | !validatePassword()) {
        //window.location = "login_wrong.html"
        return false;
    }
    else {
        //window.location = "Login1";
        return true;
    }
}