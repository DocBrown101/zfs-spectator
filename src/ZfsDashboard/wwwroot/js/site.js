// Initialize all Bootstrap tooltips
document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
    new bootstrap.Tooltip(el);
});

// Copy-to-clipboard for command blocks
document.addEventListener('click', function (e) {
    const btn = e.target.closest('.btn-copy');
    if (!btn) return;

    const target = btn.closest('.cmd-block');
    if (!target) return;

    const code = target.querySelector('code') || target.querySelector('pre');
    if (!code) return;

    navigator.clipboard.writeText(code.textContent.trim()).then(function () {
        const icon = btn.querySelector('i');
        const original = icon.className;
        icon.className = 'bi bi-check2';
        btn.classList.add('btn-success');
        btn.classList.remove('btn-outline-secondary');
        setTimeout(function () {
            icon.className = original;
            btn.classList.remove('btn-success');
            btn.classList.add('btn-outline-secondary');
        }, 1500);
    });
});
