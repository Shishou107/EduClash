// arena-fx.js — hiệu ứng hình ảnh cho trận đấu (pháp sư pixel, phép thuật, HUD, mở màn/kết thúc).
// Chỉ lo phần "nhìn": logic HP, chấm điểm, đồng bộ PvP vẫn nằm trong Arena.cshtml.
(function () {
    const reduce = () => matchMedia('(prefers-reduced-motion: reduce)').matches;
    const $ = (id) => document.getElementById(id);
    const rand = (a, b) => a + Math.random() * (b - a);

    // ---------- Pháp sư pixel 16×20 (mỗi ký tự là 1 pixel) ----------
    const WIZARD = [
        '......Hh........',
        '.....HHh........',
        '.....HSHh...OO..',
        '....HHHHh..OOOO.',
        '...HHHHHHh.OOOO.',
        '..hhhhhhhhh.OO..',
        '....KKKKK....W..',
        '....KEKEK....W..',
        '....KKKKK....W..',
        '....BBBBB...KW..',
        '...RBBBBBRRRKW..',
        '..RRRBBBRRRR.W..',
        '..RRRRBRRRRr.W..',
        '..RrRRTRRRrr.W..',
        '..RrRNNPRRrr.W..',
        '..RrRNNPRRrr.W..',
        '..RRRRTRRRRr.W..',
        '..TTTTTTTTTT.W..',
        '...DD...DD...W..',
        '..DDD...DDD.....'
    ];
    const SHARED = { S: '#F5B70A', T: '#F5B70A', K: '#F2C9A0', E: '#0B1220', B: '#EDEDED', W: '#8A5A2B', D: '#1F2937', P: '#F8FAFC' };
    const TEAMS = {
        blue: { H: '#2F5DD0', h: '#1E3A8A', R: '#3B6FE0', r: '#24479E', N: '#0EA5E9', O: '#7DD3FC' },
        red:  { H: '#C2333A', h: '#7F1D1D', R: '#DC4A4F', r: '#8E2328', N: '#F97316', O: '#FDBA74' }
    };

    function wizardSvg(team) {
        const pal = Object.assign({}, SHARED, TEAMS[team]);
        let rects = '';
        WIZARD.forEach((row, y) => {
            let x = 0;
            while (x < row.length) {
                const ch = row[x];
                let w = 1;
                while (row[x + w] === ch) w++;
                if (pal[ch]) rects += `<rect x="${x}" y="${y}" width="${w}" height="1" fill="${pal[ch]}"${ch === 'O' ? ' class="wz-orb"' : ''}/>`;
                x += w;
            }
        });
        return `<svg viewBox="0 0 16 20" shape-rendering="crispEdges" aria-hidden="true">${rects}</svg>`;
    }

    // ---------- Âm thanh 8-bit ngắn ----------
    let actx = null;
    function beep(freq, dur = 0.12, type = 'square', vol = 0.18) {
        try {
            actx = actx || new (window.AudioContext || window.webkitAudioContext)();
            const o = actx.createOscillator(), g = actx.createGain();
            o.type = type;
            o.frequency.setValueAtTime(freq, actx.currentTime);
            g.gain.setValueAtTime(vol, actx.currentTime);
            g.gain.exponentialRampToValueAtTime(0.001, actx.currentTime + dur);
            o.connect(g); g.connect(actx.destination);
            o.start(); o.stop(actx.currentTime + dur);
        } catch (e) { }
    }

    // ---------- Vị trí ----------
    const stage = () => $('fxStage');
    const layer = () => $('fxLayer');
    function orbPoint(side) {
        const s = stage().getBoundingClientRect();
        const w = $(side === 1 ? 'wiz1' : 'wiz2').getBoundingClientRect();
        // quả cầu trên đầu gậy nằm ở cột 12-15, hàng 2-5 của lưới 16×20
        const fx = side === 1 ? 13.5 / 16 : 2.5 / 16;
        return { x: w.left - s.left + w.width * fx, y: w.top - s.top + w.height * (3.5 / 20) };
    }
    function bodyPoint(side) {
        const s = stage().getBoundingClientRect();
        const w = $(side === 1 ? 'wiz1' : 'wiz2').getBoundingClientRect();
        return { x: w.left - s.left + w.width / 2, y: w.top - s.top + w.height * 0.5 };
    }

    function spawn(cls, x, y, html = '') {
        const el = document.createElement('div');
        el.className = cls;
        el.style.left = x + 'px';
        el.style.top = y + 'px';
        el.innerHTML = html;
        layer().appendChild(el);
        return el;
    }

    function burst(x, y, color, count) {
        for (let i = 0; i < count; i++) {
            const p = spawn('fx-spark', x, y);
            p.style.background = Math.random() < 0.3 ? '#FFFFFF' : color;
            const a = rand(0, Math.PI * 2), d = rand(30, 110), size = rand(4, 9);
            p.style.width = p.style.height = size + 'px';
            p.animate([
                { transform: 'translate(-50%, -50%)', opacity: 1 },
                { transform: `translate(calc(-50% + ${Math.cos(a) * d}px), calc(-50% + ${Math.sin(a) * d + 20}px))`, opacity: 0 }
            ], { duration: rand(380, 650), easing: 'cubic-bezier(.16,1,.3,1)' }).onfinish = () => p.remove();
        }
    }

    function shake(strength = 8) {
        const screen = document.querySelector('.cabinet__screen');
        if (!screen || reduce()) return;
        const k = [];
        for (let i = 0; i < 7; i++) k.push({ transform: `translate(${rand(-strength, strength)}px, ${rand(-strength, strength)}px)` });
        k.push({ transform: 'none' });
        screen.animate(k, { duration: 380, easing: 'steps(7, end)' });
    }

    function flashScreen(color = 'rgba(255,255,255,.55)') {
        if (reduce()) return;
        const f = spawn('fx-flash', 0, 0);
        f.style.background = color;
        f.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 220 }).onfinish = () => f.remove();
    }

    function setPose(side, pose, ms) {
        const el = $(side === 1 ? 'wiz1' : 'wiz2');
        if (!el) return;
        el.classList.remove('is-cast', 'is-hit', 'is-ko', 'is-win');
        void el.offsetWidth; // chạy lại animation
        if (pose) el.classList.add(pose);
        if (ms) setTimeout(() => el.classList.remove(pose), ms);
    }

    const GLYPHS = ['Σ', 'π', 'λ', '√', '∫', 'Ω', '{ }', 'A+', 'x²', '∞'];

    // ---------- API ----------
    const FX = {
        mount() {
            const w1 = $('wiz1'), w2 = $('wiz2');
            if (w1 && !w1.dataset.ready) { w1.querySelector('.wizard__body').innerHTML = wizardSvg('blue'); w1.dataset.ready = '1'; }
            if (w2 && !w2.dataset.ready) { w2.querySelector('.wizard__body').innerHTML = wizardSvg('red'); w2.dataset.ready = '1'; }
        },

        reset() {
            FX.mount();
            layer() && (layer().innerHTML = '');
            [$('wiz1'), $('wiz2')].forEach(w => w && w.classList.remove('is-cast', 'is-hit', 'is-ko', 'is-win'));
            $('fxCombo') && ($('fxCombo').className = 'fx-combo');
            $('fxOverlay') && ($('fxOverlay').className = 'fx-overlay');
            FX.health(200, 200, true);
            $('questionText') && ($('questionText').textContent = '');
        },

        // Mở màn: 3 · 2 · 1 · FIGHT!
        intro(done) {
            const ov = $('fxOverlay');
            const steps = reduce() ? ['FIGHT!'] : ['3', '2', '1', 'FIGHT!'];
            let i = 0;
            const next = () => {
                if (i >= steps.length) { ov.className = 'fx-overlay'; done && done(); return; }
                const word = steps[i++];
                ov.textContent = word;
                ov.className = 'fx-overlay is-show' + (word === 'FIGHT!' ? ' is-fight' : '');
                beep(word === 'FIGHT!' ? 880 : 440, word === 'FIGHT!' ? 0.35 : 0.12);
                if (word === 'FIGHT!') { shake(6); flashScreen('rgba(245,183,10,.35)'); }
                setTimeout(next, word === 'FIGHT!' ? 700 : 520);
            };
            next();
        },

        // Câu hỏi hiện từng chữ (không chặn việc chọn đáp án)
        type(el, text) {
            const sr = $('questionTextSr');
            if (sr) sr.textContent = text;
            clearInterval(el._typing);
            if (reduce()) { el.textContent = text; return; }
            el.textContent = '';
            let n = 0;
            const step = Math.max(1, Math.ceil(text.length / 40)); // tối đa ~40 nhịp ≈ 0,7s
            el._typing = setInterval(() => {
                n = Math.min(text.length, n + step);
                el.textContent = text.slice(0, n);
                if (n >= text.length) clearInterval(el._typing);
            }, 18);
        },

        // Thanh thời gian cháy dần
        timer(sec, max = 15) {
            const bar = $('fxBurn');
            if (!bar) return;
            bar.style.transform = `scaleX(${Math.max(0, sec) / max})`;
            bar.parentElement.classList.toggle('is-low', sec <= 5);
            if (sec <= 3 && sec > 0) beep(220, 0.06, 'square', 0.08);
        },

        // Thanh máu + vệt máu trễ
        health(p1, p2, instant) {
            [[p1, 'p1Ghost'], [p2, 'p2Ghost']].forEach(([hp, id]) => {
                const g = $(id);
                if (!g) return;
                g.style.transition = instant ? 'none' : '';
                g.style.transform = `scaleX(${hp / 200})`;
            });
            [[p1, 'p1Hud'], [p2, 'p2Hud']].forEach(([hp, id]) => $(id)?.classList.toggle('is-danger', hp <= 60));
        },

        // Tung phép: cầu năng lượng bay từ người bắn sang đối thủ trong ~350ms
        cast(side, power = false) {
            setPose(side, 'is-cast', 320);
            if (reduce()) return;
            const from = orbPoint(side), to = bodyPoint(side === 1 ? 2 : 1);
            const color = side === 1 ? '#7DD3FC' : '#FDBA74';
            const orb = spawn('fx-orb' + (power ? ' is-power' : ''), from.x, from.y, `<span>${GLYPHS[Math.floor(Math.random() * GLYPHS.length)]}</span>`);
            orb.style.setProperty('--c', color);
            burst(from.x, from.y, color, 6);
            const trail = setInterval(() => {
                const r = orb.getBoundingClientRect(), s = stage().getBoundingClientRect();
                const t = spawn('fx-trail', r.left - s.left + r.width / 2, r.top - s.top + r.height / 2);
                t.style.background = color;
                t.animate([{ opacity: 0.9, transform: 'translate(-50%,-50%) scale(1)' }, { opacity: 0, transform: 'translate(-50%,-50%) scale(.2)' }], { duration: 260 }).onfinish = () => t.remove();
            }, 25);
            orb.animate([
                { transform: 'translate(-50%,-50%) scale(.6)' },
                { transform: `translate(calc(-50% + ${(to.x - from.x) / 2}px), calc(-50% + ${(to.y - from.y) / 2 - 36}px)) scale(1.15)` },
                { transform: `translate(calc(-50% + ${to.x - from.x}px), calc(-50% + ${to.y - from.y}px)) scale(1)` }
            ], { duration: 340, easing: 'cubic-bezier(.4,0,.8,.6)' }).onfinish = () => { clearInterval(trail); orb.remove(); };
        },

        // Trúng đòn: nổ hạt, rung màn hình, pháp sư bị đẩy lùi
        impact(targetSide, crit = false) {
            setPose(targetSide, 'is-hit', 420);
            const p = bodyPoint(targetSide);
            const color = targetSide === 2 ? '#7DD3FC' : '#FDBA74';
            if (!reduce()) {
                burst(p.x, p.y, color, crit ? 30 : 20);
                burst(p.x, p.y, '#F5B70A', crit ? 12 : 6);
                shake(crit ? 14 : 9);
                flashScreen(targetSide === 1 ? 'rgba(229,72,77,.35)' : 'rgba(255,255,255,.45)');
            }
            if (crit) {
                const c = spawn('fx-crit', p.x, p.y - 125, 'CRIT!');
                c.animate([{ opacity: 0, transform: 'translate(-50%,-50%) scale(2.2)' }, { opacity: 1, transform: 'translate(-50%,-50%) scale(1)', offset: 0.25 }, { opacity: 0, transform: 'translate(-50%,-90%) scale(1)' }], { duration: 900 }).onfinish = () => c.remove();
            }
        },

        // Trả lời sai: phép xịt khói
        fizzle(side = 1) {
            if (reduce()) return;
            const p = orbPoint(side);
            for (let i = 0; i < 8; i++) {
                const s = spawn('fx-smoke', p.x, p.y);
                s.animate([{ opacity: 0.8, transform: 'translate(-50%,-50%) scale(.6)' }, { opacity: 0, transform: `translate(calc(-50% + ${rand(-20, 20)}px), calc(-50% - ${rand(20, 50)}px)) scale(1.6)` }], { duration: rand(500, 800) }).onfinish = () => s.remove();
            }
            beep(110, 0.2, 'sawtooth', 0.1);
        },

        combo(n) {
            const el = $('fxCombo');
            if (!el) return;
            if (n < 2) { el.className = 'fx-combo'; return; }
            el.innerHTML = `COMBO <b>x${n}</b>`;
            el.className = 'fx-combo';
            void el.offsetWidth;
            el.className = 'fx-combo is-show' + (n >= 3 ? ' is-hot' : '');
            beep(520 + n * 80, 0.1);
        },

        // Kết thúc: chữ lớn + tư thế, rồi gọi done
        outro(kind, done) {
            const ov = $('fxOverlay');
            const map = {
                'ko-win': ['K.O.!', 2, 1, 'is-win'],
                'ko-lose': ['K.O.!', 1, 2, 'is-lose'],
                'win': ['YOU WIN', 2, 1, 'is-win'],
                'lose': ['YOU LOSE', 1, 2, 'is-lose'],
                'draw': ['DRAW', 0, 0, 'is-draw'],
                'forfeit': ['FORFEIT', 1, 2, 'is-lose']
            };
            const [text, loser, winner, tone] = map[kind] || map.draw;
            if (loser) setPose(loser, 'is-ko');
            if (winner) setPose(winner, 'is-win');
            ov.textContent = text;
            ov.className = `fx-overlay is-show is-end ${tone}`;
            if (kind.startsWith('ko')) { shake(16); flashScreen(); beep(140, 0.5, 'sawtooth', 0.2); }
            else beep(tone === 'is-win' ? 660 : 200, 0.4);
            setTimeout(() => { ov.className = 'fx-overlay'; done && done(); }, reduce() ? 700 : 1900);
        },

        // Pháo hoa pixel trên màn kết quả
        confetti(host) {
            if (reduce() || !host) return;
            const colors = ['#F5B70A', '#10B981', '#3B82F6', '#E5484D', '#FFFFFF'];
            const box = host.getBoundingClientRect();
            for (let i = 0; i < 70; i++) {
                const c = document.createElement('i');
                c.className = 'fx-confetti';
                c.style.background = colors[i % colors.length];
                c.style.left = rand(10, box.width - 10) + 'px';
                host.appendChild(c);
                c.animate([
                    { transform: `translate(0, -20px) rotate(0deg)`, opacity: 1 },
                    { transform: `translate(${rand(-60, 60)}px, ${rand(box.height * 0.5, box.height)}px) rotate(${rand(180, 720)}deg)`, opacity: 0 }
                ], { duration: rand(1200, 2200), delay: rand(0, 400), easing: 'cubic-bezier(.2,.6,.4,1)' }).onfinish = () => c.remove();
            }
        }
    };

    window.ArenaFX = FX;
    document.addEventListener('DOMContentLoaded', FX.mount);
})();
