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

function showHideNewSupplierField(executionContext) {
    var formContext = executionContext.getFormContext();
    var newSupplierField = formContext.getAttribute("leap_newsuppliername");

    if(newSupplierField.getValue() == '' || newSupplierField.getValue() == null) {
        hideNewSupplierField(formContext);
        showSupplierField(formContext);
    } else {
        hideSupplierField(formContext);
        showNewSupplierField(formContext);
    }
}