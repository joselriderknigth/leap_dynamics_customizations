/*
* PRODUCT
*/

function hideNewProductField(formContext) {
    var newProductFieldCtrl = formContext.getControl("leap_newproductname");
    var newProductField = formContext.getAttribute("leap_newproductname");

    newProductFieldCtrl.setVisible(false);
    newProductField.setRequiredLevel('none');
}

function hideProductField(formContext) {
    var productFieldCtrl = formContext.getControl("leap_product");
    var productField = formContext.getAttribute("leap_product");

    productFieldCtrl.setVisible(false);
    productField.setRequiredLevel('none');
}

function showNewProductField(formContext) {
    var newProductFieldCtrl = formContext.getControl("leap_newproductname");
    var newProductField = formContext.getAttribute("leap_newproductname");

    newProductFieldCtrl.setVisible(true);
    newProductField.setRequiredLevel('required');
}

function showProductField(formContext) {
    var productFieldCtrl = formContext.getControl("leap_product");
    var productField = formContext.getAttribute("leap_product");

    productFieldCtrl.setVisible(true);
    productField.setRequiredLevel('required');
}

/*
* SUPPLIER
*/

function hideNewSupplierField(formContext) {
    var newSupplierFieldCtrl = formContext.getControl("leap_newsuppliername");
    var newSupplierField = formContext.getAttribute("leap_newsuppliername");

    newSupplierFieldCtrl.setVisible(false);
    newSupplierField.setRequiredLevel('none');
}

function hideSupplierField(formContext) {
    var supplierFieldCtrl = formContext.getControl("leap_supplier");
    var supplierField = formContext.getAttribute("leap_supplier");

    supplierFieldCtrl.setVisible(false);
    supplierField.setRequiredLevel('none');
}

function showNewSupplierField(formContext) {
    var newSupplierFieldCtrl = formContext.getControl("leap_newsuppliername");
    var newSupplierField = formContext.getAttribute("leap_newsuppliername");

    newSupplierFieldCtrl.setVisible(true);
    newSupplierField.setRequiredLevel('required');
}

function showSupplierField(formContext) {
    var supplierFieldCtrl = formContext.getControl("leap_supplier");
    var supplierField = formContext.getAttribute("leap_supplier");

    supplierFieldCtrl.setVisible(true);
    supplierField.setRequiredLevel('required');
}

/*
* HANDLER
*/

function showHideNewSupplierProductFields(executionContext) {
    var formContext = executionContext.getFormContext();
    var newSupplierField = formContext.getAttribute("leap_newsuppliername");
    var newProductField = formContext.getAttribute("leap_newproductname");

    if(newSupplierField.getValue() == '' || newSupplierField.getValue() == null) {
        hideNewSupplierField(formContext);
        showSupplierField(formContext);
    } else {
        hideSupplierField(formContext);
        showNewSupplierField(formContext);
    }

    if(newProductField.getValue() == '' || newProductField.getValue() == null) {
        hideNewProductField(formContext);
        showProductField(formContext);
    } else {
        hideProductField(formContext);
        showNewProductField(formContext);
    }
}