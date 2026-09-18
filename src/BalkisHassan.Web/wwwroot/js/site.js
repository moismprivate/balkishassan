document.querySelector('.menu-toggle')?.addEventListener('click', event => {
  const button = event.currentTarget;
  const menu = document.getElementById('main-menu');
  const open = menu?.classList.toggle('open') ?? false;
  button.setAttribute('aria-expanded', String(open));
});

const editor = document.querySelector('.rich-editor');
if (editor) {
  const source = document.getElementById(editor.dataset.source);
  document.querySelectorAll('.editor-toolbar [data-command]').forEach(button => {
    button.addEventListener('click', () => {
      const command = button.dataset.command;
      let value = button.dataset.value ?? null;
      if (command === 'createLink') value = window.prompt('أدخلي عنوان الرابط كاملاً');
      if (command !== 'createLink' || value) document.execCommand(command, false, value);
      editor.focus();
    });
  });
  editor.closest('form')?.addEventListener('submit', () => { source.value = editor.innerHTML; });
}

document.querySelectorAll('.media-grid input[readonly]').forEach(input => {
  input.addEventListener('click', async () => {
    input.select();
    await navigator.clipboard?.writeText(input.value);
  });
});
