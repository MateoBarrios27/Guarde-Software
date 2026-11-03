const MUTED_KEY = 'chatSoundMuted';

/**
 * Aviso sonoro de un mensaje nuevo del chat. Genera el tono con Web Audio, así que no
 * hay archivos que desplegar. El navegador puede mantener el audio bloqueado hasta que
 * la persona toque la página: en ese caso el aviso visual alcanza y el error se ignora.
 */
export class ChatNotificationSound {
  private static context?: AudioContext;

  static isMuted(): boolean {
    try {
      return localStorage.getItem(MUTED_KEY) === '1';
    } catch {
      return false;
    }
  }

  static setMuted(muted: boolean): void {
    try {
      if (muted) localStorage.setItem(MUTED_KEY, '1');
      else localStorage.removeItem(MUTED_KEY);
    } catch {
      // Sin almacenamiento, la preferencia vale solo para esta sesión.
    }
  }

  static play(): void {
    if (ChatNotificationSound.isMuted()) return;

    try {
      const Context = window.AudioContext
        ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
      if (!Context) return;

      const context = ChatNotificationSound.context ??= new Context();
      void context.resume();

      // Dos notas cortas y suaves: se distinguen de otros sonidos del sistema.
      ChatNotificationSound.tone(context, 880, 0, 0.14);
      ChatNotificationSound.tone(context, 1175, 0.15, 0.2);
    } catch {
      // Audio no disponible.
    }
  }

  private static tone(context: AudioContext, frequency: number, delay: number, duration: number): void {
    const start = context.currentTime + delay;
    const oscillator = context.createOscillator();
    const gain = context.createGain();

    oscillator.type = 'sine';
    oscillator.frequency.value = frequency;
    gain.gain.setValueAtTime(0.0001, start);
    gain.gain.exponentialRampToValueAtTime(0.18, start + 0.02);
    gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);

    oscillator.connect(gain).connect(context.destination);
    oscillator.start(start);
    oscillator.stop(start + duration + 0.02);
  }
}
