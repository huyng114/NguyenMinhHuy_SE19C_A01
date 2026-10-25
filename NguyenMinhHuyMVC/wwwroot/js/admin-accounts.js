(() => {
    "use strict";

    const modalElement = document.getElementById("accountModal");
    const modal = new bootstrap.Modal(modalElement);
    const content = document.getElementById("accountModalContent");
    let opener;
    let request;
    let saving = false;
    let deleting = false;
    const editGuard = window.FUNews.trackUnsavedForm(content, () => saving);

    function bindValidation() {
        $.validator.unobtrusive.parse(content);
        window.FUNews.focusForm(content);
    }

    function handleRedirect(response) {
        if (response.redirected) {
            window.location.assign(response.url);
            return true;
        }
        return false;
    }

    function showError(element, message) {
        element.textContent = message;
        element.classList.remove("d-none");
    }

    document.querySelectorAll("[data-account-form]").forEach(button => {
        button.addEventListener("click", async () => {
            opener = button;
            editGuard.reset();
            request?.abort();
            const currentRequest = new AbortController();
            request = currentRequest;
            content.innerHTML = '<div class="modal-header"><h2 class="modal-title h5" id="accountModalLabel">Account</h2><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div><div class="modal-body" role="status">Loading...</div>';
            modal.show();
            try {
                const response = await fetch(button.dataset.accountForm, { signal: currentRequest.signal });
                if (handleRedirect(response)) return;
                if (!response.ok) throw new Error("Unable to load this account. Close this dialog and refresh the list.");
                const html = await response.text();
                if (currentRequest.signal.aborted) return;
                content.innerHTML = html;
                bindValidation();
                editGuard.markClean();
            } catch (error) {
                if (error.name !== "AbortError") showError(content.querySelector(".modal-body"), error.message);
            }
        });
    });
    modalElement.addEventListener("shown.bs.modal", () => window.FUNews.focusForm(content));
    modalElement.addEventListener("hide.bs.modal", event => {
        if (!editGuard.confirmClose()) event.preventDefault();
        else request?.abort();
    });
    modalElement.addEventListener("hidden.bs.modal", () => { editGuard.reset(); opener?.focus(); });

    content.addEventListener("submit", async event => {
        const form = event.target;
        if (form.id !== "accountForm") return;
        event.preventDefault();
        if (saving) return;
        if (!$(form).valid()) {
            window.FUNews.focusForm(form);
            return;
        }
        saving = true;
        const submit = form.querySelector("button[type='submit']");
        const originalLabel = submit.textContent;
        submit.disabled = true;
        submit.textContent = "Saving...";
        form.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(form.action, { method: "POST", body: new FormData(form) });
            if (handleRedirect(response)) return;
            if (response.status === 422) {
                content.innerHTML = await response.text();
                bindValidation();
                return;
            }
            if (!response.ok) throw new Error("Unable to save the account. Please refresh the page and try again.");
            const result = await response.json();
            if (result.success) {
                editGuard.reset();
                window.FUNews.reloadAfterSuccess();
            }
        } catch (error) {
            showError(document.getElementById("accountFormError"), error.message);
        } finally {
            saving = false;
            submit.disabled = false;
            submit.textContent = originalLabel;
            form.removeAttribute("aria-busy");
        }
    });

    const deleteModal = document.getElementById("deleteAccountModal");
    const deleteForm = document.getElementById("deleteAccountForm");
    const deleteError = document.getElementById("deleteAccountError");
    deleteModal.addEventListener("show.bs.modal", event => {
        document.getElementById("deleteAccountId").value = event.relatedTarget.dataset.accountId;
        document.getElementById("deleteAccountName").textContent = event.relatedTarget.dataset.accountName;
        deleteError.classList.add("d-none");
    });
    deleteModal.addEventListener("hide.bs.modal", event => {
        if (deleting) event.preventDefault();
    });
    deleteForm.addEventListener("submit", async event => {
        event.preventDefault();
        if (deleting) return;
        deleting = true;
        const submit = deleteForm.querySelector("button[type='submit']");
        const originalLabel = submit.textContent;
        submit.disabled = true;
        submit.textContent = "Deleting...";
        deleteForm.setAttribute("aria-busy", "true");
        try {
            const response = await fetch(deleteForm.action, { method: "POST", body: new FormData(deleteForm) });
            if (handleRedirect(response)) return;
            if (!(response.headers.get("content-type") || "").includes("application/json"))
                throw new Error("Unable to delete the account. Please refresh the page and try again.");
            const result = await response.json();
            if (!response.ok) throw new Error(result.message || "Unable to delete the account.");
            if (result.success) {
                window.FUNews.reloadAfterSuccess();
            }
        } catch (error) {
            showError(deleteError, error.message);
        } finally {
            deleting = false;
            submit.disabled = false;
            submit.textContent = originalLabel;
            deleteForm.removeAttribute("aria-busy");
        }
    });
})();
