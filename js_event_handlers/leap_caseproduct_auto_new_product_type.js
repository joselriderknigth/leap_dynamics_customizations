function autoSetProductTypeForNewProduct(executionContext) {
    var formContext = executionContext.getFormContext();
    var productType = formContext.getAttribute("leap_producttype");
    
    var supplier = formContext.getAttribute("leap_supplier");
    var existingProduct = formContext.getAttribute("leap_existingproduct");
    var existingSupplier = formContext.getAttribute("leap_existingsupplier");

    if (existingProduct.getValue() == 0 && existingSupplier.getValue() == 1 && supplier.getValue() != null) {
        Xrm.WebApi.retrieveRecord("account", supplier.getValue()[0].id, "?$select=name").then(
            function success(result) {
                if (result == null) {
                    console.log("JSEventHandler - retrieved account is null.");
                } else {
                    console.log("JSEventHandler - retrieved account name: " + result.name);
                    if(result.name == 'LEAP SURGICAL') {
                        productType.setValue(293360000);
                    } else {
                        productType.setValue(293360001);
                    }
                }
            },
            function (error) {
                console.log("JSEventHandler - error retrieving account record.", error.message);
            }
        );
    } else if(supplier.getValue() == null) {
        productType.setValue(null);
    }
}

function autoSetProductTypeForNewProductBasedOnToggle(executionContext) {
    var formContext = executionContext.getFormContext();
    var productType = formContext.getAttribute("leap_producttype");

    var supplier = formContext.getAttribute("leap_supplier");
    var newProductToggle = formContext.getAttribute("leap_addanewproduct");
    var existingSupplier = formContext.getAttribute("leap_existingsupplier");

    if(supplier.getValue() != null && newProductToggle.getValue() == 1) {
        Xrm.WebApi.retrieveRecord("account", supplier.getValue()[0].id, "?$select=name").then(
            function success(result) {
                if (result == null) {
                    console.log("JSEventHandler - retrieved account is null.");
                } else {
                    console.log("JSEventHandler - retrieved account name: " + result.name);
                    if(result.name == 'LEAP SURGICAL') {
                        productType.setValue(293360000);
                    } else {
                        productType.setValue(293360001);
                    }
                }
            },
            function (error) {
                console.log("JSEventHandler - error retrieving account record.", error.message);
            }
        );
    } else if(productType.getValue() != null && newProductToggle.getValue() == 1) {
        productType.setValue(null);
    }
}