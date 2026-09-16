/**
 * Drag-and-drop и выбор файла: только имя и размер, без чтения содержимого.
 * Отправка формы только по явному нажатию кнопки.
 * IE 11: без DataTransfer — только выбор через кнопку / input file.
 */
(function () {
    function supportsDataTransfer() {
        return typeof DataTransfer !== 'undefined';
    }

    function clearPreviousUploadResult() {
        var results = document.querySelectorAll('[data-upload-result]');
        for (var i = 0; i < results.length; i++) {
            var node = results[i];
            if (node.parentNode) {
                node.parentNode.removeChild(node);
            }
        }

        var summary = document.querySelector('[data-validation-summary]');
        if (summary) {
            summary.innerHTML = '';
            summary.className = summary.className.replace(/\bvalidation-summary-errors\b/g, 'validation-summary-valid');
            if (summary.className.indexOf('validation-summary-valid') === -1) {
                summary.className = (summary.className + ' validation-summary-valid').replace(/^\s+/, '');
            }
        }
    }

    function setUploadWaitVisible(visible) {
        var wait = document.getElementById('upload-wait');
        if (!wait) {
            return;
        }
        if (visible) {
            wait.hidden = false;
            wait.classList.add('is-visible');
            wait.setAttribute('aria-busy', 'true');
        } else {
            wait.classList.remove('is-visible');
            wait.hidden = true;
            wait.setAttribute('aria-busy', 'false');
        }
    }

    function isUploadFormClientValid(form) {
        if (window.jQuery) {
            var $form = window.jQuery(form);
            if ($form.length && typeof $form.valid === 'function') {
                return $form.valid();
            }
        }
        if (typeof form.checkValidity === 'function') {
            return form.checkValidity();
        }
        return true;
    }

    function initUploadDnD(options) {
        var input = document.getElementById(options.inputId || 'upload-file');
        var meta = document.getElementById(options.metaId || 'upload-file-meta');
        var zone = document.getElementById(options.zoneId || 'upload-dropzone');
        var browse = document.getElementById(options.browseId || 'upload-browse');
        var clearBtn = document.getElementById(options.clearId || 'upload-file-clear');
        var form = document.getElementById(options.formId || 'upload-form');
        var listType = document.getElementById(options.listTypeId || 'Input_ListTypeCode');
        var submitBtn = form
            ? form.querySelector('[data-upload-submit="true"]')
            : document.querySelector('[data-upload-submit="true"]');
        var canAssignFiles = supportsDataTransfer();

        if (!input || !meta || !zone) {
            return;
        }

        function hasFile() {
            return !!(input.files && input.files[0]);
        }

        function showMeta() {
            if (!hasFile()) {
                meta.textContent = '';
                updateClearButton();
                return;
            }
            var f = input.files[0];
            meta.textContent = f.name + ' (' + f.size + ' bytes)';
            updateClearButton();
        }

        function updateClearButton() {
            if (!clearBtn) {
                return;
            }
            clearBtn.hidden = !hasFile();
        }

        function assignFile(file) {
            if (!file || !canAssignFiles) {
                return;
            }
            try {
                var dt = new DataTransfer();
                dt.items.add(file);
                input.files = dt.files;
            } catch (e) {
                // Fallback: some browsers disallow assigning FileList; leave existing picker selection.
            }
            clearPreviousUploadResult();
            showMeta();
        }

        function clearFile() {
            input.value = '';
            if (canAssignFiles) {
                try {
                    var dt = new DataTransfer();
                    input.files = dt.files;
                } catch (e) {
                    // Fallback: value reset above is enough for submit validation.
                }
            }
            clearPreviousUploadResult();
            showMeta();
        }

        input.addEventListener('change', function () {
            clearPreviousUploadResult();
            showMeta();
        });

        if (listType) {
            listType.addEventListener('change', clearPreviousUploadResult);
        }

        if (form) {
            form.addEventListener('submit', function () {
                if (!isUploadFormClientValid(form)) {
                    return;
                }
                clearPreviousUploadResult();
                setUploadWaitVisible(true);
                // disabled в том же тике submit в части браузеров отменяет POST
                window.setTimeout(function () {
                    if (submitBtn) {
                        submitBtn.disabled = true;
                    }
                }, 0);
            });
        }

        if (clearBtn) {
            clearBtn.addEventListener('click', function (e) {
                e.preventDefault();
                clearFile();
            });
        }

        if (browse) {
            browse.addEventListener('click', function (e) {
                e.preventDefault();
                input.click();
            });
        }

        if (canAssignFiles) {
            zone.addEventListener('dragover', function (e) {
                e.preventDefault();
                zone.classList.add('border-primary');
            });

            zone.addEventListener('dragleave', function () {
                zone.classList.remove('border-primary');
            });

            zone.addEventListener('drop', function (e) {
                e.preventDefault();
                zone.classList.remove('border-primary');
                if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files[0]) {
                    assignFile(e.dataTransfer.files[0]);
                }
            });
        }

        showMeta();
    }

    window.VTBL = window.VTBL || {};
    window.VTBL.initUploadDnD = initUploadDnD;
    window.VTBL.clearPreviousUploadResult = clearPreviousUploadResult;
    window.VTBL.setUploadWaitVisible = setUploadWaitVisible;

    document.addEventListener('DOMContentLoaded', function () {
        if (document.getElementById('upload-dropzone')) {
            initUploadDnD({});
        }
    });
})();
