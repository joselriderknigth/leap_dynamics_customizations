window.UsageForm = window.UsageForm || {};

UsageForm.Commands = UsageForm.Commands || {};

(function (ns) {

    "use strict";

    var POSTED_STATUS = 293360002;
    var PAID_STATUS = 293360003;

    console.log("leap_usageform_commands.js v1.0 loaded");

    /**
     * Enable/Disable Regenerate UFD button
     * Returns false when BC invoice status is Posted or Paid
     * @param {object} primaryControl Form context
     * @returns {boolean}
     */
    ns.enableRegenerateUFDButton = function (primaryControl) {

        try {

            var formContext = primaryControl;

            if (!formContext) {
                return true;
            }

            var statusAttribute = formContext.getAttribute("leap_bcsalesinvoicestatus");

            if (!statusAttribute) {
                return true;
            }

            var statusValue = statusAttribute.getValue();

            if (
                statusValue === POSTED_STATUS ||
                statusValue === PAID_STATUS
            ) {

                console.log("Regenerate UFD button disabled due to BC invoice status");

                return false;
            }

            return true;

        }
        catch (error) {

            console.error("Error in enableRegenerateUFDButton", error);

            // Fail open to avoid blocking users unintentionally
            return true;
        }
    };

})(UsageForm.Commands);