document.addEventListener('click', async event => {
  const menuButton = event.target.closest('.menu-toggle');
  if (menuButton) {
    const menu = document.getElementById('main-menu');
    const open = menu?.classList.toggle('open') ?? false;
    menuButton.setAttribute('aria-expanded', String(open));
    return;
  }

  const mediaPath = event.target.closest('.media-grid input[readonly]');
  if (mediaPath) {
    mediaPath.select();
    await navigator.clipboard?.writeText(mediaPath.value);
  }
});

const formatAudioTime = value => {
  if (!Number.isFinite(value)) return '--:--';
  const minutes = Math.floor(value / 60);
  const seconds = Math.floor(value % 60).toString().padStart(2, '0');
  return `${minutes}:${seconds}`;
};

const initializeAudioPlayers = () => document.querySelectorAll('[data-audio-player]:not([data-initialized])').forEach(player => {
  player.dataset.initialized = 'true';
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
    document.querySelectorAll('[data-audio-player]').forEach(other => {
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

initializeAudioPlayers();
document.addEventListener('DOMContentLoaded', initializeAudioPlayers);
window.Blazor?.addEventListener('enhancedload', initializeAudioPlayers);
