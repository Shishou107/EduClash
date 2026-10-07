// EduClash — tiện ích giao diện dùng chung: thông báo nhỏ (toast), hộp xác nhận, hiện/ẩn mật khẩu.
(function () {
    const ICONS = { ok: 'i-check', err: 'i-alert', warn: 'i-alert', info: 'i-info' };

    function toast(message, type = 'ok', ms = 4200) {
        const stack = document.getElementById('toastStack');
        if (!stack) return;
        const el = document.createElement('div');
        el.className = `ec-toast ec-toast--${type}`;
        el.setAttribute('role', type === 'err' ? 'alert' : 'status');

        const icon = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        icon.setAttribute('class', 'icon');
        icon.setAttribute('aria-hidden', 'true');
        const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
        use.setAttribute('href', '#' + (ICONS[type] || ICONS.info));
        icon.appendChild(use);

        const text = document.createElement('span');
        text.textContent = message;

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'ec-toast__close';
        close.setAttribute('aria-label', 'Đóng thông báo');
        close.textContent = '×';

        el.append(icon, text, close);
        stack.appendChild(el);

        const remove = () => {
            el.classList.add('is-leaving');
            setTimeout(() => el.remove(), 200);
        };
        close.addEventListener('click', remove);
        if (ms > 0) setTimeout(remove, ms);
    }

    // Hộp xác nhận thay cho confirm() của trình duyệt. Trả về Promise<boolean>.
    function confirmDialog({ title, body, okText = 'Đồng ý', cancelText = 'Huỷ', danger = false }) {
        return new Promise((resolve) => {
            const dlg = document.createElement('dialog');
            dlg.className = 'ec-dialog';
            dlg.innerHTML = `
                <form method="dialog">
                    <div class="ec-dialog__body">
                        <h2></h2>
                        <p></p>
                    </div>
                    <div class="ec-dialog__actions">
                        <button value="cancel" class="btn-arcade btn-arcade--sm"></button>
                        <button value="ok" class="btn-arcade btn-arcade--sm ${danger ? 'btn-arcade--danger' : 'btn-arcade--gold'}"></button>
                    </div>
                </form>`;
            dlg.querySelector('h2').textContent = title;
            dlg.querySelector('p').textContent = body;
            const [cancelBtn, okBtn] = dlg.querySelectorAll('button');
            cancelBtn.textContent = cancelText;
            okBtn.textContent = okText;
            document.body.appendChild(dlg);
            dlg.addEventListener('close', () => {
                resolve(dlg.returnValue === 'ok');
                dlg.remove();
            });
            dlg.showModal();
            cancelBtn.focus();
        });
    }

    // Nút hiện/ẩn mật khẩu: <button class="pw-toggle" data-target="id">
    document.addEventListener('click', (e) => {
        const btn = e.target.closest('.pw-toggle');
        if (!btn) return;
        const input = document.getElementById(btn.dataset.target);
        if (!input) return;
        const show = input.type === 'password';
        input.type = show ? 'text' : 'password';
        btn.setAttribute('aria-pressed', String(show));
        btn.setAttribute('aria-label', show ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
        btn.querySelector('use')?.setAttribute('href', show ? '#i-eye-off' : '#i-eye');
    });

    window.EC = { toast, confirm: confirmDialog };
})();
