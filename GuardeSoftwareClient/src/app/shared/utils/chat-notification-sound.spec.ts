import { ChatNotificationSound } from './chat-notification-sound';

describe('ChatNotificationSound', () => {
  afterEach(() => localStorage.removeItem('chatSoundMuted'));

  it('recuerda si el sonido se silenció', () => {
    expect(ChatNotificationSound.isMuted()).toBeFalse();

    ChatNotificationSound.setMuted(true);
    expect(ChatNotificationSound.isMuted()).toBeTrue();

    ChatNotificationSound.setMuted(false);
    expect(ChatNotificationSound.isMuted()).toBeFalse();
  });

  it('con el almacenamiento bloqueado no falla y el sonido queda activo', () => {
    spyOn(Storage.prototype, 'getItem').and.throwError('bloqueado');
    spyOn(Storage.prototype, 'setItem').and.throwError('bloqueado');

    expect(() => ChatNotificationSound.setMuted(true)).not.toThrow();
    expect(ChatNotificationSound.isMuted()).toBeFalse();
  });

  it('play no hace nada y no lanza cuando está silenciado', () => {
    ChatNotificationSound.setMuted(true);
    expect(() => ChatNotificationSound.play()).not.toThrow();
  });
});
