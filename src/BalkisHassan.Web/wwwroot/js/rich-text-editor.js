const safeSync = (dotNetReference, value) => {
  dotNetReference.invokeMethodAsync('UpdateValue', value).catch(() => {});
};

const isSafeUrl = value => {
  const url = value.trim();
  return (/^https?:\/\//i.test(url) || /^mailto:/i.test(url) || /^#/.test(url) || (/^\//.test(url) && !/^\/\//.test(url)));
};

const sanitizeForPreview = html => {
  const template = document.createElement('template');
  template.innerHTML = html || '';
  template.content.querySelectorAll('script,style,iframe,object,embed,form,input,button,link,meta').forEach(element => element.remove());
  template.content.querySelectorAll('*').forEach(element => {
    for (const attribute of [...element.attributes]) {
      const name = attribute.name.toLowerCase();
      if (name.startsWith('on') || name === 'style' || name === 'srcdoc') element.removeAttribute(attribute.name);
    }
    if (element.hasAttribute('href') && !isSafeUrl(element.getAttribute('href'))) element.removeAttribute('href');
    if (element.hasAttribute('src') && !isSafeUrl(element.getAttribute('src'))) element.removeAttribute('src');
  });
  return template.innerHTML;
};

export function initialize(host, initialHtml, dotNetReference) {
  if (!host || host.dataset.initialized === 'true') return;

  host.dataset.initialized = 'true';
  const editor = host.querySelector('.rich-editor');
  const source = host.querySelector('.editor-source');
  const toolbar = host.querySelector('.editor-toolbar');
  const blockPicker = host.querySelector('[data-editor-block]');
  const sourceToggle = host.querySelector('[data-editor-action="source"]');
  if (!editor || !source || !toolbar || !blockPicker || !sourceToggle) return;

  const safeInitialHtml = sanitizeForPreview(initialHtml);
  editor.innerHTML = safeInitialHtml;
  source.value = safeInitialHtml;
  const controller = new AbortController();
  const options = { signal: controller.signal };

  const sourceMode = () => host.classList.contains('is-source-mode');
  const sync = () => {
    if (!sourceMode()) source.value = editor.innerHTML;
    safeSync(dotNetReference, source.value);
  };
  const execute = (command, value = null) => {
    editor.focus();
    document.execCommand(command, false, value);
    sync();
  };

  editor.addEventListener('input', sync, options);
  editor.addEventListener('blur', sync, options);
  source.addEventListener('input', sync, options);
  source.addEventListener('blur', sync, options);

  toolbar.addEventListener('mousedown', event => {
    if (event.target.closest('button')) event.preventDefault();
  }, options);

  toolbar.addEventListener('click', event => {
    const button = event.target.closest('button');
    if (!button) return;

    const command = button.dataset.editorCommand;
    if (command) {
      execute(command);
      return;
    }

    if (button.dataset.editorAction === 'link') {
      const url = window.prompt('أدخلي رابطاً يبدأ بـ https:// أو http:// أو /');
      if (url?.trim() && isSafeUrl(url)) execute('createLink', url.trim());
      else if (url?.trim()) window.alert('الرابط غير صالح. استخدمي https:// أو http:// أو رابطاً داخلياً يبدأ بـ /.');
      return;
    }

    if (button.dataset.editorAction === 'source') {
      if (sourceMode()) {
        source.value = sanitizeForPreview(source.value);
        editor.innerHTML = source.value;
        host.classList.remove('is-source-mode');
        button.classList.remove('active');
        button.setAttribute('aria-pressed', 'false');
        editor.focus();
      } else {
        source.value = editor.innerHTML;
        host.classList.add('is-source-mode');
        button.classList.add('active');
        button.setAttribute('aria-pressed', 'true');
        source.focus();
      }
      sync();
    }
  }, options);

  blockPicker.addEventListener('change', () => {
    execute('formatBlock', blockPicker.value);
    blockPicker.value = 'p';
  }, options);

  host.__balkisEditorController = controller;
  host.classList.add('is-initialized');
}

export function dispose(host) {
  host?.__balkisEditorController?.abort();
  if (host) delete host.__balkisEditorController;
}

export function setHtml(host, html) {
  if (!host) return;
  const editor = host.querySelector('.rich-editor');
  const source = host.querySelector('.editor-source');
  if (!editor || !source) return;
  const safeHtml = sanitizeForPreview(html);
  editor.innerHTML = safeHtml;
  source.value = safeHtml;
}
