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

    productField.setRequiredLevel('none');
    unitPriceField.setRequiredLevel('none');
    productFieldCtrl.setVisible(false);
    unitPriceFieldCtrl.setVisible(false);
    unitPriceOverrideFieldCtrl.setLabel('New Product Price');
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
    unitPriceField.setRequiredLevel('required');
    unitPriceOverrideFieldCtrl.setLabel('Unit Price Override');
    unitPriceOverrideField.setRequiredLevel('none');
}

function hideNewProductField(formContext) {
    var newProductFieldCtrl = formContext.getControl("leap_newproductname");
    var newProductIdFieldCtrl = formContext.getControl("leap_newproductid");
    var newProductField = formContext.getAttribute("leap_newproductname");
    var newProductIdField = formContext.getAttribute("leap_newproductid");

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

    supplierField.setRequiredLevel('none');
    supplierFieldCtrl.setVisible(false);
}

function showSupplierField(formContext) {
    var supplierFieldCtrl = formContext.getControl("leap_supplier");
    var supplierField = formContext.getAttribute("leap_supplier");

    supplierFieldCtrl.setVisible(true);
    supplierField.setRequiredLevel('required');
}

function hideNewSupplierField(formContext) {
    var newSupplierFieldCtrl = formContext.getControl("leap_newsuppliername");
    var newSupplierField = formContext.getAttribute("leap_newsuppliername");

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

function onLoadMainFormHandler(executionContext) {
    var formContext = executionContext.getFormContext();
    var newProductToggle = formContext.getAttribute("leap_addanewproduct");
    var existingProductCtrl = formContext.getControl("leap_existingproduct");
    var existingSupplierToggleCtrl = formContext.getControl("leap_existingsupplier");
    var existingSupplierToggle = formContext.getAttribute("leap_existingsupplier");

    if(newProductToggle.getValue() == 0) { // No
        showProductField(formContext);
        hideNewProductField(formContext);

        showSupplierField(formContext);
        hideNewSupplierField(formContext);
        existingSupplierToggleCtrl.setVisible(false);
    } else {
        hideProductField(formContext);
        showNewProductField(formContext);
        // existingSupplierToggleCtrl.setVisible(true); // hide toggle for now
    }

    if (existingSupplierToggle.getValue() == 0) { // No
        hideProductField(formContext);
        showNewProductField(formContext);

        existingProductCtrl.setDisabled(true);
 
        hideSupplierField(formContext);
        showNewSupplierField(formContext);
    } else { // Yes
        existingProductCtrl.setDisabled(false);

        showSupplierField(formContext);
        hideNewSupplierField(formContext);
    }
}