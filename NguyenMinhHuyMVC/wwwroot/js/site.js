(() => {
    "use strict";

    const successKey = "fu-news:focus-success";
    window.FUNews = {
        focusForm(root) {
            const error = root.querySelector(".input-validation-error")
                || root.querySelector(".validation-summary-errors");
            const target = error || (!window.matchMedia("(pointer: coarse)").matches
                ? root.querySelector("input:not([type='hidden']), select, textarea") : null);
            if (!target) return;
            if (target.classList.contains("validation-summary-errors")) target.tabIndex = -1;
            target.focus();
        },
        trackUnsavedForm(root, isSaving) {
            let initialValues = null;
            const snapshot = () => {
                const form = root.querySelector("form");
                return form ? JSON.stringify(Array.from(new FormData(form), ([name, value]) => [name, String(value)])) : null;
            };
            const changed = () => initialValues !== null && snapshot() !== initialValues;
            const guard = {
                markClean() { initialValues = snapshot(); },
                reset() { initialValues = null; },
                confirmClose() {
                    return !isSaving() && (!changed() || window.confirm("Discard unsaved changes?"));
                }
            };
            window.addEventListener("beforeunload", event => {
                if (!isSaving() && changed()) {
                    event.preventDefault();
                    event.returnValue = "";
                }
            });
            return guard;
        },
        reloadAfterSuccess() {
            try { sessionStorage.setItem(successKey, "1"); } catch { /* Storage may be unavailable. */ }
            window.location.reload();
        }
    };

    let focusedSuccess = false;
    try {
        if (sessionStorage.getItem(successKey) === "1") {
            sessionStorage.removeItem(successKey);
            const message = document.querySelector("#main-content > .alert-success[role='status']");
            if (message) {
                message.tabIndex = -1;
                message.focus();
                focusedSuccess = true;
            }
        }
    } catch { /* The success message remains visible if storage is unavailable. */ }

    if (!focusedSuccess) {
        const invalid = document.querySelector("#main-content form .input-validation-error, #main-content form .validation-summary-errors");
        if (invalid) window.FUNews.focusForm(invalid.closest("form"));
    }
})();
