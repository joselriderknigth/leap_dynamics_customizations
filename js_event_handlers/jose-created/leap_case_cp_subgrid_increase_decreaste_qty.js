function processSelectedItems(selectedControlselecteditemsId,bool1) {
    // Check if the parameter is valid and not empty
    if (!selectedControlselecteditemsId || selectedControlselecteditemsId.length === 0) {
        alert("No items selected.");
        return;
    }

    selectedControlselecteditemsId.forEach(function (id) {
       var qty=0;
       var unitp = 0.0;
       const entityName = "leap_caseproduct"; 
       Xrm.WebApi.retrieveRecord(entityName, id, "?$select=leap_quantity,leap_unitprice,leap_unitpriceoverride,leap_io,leap_ioquantity,leap_overrideprice").then(
         function success(result) {
             qty = result.leap_quantity.toString();
             if (bool1){
                  qty++;
             }else{
                qty>1 ? qty-- : alert("the quantity is 1 or less, it suggest delete the product");
             }   
             unitp = result.leap_unitpriceoverride || result.leap_unitprice || 0; 
             if (result.leap_io){
                 var ioqty = result.leap_ioquantity || 0;
                 var unitpoverpric = result.leap_overrideprice || 0;
                updateFieldUsingWebApiIO(id, qty,unitp,ioqty,unitpoverpric)
               }else{                    
                   updateFieldUsingWebApi(id,qty,unitp); 
             }          
                
            //Xrm.Page.ui.refresh();  
         },
         function error(err) {
           console.error("Error retrieving the record:", err.message);
         }
       );            
     });

}


function updateFieldUsingWebApi(id, qty,unitp) {
    const entityName = "leap_caseproduct"; // Replace 'account' with your entity name

    // Define the payload
    const data = {};
    data["leap_quantity"] = qty;
    data["leap_caseextendedprice"] = qty*unitp;
    // Send the Web API request
    Xrm.WebApi.updateRecord(entityName, id, data).then(
        function success(result2) {
            console.log(`Record updated successfully. Record ID: ${result2.id}`);
            const subgrid = Xrm.Page.ui.controls.get("CaseProducts");
            subgrid .refresh();
        },
        function error(error) {
            console.error("Error updating the record:", error.message);
        }
    );
}

function updateFieldUsingWebApiIO(id, qty,unitp,ioqty,unitpoverpric) {
    const entityName = "leap_caseproduct"; // Replace 'account' with your entity name

    // Define the payload
    const data = {};
    data["leap_quantity"] = qty;
    data["leap_caseextendedprice"] = ((qty-ioqty)*unitp) + (ioqty*unitpoverpric);
    // Send the Web API request
    Xrm.WebApi.updateRecord(entityName, id, data).then(
        function success(result2) {
            console.log(`Record updated successfully. Record ID: ${result2.id}`);
              const subgrid = Xrm.Page.ui.controls.get("CaseProducts");
            subgrid .refresh();
        },
        function error(error) {
            console.error("Error updating the record:", error.message);
        }
    );
}


function customruleenable1increase(PrimaryControl) {
   console.log("testinvoked123..");
   //debugger;

   try{
     var formContext = PrimaryControl;
     var statuscase =  PrimaryControl.getAttribute("statuscode").getValue();
    console.log("statuscode value: ",statuscase);
     if( /* statuscase !== 293360001 && */ statuscase  !== 293360002 && statuscase  !== 293360005 &&  statuscase  !== 293360004)
         return true;
     else{ return false; }
   }catch(error){
         console.log("errore message: ",error);
   }

}

function disableFormSelector(){
   Xrm.Page.ui.formSelector.items.forEach(
      function(f){
        f.setVisible(false);
      }
    );
  }