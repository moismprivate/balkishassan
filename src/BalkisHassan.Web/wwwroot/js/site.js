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

const audioPlayers = [...document.querySelectorAll('[data-audio-player]')];
const formatAudioTime = value => {
  if (!Number.isFinite(value)) return '--:--';
  const minutes = Math.floor(value / 60);
  const seconds = Math.floor(value % 60).toString().padStart(2, '0');
  return `${minutes}:${seconds}`;
};

audioPlayers.forEach(player => {
  const audio = player.querySelector('audio');
  const toggle = player.querySelector('.audio-toggle');
  const icon = toggle?.querySelector('.audio-toggle-icon');
  const label = toggle?.querySelector('.audio-toggle-label');
  const progress = player.querySelector('[data-audio-progress]');
  const current = player.querySelector('[data-audio-current]');
  const duration = player.querySelector('[data-audio-duration]');
  const message = player.querySelector('[data-audio-message]');
  if (!audio || !toggle || !icon || !label || !progress || !current || !duration || !message) return;

  const showError = () => { message.hidden = false; };
  const hideError = () => { message.hidden = true; };
  const setPaused = paused => {
    icon.textContent = paused ? '▶' : 'Ⅱ';
    label.textContent = paused ? 'تشغيل الصوت' : 'إيقاف مؤقت';
    toggle.setAttribute('aria-label', paused ? 'تشغيل الصوت' : 'إيقاف الصوت مؤقتاً');
    player.classList.toggle('is-playing', !paused);
  };

  audio.addEventListener('loadedmetadata', () => {
    duration.textContent = formatAudioTime(audio.duration);
    hideError();
  });
  audio.addEventListener('durationchange', () => { duration.textContent = formatAudioTime(audio.duration); });
  audio.addEventListener('timeupdate', () => {
    current.textContent = formatAudioTime(audio.currentTime);
    progress.value = Number.isFinite(audio.duration) && audio.duration > 0
      ? String(Math.round((audio.currentTime / audio.duration) * 1000)) : '0';
  });
  audio.addEventListener('pause', () => setPaused(true));
  audio.addEventListener('play', () => {
    hideError();
    setPaused(false);
  });
  audio.addEventListener('ended', () => setPaused(true));
  toggle.addEventListener('click', async () => {
    if (!audio.paused) {
      audio.pause();
      return;
    }
    audioPlayers.forEach(other => {
      if (other !== player) other.querySelector('audio')?.pause();
    });
    try {
      await audio.play();
    } catch {
      showError();
      setPaused(true);
    }
  });

  progress.addEventListener('input', () => {
    if (Number.isFinite(audio.duration) && audio.duration > 0) {
      audio.currentTime = (Number(progress.value) / 1000) * audio.duration;
    }
  });
});
