var fetchIntervalId;

function checkPendingUsagePeriodically(executionContext) {
    // Get the form context..134
    var formContext =executionContext.getFormContext ? executionContext.getFormContext() : Xrm.Page;

     var statusValue = formContext.getAttribute("statuscode").getValue();
     var pendingUsage = false;
     var count =0;
    // Retrieve the case id from the form field "leap_caseid"
   var caseId =  Xrm.Page.data.entity.getId().toString();
    if (!caseId) {
        console.error("Case ID not found on the form.");
        return;
    }
    // Clean the GUID if necessary (remove braces)
    caseId = caseId.replace("{", "").replace("}", "");
    debugger;
    
    if (statusValue === 293360000 || statusValue === 293360007 ||  statusValue === 1 ) {
       // Function that builds and executes the FetchXML query
       function fetchPendingUsage() {
          // Build the FetchXML query; adjust the entity name and attribute names as needed.
           var fetchXml = 
              "<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>" +
                "<entity name='leap_case'>" +
                    "<attribute name='leap_hasproductpendingusageform' />" +
                    "<filter type='and'>" +
                        "<condition attribute='leap_caseid' operator='eq' value='" + caseId + "' />" +
                    "</filter>" +
                "</entity>" +
             "</fetch>";
        
           // Execute the FetchXML query using the Web API
           Xrm.WebApi.retrieveMultipleRecords("leap_case", "?fetchXml=" + encodeURIComponent(fetchXml))
               .then(function success(result) {
                   if (result.entities.length > 0) {
                     // Retrieve the value from the first (and likely only) record returned
                        pendingUsage = result.entities[0]["leap_hasproductpendingusageform"];
                        if (pendingUsage && count<=2) {
                            var webResourceControl = formContext.getControl("WebResource_north52_quick_button_close_case");
                            if (webResourceControl) {
                                debugger;
                                      var currentSrc = webResourceControl.getSrc();
                                     // Set the source again to force a reload.
                                     webResourceControl.setSrc(currentSrc);

                                      webResourceControl.setVisible(false);
        
                                      // After a short delay, make it visible again
                                      setTimeout(function() {
                                         webResourceControl.setVisible(true);
                                         console.log("Web resource refreshed (toggled visibility).");
                                       }, 12000); // Adjust delay (in milliseconds) as needed
                                     count=count+1;
                                     console.log("Web resource refreshed.");
                            } else {
                                  console.error("Web resource control not found.");
                            }

                       }
                      console.log("Current leap_hasproductpendingusageform: " + pendingUsage);
                     debugger;
                     if (count > 2 ) {
                         clearInterval(fetchIntervalId);
                         console.log("Condition met, background fetch stopped.");
                        return;
                     }
                    // You might also add logic here to trigger additional actions if the value changes
                   } else {
                       console.warn("No records found for case id: " + caseId);
                   }
            },
            function(error) {
                console.error("Error retrieving pending usage: " + error.message);
            });
       }
       
        debugger;
         var myInterval;
       if (!pendingUsage){
          // Immediately check once on form load
          fetchPendingUsage(formContext);
          // Then, set an interval to check every 2 minutes (120,000 milliseconds)
          fetchIntervalId = setInterval(fetchPendingUsage, 60000);
       }
    }
}