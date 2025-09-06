function validateFaxNumber(executionContext) {
    var formContext = executionContext.getFormContext(); // Get the form context
    var faxField = formContext.getAttribute("leap_frontdeskfax"); // Get the Fax field

    if (faxField && faxField.getValue() !== null) {
        var faxNumber = faxField.getValue();
        var regex =/^\+?[0-9]{1,3}[-. ]?(\([0-9]{3}\)|[0-9]{3})[-. ]?[0-9]{3}[-. ]?[0-9]{4}$/;
        var regex2 = /^\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}$/;
        let regex3 = /^\d{10}$/;

        //if (!regex.test(faxNumber) && !regex2.test(faxNumber)) {
        if (!regex3.test(faxNumber)) {
            //formContext.getControl("leap_frontdeskfax").setNotification("Invalid FAX number format! Use formats like +1-234-567-8901 or (123) 456-7890.", "FaxError");
            formContext.getControl("leap_frontdeskfax").setNotification("Invalid FAX number format! only 10 digits.", "FaxError");
        } else {
            // Remove the error if the format is correct
            formContext.getControl("leap_frontdeskfax").clearNotification("FaxError");
        }
    }
}


function customruleenable1usgdoc(PrimaryControl) {
   //console.log("testdoc126..");  
   try{
     var formContext = PrimaryControl;
     var caseid =  PrimaryControl.getAttribute("statuscode").getValue();
     //alert("caseid: "+ caseid);
   }catch(error){
         console.log("errore message: ",error);
   }
}


function processSelectedItemssdoc(selectedControlselecteditemsId,bool1) {
  //console.log("testdoc334..");
   //debugger;
    if (!selectedControlselecteditemsId || selectedControlselecteditemsId.length === 0) {
        alert("No items selected.");
        return;
    }
    let customParam = "";
    let shouldStop = false;
    var jsonArray = [];
     const entityId = Xrm.Page.data.entity.getId().toString(); // Get the current record ID
      const entityName = Xrm.Page.data.entity.getEntityName(); // Get the entity name
   
        var cleanEntityId = entityId.replace(/[{}]/g, "");
              
    selectedControlselecteditemsId.forEach(async function (id, index, array) {
        if (shouldStop) return;
        let finalurl;
        let fileName;
        var qty=0;
        var unitp = 0.0;
        const entityNamesh = "sharepointdocument"; 
       
        const getxdocsurls = await getxdocsUrl(entityId);
            for(const getxdocurl of getxdocsurls) {
                if (id == getxdocurl.sharepointdocumentid) {
                    finalurl = getxdocurl.absoluteurl;
                    fileName = getxdocurl.fullname;
                }
             }
                   
            if ((finalurl === null || finalurl === undefined || finalurl === "" || fileName === null || fileName === undefined || fileName === "") && !shouldStop) {
               //debugger;
                alert("UF documents might still be regenerating. Please wait for all documents to finish. In the meantime you can manually refresh the list.");                                      
                shouldStop = true;
                if (index === selectedControlselecteditemsId.length - 1) {
                    Xrm.Page.data.refresh(); 
                    var tabControl = Xrm.Page.ui.tabs.get("tab_3");
                    if (tabControl) {
                        tabControl.setFocus();
                    }
                  return;
                }
            }
            
            if (shouldStop){ 
                Xrm.Page.data.refresh(); 
                var tabControl = Xrm.Page.ui.tabs.get("tab_3");
                if (tabControl) {
                    tabControl.setFocus();
                }
                return;
             } 

             jsonArray.push({ "documentId": id , "absoluteUrl": finalurl, "name": fileName });

         if (index === selectedControlselecteditemsId.length - 1) {
               var formId = ""; // Replace with your actual formId
               const getxCacheVares = await getxCacheVars("FormIdEmailFax");
               for(const getxCacheVar of getxCacheVares) {
                 formId = getxCacheVar.north52_valuesecured;
               }
               

               // Construct the pageInput object
               var pageInput = {
                   pageType: "entityrecord", // Set pageType to "entityrecord" for entity forms
                    entityName: entityName,
                    formId: formId, // Set the specific formId to open
                    entityId: entityId, // Optional: Include if you want to navigate to a specific record
                    data: {
                       leap_selectedusfdocumentids: JSON.stringify(jsonArray), // Key-value pair for parameters to pass
                    },
                };
      
                // Define navigation options for dialog mode
                var navigationOptions = {
                    target: 2, // 2 means open in a dialog
                    position: 1, // 1 means centered
                    width: { value: 500, unit: "px" }, // Optional: Set the width of the dialog
                    height: { value: 600, unit: "px" }, // Optional: Set the height of the dialog
                };
      
                 // Open the form in dialog mode
                Xrm.Navigation.navigateTo(pageInput, navigationOptions)
                     .then(function () {
                       console.log("Form opened in dialog successfully.");
                 })
                 .catch(function (error) {
                     console.error("Failed to open form in dialog: ", error);
                  });

             }
     });    
 }

function getCaseInfo(caseId) {
    return Xrm.WebApi.retrieveRecord("leap_case", caseId, "?$select=leap_casedate&$expand=leap_Hospital($select=accountid),owninguser($select=systemuserid),leap_Surgeon($select=contactid)")
        .then(result => {
            if (result == null) {
                throw Error("Invalid operation result when attempting to retrieve case information.");
            }
            return result;
        }).catch(error => {
            throw Error(`Error retrieving case with id ${caseId}: ${error.message}`);
        });
}


async function generateEmailFax(executionContext) {
    //console.log("testdoc133..");
    //debugger;
    if(!isTriggered) {
        isTriggered = true;
             var finalfax="";
            const caseId = Xrm.Page.data.entity.getId().toString();
            const caseInfo = await getCaseInfo(caseId);
            
            const getxCacheVares = await getxCacheVars("SubfixFrontDeskFax");
            for(const getxCacheVar of getxCacheVares) {
                    finalfax = getxCacheVar.north52_valuesecured;
             }

            const entityName2 = "account"; 
            var account_id =`${caseInfo.leap_Hospital.accountid}`;
          Xrm.WebApi.retrieveRecord(entityName2,account_id , "?$select=leap_frontdeskfax").then(
            function success(result) {
                finalfax = result.leap_frontdeskfax.toString()+finalfax; 
                executionContext.getFormContext().getAttribute("leap_emailnotes").setValue(finalfax);             
            }),
           function error(err) {
             console.error("Error retrieving the record:", err.message);
           };                    
    }
}


function getxCacheVars(varxcacheId) {
    var fetchXML =  `<fetch mapping="logical" distinct="false" top="1">
       <entity name="north52_xcache">
         <attribute name="north52_name" />
         <attribute name="createdon" />
         <attribute name="north52_valuesecured" />
         <attribute name="north52_valuedatatype" />
         <attribute name="north52_category" />
         <attribute name="north52_xcacheid" />
         <order attribute="north52_category" descending="false" />
         <order attribute="north52_name" descending="false" />
        <filter type="and">
           <condition attribute="north52_category" operator="eq" value="${varxcacheId}" />
         </filter>
      </entity>
    </fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    return Xrm.WebApi.retrieveMultipleRecords("north52_xcache", fetchXML).then(result => {
        if(result == null) {
            throw Error("Invalid operation result when attempting to retrieve case products for case.");
        }
        return result.entities;
    }).catch(error => {
        throw Error(`Error retrieving case products for case with id ${varxcacheId}: ${error.message}`);
    });
}

function getxdocsUrl(varxcacheId) {
    var fetchXML = `<fetch distinct="false" mapping="logical" returntotalrecordcount="true" page="1" count="10" no-lock="false">
  <entity name="sharepointdocument">
    <attribute name="documentid"/>
    <attribute name="absoluteurl"/>
    <attribute name="fullname"/>
    <attribute name="relativelocation"/>
    <attribute name="sharepointdocumentid"/>
    <order attribute="relativelocation" descending="false"/>
    <attribute name="modified"/>
    <attribute name="sharepointmodifiedby"/>
    <attribute name="sharepointcreatedon"/>
    <link-entity name="leap_case" from="leap_caseid" to="regardingobjectid" alias="bb">
      <filter type="and">
        <condition attribute="leap_caseid" operator="eq" uitype="leap_case" value="${varxcacheId}" />
      </filter>
    </link-entity>
  </entity>
</fetch>`;
    fetchXML = "?fetchXml=" + encodeURIComponent(fetchXML);

    return Xrm.WebApi.retrieveMultipleRecords("sharepointdocument", fetchXML).then(result => {
        if(result == null) {
            throw Error("Invalid operation result when attempting to retrieve case products for case.");
        }
        return result.entities;
    }).catch(error => {
        throw Error(`Error retrieving case products for case with id ${varxcacheId}: ${error.message}`);
    });
}

function onlyemailform(executionContext) {
   Xrm.Page.ui.headerSection.setCommandBarVisible(false);
   Xrm.Page.ui.headerSection.setTabNavigatorVisible(false);
}

