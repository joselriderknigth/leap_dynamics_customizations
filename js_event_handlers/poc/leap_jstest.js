function populateJSTest(executionContext) {
    var formContext = executionContext.getFormContext();
    var testToggle = formContext.getAttribute("leap_testtoggle").getValue();

    if(testToggle == 293360000) {
        console.log("JSEventHandler - selected JS option in toggle");

        var productId = formContext.getAttribute("leap_productid").getValue()[0].id ;
        if (productId == null) {
            console.log("JSEventHandler - no product ID found");
        } else {
            console.log("JSEventHandler - product ID found: ", productId);
            parent.Xrm.WebApi.retrieveRecord("product", productId, "?$select=leap_bcitemid").then(
                function success(result) {
                    if (result == null) {
                        console.log("JSEventHandler - retrieved product is null.");
                    } else {
                        console.log("JSEventHandler - retrieved product BC Item ID: " + result.leap_bcitemid);
                        formContext.getAttribute("leap_jstest").setValue(result.leap_bcitemid);
                    }
                },
                function (error) {
                    console.log("JSEventHandler - error retrieving product record.", error.message);
                }
            );
        }
    } else {
        console.log("JSEventHandler - selected non JS option in toggle");
        formContext.getAttribute("leap_jstest").setValue("");
    }
}
