/**
 * Studocu-style Document Viewer for EduClash
 * Supports high-fidelity rendering for both Adobe PDF (.pdf) and Microsoft Word (.docx)
 * Designed to look and feel exactly like Studocu & Word Online.
 */
(function (window, document) {
    'use strict';

    var StudocuViewer = function () {
        this.container = null;
        this.docUrl = null;
        this.proxyUrl = null;
        this.fileType = 'pdf'; // 'pdf' | 'docx'
        this.pdfDoc = null;
        this.currentPage = 1;
        this.totalPages = 1;
        this.scale = 1.25;
        this.isRendering = false;
        this.observer = null;
    };

    StudocuViewer.prototype.init = function (options) {
        var self = this;
        this.container = document.getElementById(options.containerId);
        if (!this.container) return;

        this.docUrl = options.docUrl;
        this.proxyUrl = options.proxyUrl;
        this.fileType = (options.fileType || 'pdf').toLowerCase();

        this.setupToolbarEvents();

        if (this.fileType === 'pdf') {
            this.loadPdf();
        } else if (this.fileType === 'docx') {
            this.loadDocx();
        }
    };

    StudocuViewer.prototype.setupToolbarEvents = function () {
        var self = this;

        var btnPrev = document.getElementById('studocu-btn-prev');
        if (btnPrev) {
            btnPrev.onclick = function () { self.prevPage(); };
        }

        var btnNext = document.getElementById('studocu-btn-next');
        if (btnNext) {
            btnNext.onclick = function () { self.nextPage(); };
        }

        var btnZoomIn = document.getElementById('studocu-btn-zoom-in');
        if (btnZoomIn) {
            btnZoomIn.onclick = function () { self.zoom(0.15); };
        }

        var btnZoomOut = document.getElementById('studocu-btn-zoom-out');
        if (btnZoomOut) {
            btnZoomOut.onclick = function () { self.zoom(-0.15); };
        }

        var btnZoomReset = document.getElementById('studocu-btn-zoom-reset');
        if (btnZoomReset) {
            btnZoomReset.onclick = function () { self.setZoom(1.0); };
        }

        var btnFitWidth = document.getElementById('studocu-btn-fit-width');
        if (btnFitWidth) {
            btnFitWidth.onclick = function () { self.fitWidth(); };
        }

        var btnFullscreen = document.getElementById('studocu-btn-fullscreen');
        if (btnFullscreen) {
            btnFullscreen.onclick = function () { self.toggleFullscreen(); };
        }

        var pageInput = document.getElementById('studocu-page-input');
        if (pageInput) {
            pageInput.onchange = function () {
                var val = parseInt(pageInput.value, 10);
                if (!isNaN(val)) self.goToPage(val);
            };
        }
    };

    // ==========================================
    // 1. PDF RENDERING VIA MOZILLA PDF.JS
    // ==========================================
    StudocuViewer.prototype.loadPdf = function () {
        var self = this;
        this.showLoading("Đang tải trang tài liệu PDF...");

        if (typeof pdfjsLib === 'undefined') {
            this.showFallback("Thư viện xem PDF chưa được nạp. Bạn có thể mở trực tiếp tập tin bằng nút bên dưới.");
            return;
        }

        // Configure PDF.js worker
        try {
            pdfjsLib.GlobalWorkerOptions.workerSrc = window.location.origin + '/js/pdf.worker.min.js';
        } catch (e) {
            pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
        }

        var targetUrl = this.docUrl;

        var loadWithUrl = function (urlToLoad, isFallback) {
            var loadingTask = pdfjsLib.getDocument({
                url: urlToLoad,
                cMapUrl: 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/cmaps/',
                cMapPacked: true
            });

            loadingTask.promise.then(function (pdf) {
                self.pdfDoc = pdf;
                self.totalPages = pdf.numPages;
                self.updateToolbar();
                self.renderAllPdfPages();
            }).catch(function (err) {
                console.warn("Failed loading PDF with URL:", urlToLoad, err);
                if (!isFallback && self.proxyUrl) {
                    console.log("Retrying with proxy URL:", self.proxyUrl);
                    loadWithUrl(self.proxyUrl, true);
                } else {
                    self.showFallback("Không thể nạp trực tiếp PDF vào trình xem. Vui lòng tải xuống hoặc mở file gốc.");
                }
            });
        };

        loadWithUrl(targetUrl, false);
    };

    StudocuViewer.prototype.renderAllPdfPages = function () {
        var self = this;
        if (!this.pdfDoc) return;

        this.container.innerHTML = '';
        var dpr = window.devicePixelRatio || 1;

        // Container wrapper for centered sheets
        var sheetsWrapper = document.createElement('div');
        sheetsWrapper.className = 'studocu-sheets-wrapper';
        sheetsWrapper.id = 'studocu-sheets-wrapper';
        this.container.appendChild(sheetsWrapper);

        var renderPagePromises = [];

        var renderNext = function (pageNum) {
            if (pageNum > self.totalPages) {
                self.setupScrollTracking();
                return;
            }

            self.pdfDoc.getPage(pageNum).then(function (page) {
                var viewport = page.getViewport({ scale: self.scale });

                var sheetDiv = document.createElement('div');
                sheetDiv.className = 'studocu-page-sheet';
                sheetDiv.id = 'studocu-page-' + pageNum;
                sheetDiv.setAttribute('data-page-number', pageNum);
                sheetDiv.style.width = Math.round(viewport.width) + 'px';
                sheetDiv.style.height = Math.round(viewport.height) + 'px';

                var canvas = document.createElement('canvas');
                canvas.width = Math.round(viewport.width * dpr);
                canvas.height = Math.round(viewport.height * dpr);
                canvas.style.width = Math.round(viewport.width) + 'px';
                canvas.style.height = Math.round(viewport.height) + 'px';

                var ctx = canvas.getContext('2d');
                ctx.scale(dpr, dpr);

                sheetDiv.appendChild(canvas);

                // Studocu bottom page indicator badge
                var pageBadge = document.createElement('div');
                pageBadge.className = 'studocu-page-badge';
                pageBadge.textContent = 'Trang ' + pageNum + ' / ' + self.totalPages;
                sheetDiv.appendChild(pageBadge);

                sheetsWrapper.appendChild(sheetDiv);

                var renderContext = {
                    canvasContext: ctx,
                    viewport: viewport
                };

                page.render(renderContext).promise.then(function () {
                    renderNext(pageNum + 1);
                });
            });
        };

        renderNext(1);
    };

    // ==========================================
    // 2. WORD DOCX RENDERING VIA DOCX-PREVIEW
    // ==========================================
    StudocuViewer.prototype.loadDocx = function () {
        var self = this;
        this.showLoading("Đang nạp và định dạng trang Microsoft Word...");

        if (typeof docx === 'undefined') {
            this.showFallback("Thư viện xem Word chưa sẵn sàng. Bạn có thể tải tập tin về xem.");
            return;
        }

        var targetUrl = this.docUrl;

        var fetchAndRender = function (urlToLoad, isFallback) {
            fetch(urlToLoad)
                .then(function (res) {
                    if (!res.ok) throw new Error("HTTP error " + res.status);
                    return res.blob();
                })
                .then(function (blob) {
                    self.container.innerHTML = '';
                    
                    var sheetsWrapper = document.createElement('div');
                    sheetsWrapper.className = 'studocu-sheets-wrapper';
                    sheetsWrapper.id = 'studocu-sheets-wrapper';
                    self.container.appendChild(sheetsWrapper);

                    return docx.renderAsync(blob, sheetsWrapper, null, {
                        className: "docx",
                        inWrapper: true,
                        ignoreWidth: false,
                        ignoreHeight: false,
                        ignoreFonts: false,
                        breakPages: true,
                        ignoreLastRenderedPageBreak: false,
                        renderHeaders: true,
                        renderFooters: true,
                        useBase64URL: true,
                        renderChanges: false,
                        renderComments: false
                    });
                })
                .then(function () {
                    // Count sections/pages generated by docx-preview
                    var sections = self.container.querySelectorAll('.docx-wrapper > section.docx, .docx-wrapper > section');
                    self.totalPages = Math.max(1, sections.length);

                    sections.forEach(function (sec, idx) {
                        var pNum = idx + 1;
                        sec.id = 'studocu-page-' + pNum;
                        sec.setAttribute('data-page-number', pNum);

                        // Attach Studocu page badge
                        var pageBadge = document.createElement('div');
                        pageBadge.className = 'studocu-page-badge';
                        pageBadge.textContent = 'Trang ' + pNum + ' / ' + self.totalPages;
                        sec.appendChild(pageBadge);
                    });

                    self.updateToolbar();
                    self.setupScrollTracking();
                    self.applyDocxZoom();
                })
                .catch(function (err) {
                    console.warn("Failed loading DOCX with URL:", urlToLoad, err);
                    if (!isFallback && self.proxyUrl) {
                        console.log("Retrying with proxy URL:", self.proxyUrl);
                        fetchAndRender(self.proxyUrl, true);
                    } else {
                        self.showFallback("Không thể mở tập tin Word trong trình xem. Vui lòng tải về máy để xem.");
                    }
                });
        };

        fetchAndRender(targetUrl, false);
    };

    StudocuViewer.prototype.applyDocxZoom = function () {
        var wrapper = this.container.querySelector('.docx-wrapper');
        if (wrapper) {
            wrapper.style.transform = 'scale(' + this.scale + ')';
            wrapper.style.transformOrigin = 'top center';
        }
        var zoomText = document.getElementById('studocu-zoom-text');
        if (zoomText) {
            zoomText.textContent = Math.round(this.scale * 100) + '%';
        }
    };

    // ==========================================
    // 3. ZOOM & NAVIGATION CONTROLS
    // ==========================================
    StudocuViewer.prototype.zoom = function (delta) {
        var newScale = Math.min(2.0, Math.max(0.6, Math.round((this.scale + delta) * 100) / 100));
        this.setZoom(newScale);
    };

    StudocuViewer.prototype.setZoom = function (scale) {
        this.scale = scale;
        this.updateToolbar();

        if (this.fileType === 'pdf') {
            this.renderAllPdfPages();
        } else if (this.fileType === 'docx') {
            this.applyDocxZoom();
        }
    };

    StudocuViewer.prototype.fitWidth = function () {
        var containerWidth = this.container.clientWidth || 900;
        // Chuẩn chiều ngang A4 ~ 794px hoặc 820px, để lề 48px
        var availableWidth = Math.max(500, containerWidth - 64);
        var calculatedScale = Math.round((availableWidth / 820) * 100) / 100;
        this.setZoom(Math.min(1.6, Math.max(0.7, calculatedScale)));
    };

    StudocuViewer.prototype.goToPage = function (pageNum) {
        if (pageNum < 1) pageNum = 1;
        if (pageNum > this.totalPages) pageNum = this.totalPages;
        this.currentPage = pageNum;
        this.updateToolbar();

        var pageEl = document.getElementById('studocu-page-' + pageNum);
        if (pageEl) {
            pageEl.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    };

    StudocuViewer.prototype.prevPage = function () {
        if (this.currentPage > 1) {
            this.goToPage(this.currentPage - 1);
        }
    };

    StudocuViewer.prototype.nextPage = function () {
        if (this.currentPage < this.totalPages) {
            this.goToPage(this.currentPage + 1);
        }
    };

    StudocuViewer.prototype.toggleFullscreen = function () {
        var card = document.getElementById('studocu-viewer-card');
        if (!card) card = this.container;

        if (!document.fullscreenElement) {
            if (card.requestFullscreen) {
                card.requestFullscreen();
            } else if (card.webkitRequestFullscreen) {
                card.webkitRequestFullscreen();
            }
        } else {
            if (document.exitFullscreen) {
                document.exitFullscreen();
            }
        }
    };

    StudocuViewer.prototype.setupScrollTracking = function () {
        var self = this;
        var sheets = this.container.querySelectorAll('.studocu-page-sheet, .docx-wrapper > section.docx, .docx-wrapper > section');
        if (sheets.length === 0) return;

        if ('IntersectionObserver' in window) {
            if (this.observer) this.observer.disconnect();

            this.observer = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        var pNum = parseInt(entry.target.getAttribute('data-page-number'), 10);
                        if (!isNaN(pNum) && pNum !== self.currentPage) {
                            self.currentPage = pNum;
                            self.updateToolbar();
                        }
                    }
                });
            }, {
                root: null,
                threshold: 0.35
            });

            sheets.forEach(function (sheet) {
                self.observer.observe(sheet);
            });
        }
    };

    StudocuViewer.prototype.updateToolbar = function () {
        var pageInput = document.getElementById('studocu-page-input');
        if (pageInput) {
            pageInput.value = this.currentPage;
            pageInput.max = this.totalPages;
        }

        var totalPagesEl = document.getElementById('studocu-total-pages');
        if (totalPagesEl) {
            totalPagesEl.textContent = this.totalPages;
        }

        var zoomText = document.getElementById('studocu-zoom-text');
        if (zoomText) {
            zoomText.textContent = Math.round(this.scale * 100) + '%';
        }

        var btnPrev = document.getElementById('studocu-btn-prev');
        if (btnPrev) btnPrev.disabled = (this.currentPage <= 1);

        var btnNext = document.getElementById('studocu-btn-next');
        if (btnNext) btnNext.disabled = (this.currentPage >= this.totalPages);
    };

    StudocuViewer.prototype.showLoading = function (msg) {
        this.container.innerHTML = 
            '<div class="studocu-loading-box">' +
                '<div class="spinner-border text-primary mb-3" style="width: 3rem; height: 3rem;" role="status"></div>' +
                '<h5 class="fw-bold text-dark mb-1">' + (msg || "Đang tải trang tài liệu...") + '</h5>' +
                '<p class="text-muted small">Đang kết xuất giao diện theo chuẩn Studocu / Word Online...</p>' +
            '</div>';
    };

    StudocuViewer.prototype.showFallback = function (msg) {
        var fallbackHtml = 
            '<div class="studocu-fallback-box text-center p-5">' +
                '<div class="fs-1 mb-2">📄</div>' +
                '<h5 class="fw-bold text-dark mb-2">Tài Liệu Gốc Đã Sẵn Sàng</h5>' +
                '<p class="text-muted mb-4" style="max-width: 500px; margin: 0 auto;">' + msg + '</p>' +
                '<div class="d-flex justify-content-center gap-3">' +
                    '<a href="' + this.docUrl + '" target="_blank" download class="btn btn-primary fw-bold px-4 py-2">' +
                        '📥 Tải Về Xem Ngay' +
                    '</a>' +
                    '<a href="' + this.docUrl + '" target="_blank" class="btn btn-outline-secondary fw-semibold px-4 py-2">' +
                        '↗️ Mở Trong Tab Mới' +
                    '</a>' +
                '</div>' +
            '</div>';

        if (this.fileType === 'pdf') {
            fallbackHtml += 
                '<div class="mt-4 px-3" style="width: 100%; height: 850px;">' +
                    '<iframe src="' + this.docUrl + '#toolbar=1" style="width: 100%; height: 100%; border: none; border-radius: 8px; box-shadow: 0 4px 16px rgba(0,0,0,0.15);" title="PDF Viewer"></iframe>' +
                '</div>';
        }

        this.container.innerHTML = fallbackHtml;
    };

    window.StudocuViewer = StudocuViewer;
    window.studocuViewer = new StudocuViewer();

})(window, document);
