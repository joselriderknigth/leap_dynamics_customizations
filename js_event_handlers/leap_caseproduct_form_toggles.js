/*
* PRODUCT
*/

function hideProductField(formContext) {
    var productFieldCtrl = formContext.getControl("leap_productid");
    var productField = formContext.getAttribute("leap_productid");
    var unitPriceFieldCtrl = formContext.getControl("leap_unitprice");
    var unitPriceField = formContext.getAttribute("leap_unitprice");
    var unitPriceOverrideFieldCtrl = formContext.getControl("leap_unitpriceoverride");
    var unitPriceOverrideField = formContext.getAttribute("leap_unitpriceoverride");

    productField.setValue(null);
    productField.setRequiredLevel('none');
    unitPriceField.setValue(null);
    unitPriceField.setRequiredLevel('none');
    productFieldCtrl.setVisible(false);
    unitPriceFieldCtrl.setVisible(false);
    unitPriceOverrideFieldCtrl.setLabel('New Product Price');
    unitPriceOverrideField.setValue(null);
    unitPriceOverrideField.setRequiredLevel('required');
}

function showProductField(formContext) {
    var productFieldCtrl = formContext.getControl("leap_productid");
    var productField = formContext.getAttribute("leap_productid");
    var unitPriceFieldCtrl = formContext.getControl("leap_unitprice");
    var unitPriceField = formContext.getAttribute("leap_unitprice");
    var unitPriceOverrideFieldCtrl = formContext.getControl("leap_unitpriceoverride");
    var unitPriceOverrideField = formContext.getAttribute("leap_unitpriceoverride");

    productFieldCtrl.setVisible(true);
    productField.setRequiredLevel('required');
    unitPriceFieldCtrl.setVisible(true);
    unitPriceField.setValue(null);
    unitPriceField.setRequiredLevel('required');
    unitPriceOverrideFieldCtrl.setLabel('Unit Price Override');
    unitPriceOverrideField.setValue(null);
    unitPriceOverrideField.setRequiredLevel('none');
}

function hideNewProductField(formContext) {
    var newProductFieldCtrl = formContext.getControl("leap_newproductname");
    var newProductIdFieldCtrl = formContext.getControl("leap_newproductid");
    var newProductField = formContext.getAttribute("leap_newproductname");
    var newProductIdField = formContext.getAttribute("leap_newproductid");

    newProductField.setValue(null);
    newProductIdField.setValue(null);
    newProductField.setRequiredLevel('none');
    newProductIdField.setRequiredLevel('none');
    newProductFieldCtrl.setVisible(false);
    newProductIdFieldCtrl.setVisible(false);
}

function showNewProductField(formContext) {
    var newProductFieldCtrl = formContext.getControl("leap_newproductname");
    var newProductIdFieldCtrl = formContext.getControl("leap_newproductid");
    var newProductField = formContext.getAttribute("leap_newproductname");
    var newProductIdField = formContext.getAttribute("leap_newproductid");

    newProductFieldCtrl.setVisible(true);
    newProductIdFieldCtrl.setVisible(true);
    newProductField.setRequiredLevel('required');
    newProductIdField.setRequiredLevel('required');
}

/*
* SUPPLIER
*/

function hideSupplierField(formContext) {
    var supplierFieldCtrl = formContext.getControl("leap_supplier");
    var supplierField = formContext.getAttribute("leap_supplier");
    var productType = formContext.getAttribute("leap_producttype");

    supplierField.setValue(null);
    supplierField.setRequiredLevel('none');
    supplierFieldCtrl.setVisible(false);
    productType.setValue(null);
}

function showSupplierField(formContext) {
    var supplierFieldCtrl = formContext.getControl("leap_supplier");
    var supplierField = formContext.getAttribute("leap_supplier");
    var productType = formContext.getAttribute("leap_producttype");

    supplierFieldCtrl.setVisible(true);
    supplierField.setRequiredLevel('required');
    productType.setValue(null);
}

function hideNewSupplierField(formContext) {
    var newSupplierFieldCtrl = formContext.getControl("leap_newsuppliername");
    var newSupplierField = formContext.getAttribute("leap_newsuppliername");

    newSupplierField.setValue(null);
    newSupplierField.setRequiredLevel('none');
    newSupplierFieldCtrl.setVisible(false);
}

function showNewSupplierField(formContext) {
    var newSupplierFieldCtrl = formContext.getControl("leap_newsuppliername");
    var newSupplierField = formContext.getAttribute("leap_newsuppliername");

    newSupplierFieldCtrl.setVisible(true);
    newSupplierField.setRequiredLevel('required');
}

/*
* HANDLERS
*/

function addNewProductHandler(executionContext) {
    var formContext = executionContext.getFormContext();
    var newProductToggle = formContext.getAttribute("leap_addanewproduct");
    var existingProduct = formContext.getAttribute("leap_existingproduct");
    var existingSupplierToggleCtrl = formContext.getControl("leap_existingsupplier");
    var existingSupplierToggle = formContext.getAttribute("leap_existingsupplier");

    if(newProductToggle.getValue() == 0) { // No
        existingProduct.setValue(true);
        showProductField(formContext);
        hideNewProductField(formContext);

        existingSupplierToggle.setValue(true);
        showSupplierField(formContext);
        hideNewSupplierField(formContext);
        existingSupplierToggleCtrl.setVisible(false);
    } else {
        existingProduct.setValue(false);
        hideProductField(formContext);
        showNewProductField(formContext);
        existingSupplierToggleCtrl.setVisible(true);
    }
}

function showHideProduct(executionContext) {
    var formContext = executionContext.getFormContext();
    var existingProduct = formContext.getAttribute("leap_existingproduct");

    if (existingProduct.getValue() == 0) { // No
        hideProductField(formContext);
        showNewProductField(formContext);
    } else { // Yes
        showProductField(formContext);
        hideNewProductField(formContext);
    }
}

function showHideSupplier(executionContext) {
    var formContext = executionContext.getFormContext();
    var existingsupplier = formContext.getAttribute("leap_existingsupplier");
    var existingProductCtrl = formContext.getControl("leap_existingproduct");
    var existingProduct = formContext.getAttribute("leap_existingproduct");

    if (existingsupplier.getValue() == 0) { // No
        hideProductField(formContext);
        showNewProductField(formContext);

        existingProduct.setValue(false);
        existingProductCtrl.setDisabled(true);
 
        hideSupplierField(formContext);
        showNewSupplierField(formContext);
    } else { // Yes
        existingProductCtrl.setDisabled(false);

        showSupplierField(formContext);
        hideNewSupplierField(formContext);
    }
}

function showHideProductDetails(executionContext) {
    var formContext = executionContext.getFormContext();
    var showProductDetails = formContext.getAttribute("leap_showproductdetails");

    var lotFieldCtrl = formContext.getControl("leap_lotnumber");
    var serialNoFieldCtrl = formContext.getControl("leap_serialnumber");
    var expirationFieldCtrl = formContext.getControl("leap_expirationdate");
    var stickersFieldCtrl = formContext.getControl("leap_stickers");

    if (showProductDetails.getValue() == 0) { // No
        lotFieldCtrl.setVisible(false);
        serialNoFieldCtrl.setVisible(false);
        expirationFieldCtrl.setVisible(false);
        stickersFieldCtrl.setVisible(false);
    } else { // Yes
        lotFieldCtrl.setVisible(true);
        serialNoFieldCtrl.setVisible(true);
        expirationFieldCtrl.setVisible(true);
        stickersFieldCtrl.setVisible(true);
    }
}