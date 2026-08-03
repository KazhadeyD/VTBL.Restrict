/**
 * Upload DnD / file picker — только metadata File API, без чтения содержимого (EC-01).
 * Не запускает отправку формы автоматически (A-04).
 */
(function () {
    function initUploadDnD(options) {
        var input = document.getElementById(options.inputId || 'upload-file');
        var meta = document.getElementById(options.metaId || 'upload-file-meta');
        var zone = document.getElementById(options.zoneId || 'upload-dropzone');
        var browse = document.getElementById(options.browseId || 'upload-browse');
        var clearBtn = document.getElementById(options.clearId || 'upload-file-clear');

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
            if (!file) {
                return;
            }
            try {
                var dt = new DataTransfer();
                dt.items.add(file);
                input.files = dt.files;
            } catch (e) {
                // Fallback: some browsers disallow assigning FileList; leave existing picker selection.
            }
            showMeta();
        }

        function clearFile() {
            input.value = '';
            try {
                var dt = new DataTransfer();
                input.files = dt.files;
            } catch (e) {
                // Fallback: value reset above is enough for submit validation.
            }
            showMeta();
        }

        input.addEventListener('change', showMeta);

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

        zone.addEventListener('click', function (e) {
            if (e.target === browse || (browse && browse.contains(e.target))) {
                return;
            }
            // Click on zone opens picker only if not using dedicated browse button path —
            // keep zone as visual drop target; browse button handles click select.
        });

        showMeta();
    }

    window.VTBL = window.VTBL || {};
    window.VTBL.initUploadDnD = initUploadDnD;

    document.addEventListener('DOMContentLoaded', function () {
        if (document.getElementById('upload-dropzone')) {
            initUploadDnD({});
        }
    });
})();
