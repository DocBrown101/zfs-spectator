(() => {
    const tokenPattern = /\{(pool|dataset|snapshot)\}/g;

    const resolveTemplate = (template, source) => template.replace(
        tokenPattern,
        (token, name) => source.dataset[`command${name[0].toUpperCase()}${name.slice(1)}`] ?? token);

    const populateMenu = (menu, source) => {
        menu.querySelectorAll('[data-command-template]').forEach(item => {
            item.dataset.command = resolveTemplate(item.dataset.commandTemplate, source);

            const preview = item.querySelector('[data-command-preview]');
            if (preview) preview.textContent = item.dataset.command;

            const description = item.querySelector('[data-description-template]');
            if (description) {
                description.textContent = resolveTemplate(description.dataset.descriptionTemplate, source);
            }
        });
    };

    const showContextMenu = (source, x, y) => {
        const menu = document.getElementById(source.dataset.commandContext);
        const anchor = menu?.querySelector('[data-command-context-anchor]');
        if (!menu || !anchor) return;

        document.querySelectorAll('.command-context-menu [data-command-context-anchor]').forEach(otherAnchor => {
            if (otherAnchor !== anchor) bootstrap.Dropdown.getInstance(otherAnchor)?.hide();
        });

        const dropdown = bootstrap.Dropdown.getOrCreateInstance(anchor);
        dropdown.hide();
        populateMenu(menu, source);
        menu.style.left = `${x}px`;
        menu.style.top = `${y}px`;
        dropdown.show();
    };

    const showToast = (message, success) => {
        const toast = document.getElementById('command-copy-toast');
        if (!toast) return;

        toast.classList.toggle('text-bg-success', success);
        toast.classList.toggle('text-bg-danger', !success);
        const body = toast.querySelector('[data-command-toast-message]');
        if (body) body.textContent = message;
        bootstrap.Toast.getOrCreateInstance(toast, { delay: 1800 }).show();
    };

    const copyText = async text => {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(text);
            return;
        }

        const textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        document.body.appendChild(textarea);
        textarea.select();
        const copied = document.execCommand('copy');
        textarea.remove();
        if (!copied) throw new Error('Clipboard copy failed');
    };

    document.addEventListener('contextmenu', event => {
        const source = event.target.closest('[data-command-context]');
        if (!source) return;

        event.preventDefault();
        showContextMenu(source, event.clientX, event.clientY);
    });

    document.addEventListener('click', async event => {
        const item = event.target.closest('.command-menu-copy');
        if (!item) return;

        try {
            await copyText(item.dataset.command ?? item.dataset.commandTemplate);
            showToast('Command copied to clipboard.', true);
        } catch {
            showToast('Could not copy the command.', false);
        }
    });

    window.addEventListener('blur', () => {
        document.querySelectorAll('.command-context-menu [data-command-context-anchor]').forEach(anchor => {
            bootstrap.Dropdown.getInstance(anchor)?.hide();
        });
    });

})();
