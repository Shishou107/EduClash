window.docxViewer = {
    currentZoom: 0.9,
    totalPages: 1,
    currentPage: 1,

    renderDocx: async function(url, containerId, docTitle) {
        var container = document.getElementById(containerId);
        if (!container) return;

        var fallback = document.getElementById(containerId + "-fallback");
        if (fallback) fallback.style.display = "none";

        container.style.display = "flex";
        container.style.flexDirection = "column";
        container.style.alignItems = "center";
        container.innerHTML = '<div class="text-center py-5 text-white-50"><div class="spinner-border text-light mb-3" role="status"></div><p class="fw-semibold">Đang chuẩn hóa phân trang A4 theo chuẩn Word...</p></div>';

        try {
            var response = await fetch(url, { mode: 'cors' });
            if (!response.ok) throw new Error("HTTP error " + response.status);
            var blob = await response.blob();

            container.innerHTML = '';

            if (typeof docx === 'undefined') {
                throw new Error("Thư viện docx-preview chưa sẵn sàng");
            }

            // 1. Render ban đầu bằng docx-preview
            await docx.renderAsync(blob, container, null, {
                className: "docx",
                inWrapper: true,
                ignoreWidth: false,
                ignoreHeight: false,
                ignoreFonts: false,
                breakPages: true,
                ignoreLastRenderedPageBreak: false,
                renderHeaders: false,
                renderFooters: false,
                useBase64URL: true,
                experimental: false
            });

            // 2. Phân trang chuẩn A4 từng trang rời độc lập theo quy chuẩn Word
            this.paginateIntoStandardWordPages(container, docTitle || "Tài liệu học tập");

            // 3. Áp dụng mức zoom chuẩn để trang vừa mắt nhất
            this.applyZoom();

            console.log("Rendered & Paginated DOCX pages to Word standard successfully!");
        } catch (err) {
            console.error("Docx render error, falling back to A4 paginated text view:", err);
            container.style.display = "none";
            if (fallback) {
                fallback.style.display = "flex";
            }
        }
    },

    paginateIntoStandardWordPages: function(container, docTitle) {
        var wrapper = container.querySelector('.docx-wrapper');
        if (!wrapper) return;

        var initialSections = Array.from(wrapper.querySelectorAll('section.docx, section'));
        if (initialSections.length === 0) return;

        // Chuẩn chiều cao nội dung 1 trang Word A4
        // Chiều cao tờ giấy A4: 1123px - 90px margins - 52px headers/footers = 981px max.
        // Cắt ở ngưỡng ~700px để mỗi trang đạt tỷ lệ chuẩn Word 18 - 22 dòng, cực kỳ cân đối và vừa mắt!
        var TARGET_PAGE_CONTENT_HEIGHT = 700;
        var allPages = [];

        var createPageElement = function() {
            var sec = document.createElement('section');
            sec.className = 'docx a4-paginated-sheet';
            var art = document.createElement('article');
            art.className = 'a4-article-content';
            sec.appendChild(art);
            return sec;
        };

        // Thu thập toàn bộ các phần tử nội dung con
        var allNodes = [];
        initialSections.forEach(function(sec) {
            var article = sec.querySelector('article');
            var source = article || sec;
            var children = Array.from(source.children);
            children.forEach(function(child) {
                if (!child.classList.contains('a4-sheet-header') && !child.classList.contains('a4-sheet-footer')) {
                    allNodes.push(child);
                }
            });
        });

        if (allNodes.length === 0) return;

        var currentPage = createPageElement();
        var currentArticle = currentPage.querySelector('article');
        allPages.push(currentPage);
        var currentHeight = 0;

        allNodes.forEach(function(node) {
            // Đo chiều cao thực tế chính xác của thẻ DOM
            var h = node.offsetHeight || node.getBoundingClientRect().height;
            if (!h || h <= 0) {
                var text = (node.textContent || '').trim();
                var charCount = text.length;
                var estLines = Math.max(1, Math.ceil(charCount / 75));
                h = estLines * 25 + 14;
            }

            // Kiểm tra xem thẻ có phải tiêu đề chương/mục không
            var isHeading = false;
            var tag = (node.tagName || '').toLowerCase();
            if (tag === 'h1' || tag === 'h2' || tag === 'h3' || tag === 'h4' || node.classList.contains('docx-heading')) {
                isHeading = true;
            } else {
                var firstText = (node.textContent || '').trim();
                if (/^(chương|phần|bài|\d+\.|\bI\.|\bII\.|\bIII\.|\bIV\.|\bV\.)/i.test(firstText)) {
                    isHeading = true;
                }
            }

            // Quy tắc ngắt trang Microsoft Word:
            // 1. Tiêu đề xuất hiện khi trang đã đầy quá nửa (> 450px) -> ngắt sang trang mới
            // 2. Thêm phần tử này vượt quá chiều cao chuẩn của 1 trang Word (700px) -> ngắt trang mới
            if (isHeading && currentHeight >= 450 && currentArticle.children.length > 0) {
                currentPage = createPageElement();
                currentArticle = currentPage.querySelector('article');
                allPages.push(currentPage);
                currentHeight = 0;
            } else if (currentHeight + h > TARGET_PAGE_CONTENT_HEIGHT && currentArticle.children.length > 0) {
                currentPage = createPageElement();
                currentArticle = currentPage.querySelector('article');
                allPages.push(currentPage);
                currentHeight = 0;
            }

            currentArticle.appendChild(node);
            currentHeight += h;
        });

        // Xóa wrapper cũ và đưa các trang rời chuẩn Word A4 vào
        wrapper.innerHTML = '';
        var total = allPages.length;
        this.totalPages = total;

        allPages.forEach(function(pageSec, idx) {
            var pageNum = idx + 1;
            pageSec.id = 'docx-page-' + pageNum;

            // Header mép trên chuẩn Word
            var headerEl = document.createElement('div');
            headerEl.className = 'a4-sheet-header';
            headerEl.innerHTML = '<span class="a4-sheet-title-text text-truncate" style="max-width: 480px;">' + (docTitle || 'Tài liệu đề cương') + '</span><span class="a4-sheet-page-mini">Trang ' + pageNum + ' / ' + total + '</span>';
            pageSec.insertBefore(headerEl, pageSec.firstChild);

            // Footer mép dưới chuẩn Word với số trang
            var footerEl = document.createElement('div');
            footerEl.className = 'a4-sheet-footer';
            footerEl.innerHTML = '<span class="text-muted small">Hệ thống Đề cương EduClash</span><div class="a4-sheet-page-indicator fw-bold">— Trang ' + pageNum + ' / ' + total + ' —</div><span class="text-muted small">Khổ A4</span>';
            pageSec.appendChild(footerEl);

            wrapper.appendChild(pageSec);
        });

        this.updateToolbarDisplay(1, total);
    },

    updateToolbarDisplay: function(current, total) {
        this.currentPage = current;
        this.totalPages = total;

        var pageIndicator = document.getElementById('toolbar-page-indicator');
        if (pageIndicator) {
            pageIndicator.textContent = 'Trang ' + current + ' / ' + total;
        }

        var totalBadge = document.getElementById('toolbar-total-pages-badge');
        if (totalBadge) {
            totalBadge.textContent = '📄 ' + total + ' Trang (Chuẩn A4)';
        }

        var btnPrev = document.getElementById('toolbar-btn-prev-page');
        if (btnPrev) btnPrev.disabled = (current <= 1);

        var btnNext = document.getElementById('toolbar-btn-next-page');
        if (btnNext) btnNext.disabled = (current >= total);
    },

    goToPage: function(pageNum) {
        if (pageNum < 1) pageNum = 1;
        if (pageNum > this.totalPages) pageNum = this.totalPages;

        var targetEl = document.getElementById('docx-page-' + pageNum);
        if (targetEl) {
            targetEl.scrollIntoView({ behavior: 'smooth', block: 'start' });
            this.updateToolbarDisplay(pageNum, this.totalPages);
        }
    },

    nextPage: function() {
        if (this.currentPage < this.totalPages) {
            this.goToPage(this.currentPage + 1);
        }
    },

    prevPage: function() {
        if (this.currentPage > 1) {
            this.goToPage(this.currentPage - 1);
        }
    },

    setZoom: function(delta) {
        if (delta === 0) {
            this.currentZoom = 0.9;
        } else {
            this.currentZoom = Math.min(1.4, Math.max(0.65, Math.round((this.currentZoom + delta) * 10) / 10));
        }
        this.applyZoom();
    },

    applyZoom: function() {
        var wrapper = document.querySelector('.docx-wrapper');
        var fallbackContainer = document.getElementById('docx-page-container-fallback');
        var zoomPercent = Math.round(this.currentZoom * 100);

        if (wrapper) {
            wrapper.style.transform = 'scale(' + this.currentZoom + ')';
            wrapper.style.transformOrigin = 'top center';
        }

        if (fallbackContainer) {
            fallbackContainer.style.transform = 'scale(' + this.currentZoom + ')';
            fallbackContainer.style.transformOrigin = 'top center';
        }

        var zoomDisplay = document.getElementById('toolbar-zoom-display');
        if (zoomDisplay) {
            zoomDisplay.textContent = zoomPercent + '%';
        }
    }
};
