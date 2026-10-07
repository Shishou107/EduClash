// drag-scroll.js — Kéo chuột & lăn chuột để lướt dải môn học qua lại
(function () {
    function setupDragScroll(track) {
        if (!track || track.dataset.dragScrollInit) return;
        track.dataset.dragScrollInit = "true";

        let isDown = false;
        let startX = 0;
        let scrollLeft = 0;
        let hasMoved = false;

        track.addEventListener('mousedown', (e) => {
            // Chỉ bắt chuột trái (button = 0)
            if (e.button !== 0) return;
            isDown = true;
            hasMoved = false;
            track.classList.add('is-dragging');
            startX = e.pageX - track.offsetLeft;
            scrollLeft = track.scrollLeft;
        });

        window.addEventListener('mouseup', () => {
            if (isDown) {
                isDown = false;
                track.classList.remove('is-dragging');
                setTimeout(() => { hasMoved = false; }, 60);
            }
        });

        track.addEventListener('mousemove', (e) => {
            if (!isDown) return;
            e.preventDefault();
            const x = e.pageX - track.offsetLeft;
            const walk = (x - startX) * 1.4; // Hệ số tốc độ lướt
            if (Math.abs(walk) > 6) {
                hasMoved = true;
            }
            track.scrollLeft = scrollLeft - walk;
        });

        // Hỗ trợ lăn bánh xe chuột (Mouse Wheel) để cuộn ngang mượt mà khi rê chuột vào
        track.addEventListener('wheel', (e) => {
            if (e.deltaY !== 0) {
                e.preventDefault();
                track.scrollLeft += e.deltaY * 0.9;
            }
        }, { passive: false });

        // Ngăn chặn click nhầm chọn môn khi đang kéo chuột lướt
        track.addEventListener('click', (e) => {
            if (hasMoved) {
                e.stopPropagation();
                e.preventDefault();
            }
        }, true);
    }

    function initAllTracks() {
        document.querySelectorAll('.subjects-scroll-track').forEach(setupDragScroll);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAllTracks);
    } else {
        initAllTracks();
    }

    // Tự động lắng nghe thay đổi DOM khi Blazor Server render hoặc chuyển trang SPA
    const observer = new MutationObserver(() => {
        initAllTracks();
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();
