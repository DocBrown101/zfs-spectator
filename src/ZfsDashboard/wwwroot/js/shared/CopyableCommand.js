document.addEventListener('click', async event => {
    const btn = event.target.closest('.btn-copy');
    if (!btn) return;

    const target = btn.closest('.cmd-block');
    const code = target?.querySelector('code, pre');

    if (!code) return;

    await navigator.clipboard.writeText(code.textContent.trim());

    const icon = btn.querySelector('i');
    if (!icon) return;

    const original = icon.className;

    icon.className = 'bi bi-check2';
    btn.classList.replace('btn-outline-secondary', 'btn-success');

    setTimeout(() => {
        icon.className = original;
        btn.classList.replace('btn-success', 'btn-outline-secondary');
    }, 1500);
});
