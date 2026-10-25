(() => {
    "use strict";
    const modalElement = document.getElementById("categoryModal");
    const modal = new bootstrap.Modal(modalElement);
    const content = document.getElementById("categoryModalContent");
    let opener;
    let request;
    let saving = false;
    const editGuard = window.FUNews.trackUnsavedForm(content, () => saving);

    function focusField() {
        window.FUNews.focusForm(content);
    }
    function bindValidation() {
        $.validator.unobtrusive.parse(content);
        focusField();
    }
    function redirect(response) {
        if (!response.redirected) return false;
        window.location.assign(response.url);
        return true;
    }
    function errorMessage(element, message) {
        element.textContent = message;
        element.classList.remove("d-none");
    }

    document.querySelectorAll("[data-category-form]").forEach(button => {
        button.addEventListener("click", async () => {
            opener = button;
            editGuard.reset();
            request?.abort();
            const currentRequest = new AbortController();
            request = currentRequest;
            content.innerHTML = '<div class="modal-header"><h2 class="modal-title h5" id="categoryModalLabel">Category</h2><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div><div class="modal-body" role="status">Loading...</div>';
            modal.show();
            try {
                const response = await fetch(button.dataset.categoryForm, { signal: currentRequest.signal });
                if (redirect(response)) return;
                if (!response.ok) throw new Error("Unable to load this category. Close the dialog and refresh the list.");
                const html = await response.text();
                if (currentRequest.signal.aborted) return;
                content.innerHTML = html;
                bindValidation();
                editGuard.markClean();
            } catch (error) {
                if (error.name !== "AbortError") errorMessage(content.querySelector(".modal-body"), error.message);
            }
        });
    });
    modalElement.addEventListener("shown.bs.modal", focusField);
    modalElement.addEventListener("hide.bs.modal", event => {
        if (!editGuard.confirmClose()) event.preventDefault();
        else request?.abort();
    });
    modalElement.addEventListener("hidden.bs.modal", () => { editGuard.reset(); opener?.focus(); });
    content.addEventListener("submit", async event => {
        const form = event.target;
        if (form.id !== "categoryForm") return;
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
            if (redirect(response)) return;
            if (response.status === 422) {
                content.innerHTML = await response.text();
                bindValidation();
                return;
            }
            if (!response.ok) throw new Error("Unable to save the category. Please refresh the page and try again.");
            if ((await response.json()).success) {
                editGuard.reset();
                window.FUNews.reloadAfterSuccess();
            }
        } catch (error) {
            errorMessage(document.getElementById("categoryFormError"), error.message);
        } finally {
            saving = false;
            submit.disabled = false;
            submit.textContent = originalLabel;
            form.removeAttribute("aria-busy");
        }
    });

    const deleteModal = document.getElementById("deleteCategoryModal");
    const deleteForm = document.getElementById("deleteCategoryForm");
    const deleteError = document.getElementById("deleteCategoryError");
    let deleting = false;
    deleteModal.addEventListener("show.bs.modal", event => {
        document.getElementById("deleteCategoryId").value = event.relatedTarget.dataset.categoryId;
        document.getElementById("deleteCategoryName").textContent = event.relatedTarget.dataset.categoryName;
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
            if (redirect(response)) return;
            if (!(response.headers.get("content-type") || "").includes("application/json"))
                throw new Error("Unable to delete the category. Please refresh the page and try again.");
            const result = await response.json();
            if (!response.ok) throw new Error(result.message || "Unable to delete the category.");
            if (result.success) {
                window.FUNews.reloadAfterSuccess();
            }
        } catch (error) {
            errorMessage(deleteError, error.message);
        } finally {
            deleting = false;
            submit.disabled = false;
            submit.textContent = originalLabel;
            deleteForm.removeAttribute("aria-busy");
        }
    });
})();
