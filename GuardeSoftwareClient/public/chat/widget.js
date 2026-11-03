/*
 * Chat en vivo de Guarde Lo Que Quiera para la web (Wix).
 *
 * Se instala con una sola etiqueta; ver GuardeSoftwareClient/public/chat/README.md:
 *   <script src="https://filgueira.ar/chat/widget.js" async></script>
 *
 * Opciones (atributos de la etiqueta <script>):
 *   data-api   Dirección base de la API. Por defecto, el origen desde donde se sirve este archivo.
 *   data-mode  "floating" (por defecto: burbuja flotante) o "inline" (ocupa todo el espacio
 *              disponible; sirve dentro de un elemento "Embed HTML" de Wix).
 *   data-right, data-bottom                Distancia en px de la burbuja al borde, en pantallas
 *   data-mobile-right, data-mobile-bottom  de más de 480 px y en celulares. Sirven para no tapar
 *                                          otros botones flotantes de la web (WhatsApp, barra de
 *                                          acciones rápidas de Wix). Por defecto 20/20 y 14/14.
 *
 * Desde la web también se puede abrir con window.GuardeChat.open(), con cualquier elemento
 * [data-guarde-chat-open], con un enlace a "#guarde-chat" o con la dirección ...#guarde-chat.
 *
 * Todo el contenido se escribe con textContent: un mensaje nunca se interpreta como HTML.
 * Sin dependencias: el cliente de tiempo real (signalr.min.js) se carga bajo demanda desde
 * el mismo dominio y, si no estuviera disponible, el chat sigue funcionando por consultas.
 */
(function () {
  'use strict';

  if (window.GuardeChat) return;

  var script = document.currentScript || document.querySelector('script[src*="/chat/widget.js"]');
  var scriptOrigin = '';
  try { scriptOrigin = script && script.src ? new URL(script.src, location.href).origin : ''; } catch (e) { /* sin origen */ }

  var API = trimSlash((script && script.getAttribute('data-api')) || scriptOrigin || location.origin);
  var INLINE = (script && script.getAttribute('data-mode')) === 'inline';
  var OFFSETS = {
    right: offsetAttribute('data-right'),
    bottom: offsetAttribute('data-bottom'),
    mobileRight: offsetAttribute('data-mobile-right'),
    mobileBottom: offsetAttribute('data-mobile-bottom')
  };
  var BASE = API + '/api/public/chat';
  var STORE_KEY = 'guarde-chat:v1';
  var MUTE_KEY = 'guarde-chat:mute';
  var LAUNCHER_LABEL = 'Chateá ahora mismo desde acá';
  var CONTACT_KEY = 'guarde-chat:contact';
  var RESUME_CLOSED_MS = 24 * 60 * 60 * 1000;
  var MAX_MESSAGE = 1000;
  var SYNC_OPEN_MS = 5000;
  var SYNC_IDLE_MS = 30000;
  var STATUS_REFRESH_MS = 60000;

  var memoryStore = {};
  var state = {
    open: INLINE,
    status: null,
    conv: null,
    messages: [],
    unread: 0,
    submitting: false,
    realtime: false
  };
  var tempSeq = 0;
  var hub = null;
  var syncTimer = null;
  var statusTimer = null;
  var readTimer = null;
  var stick = true;
  var previousOverflow = null;
  var audio = null;

  // ------------------------------------------------------------------ utilidades

  function trimSlash(value) { return String(value || '').replace(/\/+$/, ''); }

  // Un número entero de píxeles entre 0 y 400; cualquier otra cosa se ignora.
  function offsetAttribute(name) {
    var value = script && script.getAttribute(name);
    if (value === null || value === undefined || !/^\s*\d{1,3}\s*$/.test(value)) return null;
    var number = parseInt(value, 10);
    return number <= 400 ? number : null;
  }

  // Posición de la burbuja pedida en la etiqueta. Se declara después del CSS base para prevalecer.
  function launcherOffsetCss() {
    function rule(right, bottom) {
      return '.launcher{' +
        (right !== null ? 'right:' + right + 'px;' : '') +
        (bottom !== null ? 'bottom:calc(' + bottom + 'px + env(safe-area-inset-bottom,0px));' : '') + '}';
    }
    var css = '';
    if (OFFSETS.right !== null || OFFSETS.bottom !== null)
      css += '@media (min-width:481px){' + rule(OFFSETS.right, OFFSETS.bottom) + '}';
    if (OFFSETS.mobileRight !== null || OFFSETS.mobileBottom !== null)
      css += '@media (max-width:480px){' + rule(OFFSETS.mobileRight, OFFSETS.mobileBottom) + '}';
    return css;
  }

  function storeGet(key) {
    try { return window.localStorage.getItem(key); } catch (e) { return memoryStore[key] || null; }
  }
  function storeSet(key, value) {
    try { window.localStorage.setItem(key, value); } catch (e) { memoryStore[key] = value; }
  }
  function storeRemove(key) {
    try { window.localStorage.removeItem(key); } catch (e) { delete memoryStore[key]; }
  }
  function readJson(key) {
    try { return JSON.parse(storeGet(key) || 'null'); } catch (e) { return null; }
  }

  function h(tag, attrs, children) {
    var el = document.createElement(tag);
    Object.keys(attrs || {}).forEach(function (key) {
      var value = attrs[key];
      if (value === null || value === undefined || value === false) return;
      if (key === 'class') el.className = value;
      else if (key === 'text') el.textContent = value;
      else if (key.slice(0, 2) === 'on') el.addEventListener(key.slice(2), value);
      else el.setAttribute(key, value === true ? '' : value);
    });
    (children || []).forEach(function (child) {
      if (child === null || child === undefined || child === false) return;
      el.appendChild(typeof child === 'string' ? document.createTextNode(child) : child);
    });
    return el;
  }

  // Los íconos son constantes de este archivo, nunca datos del servidor ni del visitante.
  function icon(svg, size) {
    var span = document.createElement('span');
    span.className = 'icon';
    span.setAttribute('aria-hidden', 'true');
    span.style.width = span.style.height = size + 'px';
    span.innerHTML = svg;
    return span;
  }
  var ICON_CHAT = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 15a2 2 0 0 1-2 2H8l-5 4V6a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>';
  var ICON_CLOSE = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"><path d="M6 6l12 12M18 6L6 18"/></svg>';
  var ICON_SOUND_ON = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M11 5 6 9H2v6h4l5 4z"/><path d="M15.5 8.5a5 5 0 0 1 0 7"/><path d="M19 5a10 10 0 0 1 0 14"/></svg>';
  var ICON_SOUND_OFF = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M11 5 6 9H2v6h4l5 4z"/><path d="m22 9-6 6"/><path d="m16 9 6 6"/></svg>';
  var ICON_SEND ='<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 2 11 13"/><path d="M22 2l-7 20-4-9-9-4z"/></svg>';

  function formatTime(iso) {
    var date = new Date(iso);
    if (isNaN(date.getTime())) return '';
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  function dayLabel(iso) {
    var date = new Date(iso);
    var now = new Date();
    var yesterday = new Date(now.getTime() - 86400000);
    if (date.toDateString() === now.toDateString()) return 'Hoy';
    if (date.toDateString() === yesterday.toDateString()) return 'Ayer';
    return date.toLocaleDateString([], { day: '2-digit', month: '2-digit' });
  }

  function errorMessage(error, fallback) {
    if (error && error.status === 429) return error.message || 'Estás enviando demasiado rápido. Esperá unos segundos.';
    if (error && error.status && error.status < 500 && error.message) return error.message;
    return fallback;
  }

  // ------------------------------------------------------------------------ red

  function request(method, path, body, token) {
    var headers = { 'Accept': 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (token) headers['X-Chat-Visitor-Token'] = token;

    return fetch(BASE + path, {
      method: method,
      mode: 'cors',
      credentials: 'omit',
      cache: 'no-store',
      headers: headers,
      body: body === undefined ? undefined : JSON.stringify(body)
    }).then(function (response) {
      if (response.status === 204) return null;
      return response.json().catch(function () { return null; }).then(function (data) {
        if (!response.ok) {
          var error = new Error((data && data.message) || '');
          error.status = response.status;
          throw error;
        }
        return data;
      });
    });
  }

  function loadStatus() {
    return request('GET', '/status').then(function (status) {
      state.status = status;
      renderStatus();
    }).catch(function () { /* se reintenta en el próximo ciclo */ });
  }

  // ------------------------------------------------------------- conversación

  function saveConversation(extra) {
    if (!state.conv) return;
    if (extra) Object.keys(extra).forEach(function (key) { state.conv[key] = extra[key]; });
    state.conv.lastActivityAt = new Date().toISOString();
    storeSet(STORE_KEY, JSON.stringify(state.conv));
  }

  function clearConversation() {
    disconnectRealtime();
    stopSync();
    state.conv = null;
    state.messages = [];
    state.unread = 0;
    renderedIds = {};
    storeRemove(STORE_KEY);
  }

  function lastRealId() {
    var id = 0;
    state.messages.forEach(function (message) { if (message.id > id) id = message.id; });
    return id;
  }

  function mergeMessages(incoming) {
    var changed = false;
    incoming.forEach(function (message) {
      var exists = state.messages.some(function (item) { return item.id === message.id; });
      if (exists) return;
      // Si ya mostramos este texto como "enviando", el mensaje confirmado lo reemplaza.
      if (message.senderType === 'visitor') {
        state.messages = state.messages.filter(function (item) { return !(item.pending && item.body === message.body); });
      }
      state.messages.push(message);
      changed = true;
    });
    if (changed) {
      state.messages.sort(function (a, b) {
        if (a.id < 0 || b.id < 0) return a.id < 0 ? 1 : -1;
        return a.id - b.id;
      });
    }
    return changed;
  }

  function applyConversation(data, replace) {
    if (replace) state.messages = [];
    var added = mergeMessages(data.messages || []);
    state.conv.status = data.status;
    if (data.chat) state.status = data.chat;
    return added;
  }

  function resume() {
    var saved = readJson(STORE_KEY);
    if (!saved || !saved.id || !saved.token) return;

    state.conv = saved;
    renderAll();
    request('GET', '/conversations/' + encodeURIComponent(saved.id) + '?after=0', undefined, saved.token)
      .then(function (data) {
        var idleMs = Date.now() - Date.parse(data.lastActivityAt || 0);
        if (data.status === 'closed' && idleMs > RESUME_CLOSED_MS) {
          clearConversation();
          renderAll();
          return;
        }
        applyConversation(data, true);
        state.unread = state.open ? 0 : (data.unreadCount || 0);
        connectRealtime();
        startSync();
        renderAll();
        if (state.open) markRead();
      })
      .catch(function (error) {
        if (error && error.status === 404) clearConversation();
        renderAll();
      });
  }

  function start(form) {
    state.submitting = true;
    setFormError('');
    setSubmitting(true);

    var payload = {
      name: form.name,
      email: form.email,
      phone: form.phone,
      message: form.message,
      sourceUrl: pageUrl(),
      website: form.website
    };

    request('POST', '/conversations', payload).then(function (data) {
      state.submitting = false;
      state.conv = {
        id: data.conversationId,
        token: data.visitorToken,
        name: data.visitorName,
        hasEmail: !!form.email,
        status: data.status
      };
      storeSet(CONTACT_KEY, JSON.stringify({ name: form.name, email: form.email, phone: form.phone }));
      saveConversation();
      state.messages = [];
      applyConversation(data, true);
      stick = true;
      connectRealtime();
      startSync();
      renderAll();
      focusComposer();
    }).catch(function (error) {
      state.submitting = false;
      setSubmitting(false);
      setFormError(errorMessage(error, 'No pudimos enviar tu mensaje. Revisá tu conexión e intentá de nuevo.'));
    });
  }

  function send(body) {
    if (!state.conv) return;
    var temp = { id: --tempSeq, senderType: 'visitor', body: body, createdAt: new Date().toISOString(), pending: true };
    state.messages.push(temp);
    stick = true;
    renderMessages();
    deliver(temp);
  }

  function deliver(temp) {
    temp.pending = true;
    temp.failed = false;
    request('POST', '/conversations/' + encodeURIComponent(state.conv.id) + '/messages', { body: temp.body }, state.conv.token)
      .then(function (message) {
        state.messages = state.messages.filter(function (item) { return item !== temp; });
        mergeMessages([message]);
        if (state.conv.status === 'closed') state.conv.status = 'open';
        saveConversation();
        renderAll();
      })
      .catch(function (error) {
        if (error && error.status === 404) {
          clearConversation();
          renderAll();
          return;
        }
        temp.pending = false;
        temp.failed = true;
        temp.error = errorMessage(error, 'No se envió.');
        renderMessages();
      });
  }

  function markRead() {
    if (!state.conv || !state.open || document.visibilityState === 'hidden') return;
    var upTo = lastRealId();
    if (upTo <= 0) return;
    request('POST', '/conversations/' + encodeURIComponent(state.conv.id) + '/read', { upToMessageId: upTo }, state.conv.token)
      .catch(function () { /* no es crítico */ });
  }

  function scheduleRead() {
    clearTimeout(readTimer);
    readTimer = setTimeout(markRead, 400);
  }

  function sync() {
    if (!state.conv) return Promise.resolve();
    return request('GET', '/conversations/' + encodeURIComponent(state.conv.id) + '?after=' + lastRealId(), undefined, state.conv.token)
      .then(function (data) {
        var known = {};
        state.messages.forEach(function (message) { known[message.id] = true; });
        applyConversation(data, false);
        var fresh = (data.messages || []).filter(function (message) { return !known[message.id]; });
        if (fresh.length) onIncomingBatch(fresh);
        renderAll();
      })
      .catch(function (error) {
        if (error && error.status === 404) { clearConversation(); renderAll(); }
      });
  }

  function onIncomingBatch(messages) {
    var replies = messages.filter(function (message) { return message.senderType === 'staff'; }).length;
    if (!replies) return;

    // Cada respuesta suena, también con el chat abierto: el visitante puede estar leyendo otra parte de la web.
    chime();
    if (document.visibilityState === 'hidden') flashTitle(replies);

    if (state.open && document.visibilityState !== 'hidden') {
      scheduleRead();
    } else {
      state.unread += replies;
      nudgeLauncher();
    }
  }

  // Con la pestaña en segundo plano, el título avisa que llegó una respuesta hasta que la persona vuelve.
  var originalTitle = null;
  var titleTimer = null;
  function flashTitle(count) {
    if (originalTitle === null) originalTitle = document.title;
    var alert = count > 1 ? '(' + count + ') Nuevas respuestas' : '(1) Nueva respuesta';
    var showAlert = true;
    clearInterval(titleTimer);
    titleTimer = setInterval(function () {
      document.title = showAlert ? alert : originalTitle;
      showAlert = !showAlert;
    }, 1200);
    document.title = alert;
  }

  function restoreTitle() {
    clearInterval(titleTimer);
    titleTimer = null;
    if (originalTitle !== null) document.title = originalTitle;
    originalTitle = null;
  }

  function nudgeLauncher() {
    if (!launcher || INLINE) return;
    launcher.classList.remove('launcher--nudge');
    void launcher.offsetWidth;
    launcher.classList.add('launcher--nudge');
  }

  function startSync() {
    stopSync();
    // Con tiempo real conectado alcanza un repaso lento; sin él se consulta seguido.
    var interval = state.realtime ? STATUS_REFRESH_MS : (state.open ? SYNC_OPEN_MS : SYNC_IDLE_MS);
    syncTimer = setInterval(sync, interval);
  }

  function stopSync() {
    clearInterval(syncTimer);
    syncTimer = null;
  }

  function pageUrl() {
    var url = '';
    try { url = window.top.location.href; } catch (e) { url = document.referrer || location.href; }
    try { var parsed = new URL(url); return parsed.origin + parsed.pathname; } catch (e) { return ''; }
  }

  // ---------------------------------------------------------------- tiempo real

  function connectRealtime() {
    if (hub || !state.conv) return;
    if (window.signalR) return openHub();

    var tag = document.createElement('script');
    tag.src = API + '/chat/signalr.min.js';
    tag.async = true;
    tag.onload = function () { if (window.signalR) openHub(); };
    tag.onerror = function () { /* sigue por consultas */ };
    document.head.appendChild(tag);
  }

  function openHub() {
    if (hub || !state.conv || !window.signalR) return;

    var connection = new window.signalR.HubConnectionBuilder()
      .withUrl(API + '/hubs/chat-visitor', { withCredentials: false })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
      .configureLogging(window.signalR.LogLevel.None)
      .build();
    hub = connection;

    connection.on('ChatMessage', function (message) {
      if (!state.conv) return;
      var added = mergeMessages([message]);
      if (added) {
        if (message.senderType === 'staff') onIncomingBatch([message]);
        renderAll();
      }
    });
    connection.on('ChatStatus', function (payload) {
      if (!state.conv || !payload) return;
      state.conv.status = payload.status;
      renderAll();
    });

    var join = function () { return connection.invoke('Join', state.conv.id, state.conv.token); };
    connection.onreconnected(function () {
      join().then(sync).catch(function () { setRealtime(false); });
    });
    connection.onclose(function () { setRealtime(false); hub = null; });

    connection.start()
      .then(join)
      .then(function () { setRealtime(true); return sync(); })
      .catch(function () { setRealtime(false); hub = null; });
  }

  function setRealtime(value) {
    if (state.realtime === value) return;
    state.realtime = value;
    if (state.conv) startSync();
  }

  function disconnectRealtime() {
    var connection = hub;
    hub = null;
    state.realtime = false;
    if (connection) connection.stop().catch(function () { /* ya cerrada */ });
  }

  // ------------------------------------------------------------------- sonido

  function isMuted() { return storeGet(MUTE_KEY) === '1'; }

  function audioContext() {
    var Context = window.AudioContext || window.webkitAudioContext;
    if (!Context) return null;
    audio = audio || new Context();
    return audio;
  }

  // Se llama desde un gesto de la persona (toque o tecla): deja el audio habilitado para después.
  function unlockAudio() {
    try {
      var context = audioContext();
      if (context && context.state === 'suspended' && context.resume) context.resume();
    } catch (e) { /* sin audio */ }
  }

  // Aviso de respuesta: dos notas suaves, ascendentes, con un leve armónico (no se genera ningún
  // archivo). El navegador puede bloquear el audio hasta que la persona toque la página: en ese
  // caso el globo, el título de la pestaña y el movimiento del botón alcanzan; el error se ignora.
  function chime() {
    if (isMuted()) return;
    try {
      var context = audioContext();
      if (!context) return;
      if (context.resume) context.resume();
      [[659.25, 0, 0.22], [987.77, 0.13, 0.32]].forEach(function (note) {
        var start = context.currentTime + note[1];
        var oscillator = context.createOscillator();
        var overtone = context.createOscillator();
        var gain = context.createGain();
        oscillator.type = 'sine';
        overtone.type = 'triangle';
        oscillator.frequency.value = note[0];
        overtone.frequency.value = note[0] * 2;
        gain.gain.setValueAtTime(0.0001, start);
        gain.gain.exponentialRampToValueAtTime(0.16, start + 0.015);
        gain.gain.exponentialRampToValueAtTime(0.0001, start + note[2]);
        overtone.connect(gain);
        overtone.start(start);
        overtone.stop(start + note[2] + 0.02);
        oscillator.connect(gain);
        gain.connect(context.destination);
        oscillator.start(start);
        oscillator.stop(start + note[2] + 0.02);
      });
    } catch (e) { /* sin audio */ }
  }

  // --------------------------------------------------------------------- vista

  // Estética tomada de www.guardeloquequiera.net: azul acero #336A91 (hover #175B8B), títulos en
  // Poppins con letras espaciadas, textos en Avenir y botones rectos como «CONTACTANOS». Las fuentes
  // son las que ya carga la web de Wix; fuera de ella se usa la tipografía del sistema.
  var FONT_TITLE = '"poppins-semibold","Poppins","Avenir Next",system-ui,-apple-system,"Segoe UI",Roboto,Arial,sans-serif';
  var FONT_TEXT = '"avenir-lt-w01_35-light1475496","Avenir Next","Avenir",system-ui,-apple-system,"Segoe UI",Roboto,Arial,sans-serif';
  var FONT_STRONG = '"avenir-lt-w01_85-heavy1475544","Avenir Next","Avenir",system-ui,-apple-system,"Segoe UI",Roboto,Arial,sans-serif';

  var CSS = [
    ':host{all:initial}',
    '*,*::before,*::after{box-sizing:border-box}',
    // Las propiedades heredables (letter-spacing, text-transform...) cruzan el Shadow DOM: un `*{...!important}`
    // de la página llega al host y de ahí al widget. `all:initial` en un elemento interno lo corta de raíz.
    '.root{all:initial;display:block;font-family:' + FONT_TEXT + ';font-size:14px;line-height:1.45;color:#1f2d3a;-webkit-font-smoothing:antialiased}',
    'button,input,textarea{font:inherit;color:inherit;margin:0}',
    'button{cursor:pointer;border:0;background:none;padding:0}',
    'button:focus-visible,input:focus-visible,textarea:focus-visible{outline:2px solid #fff;outline-offset:2px;box-shadow:0 0 0 4px #336a91}',
    '[hidden]{display:none!important}',
    '.icon{display:inline-flex;flex-shrink:0}.icon svg{width:100%;height:100%}',

    // Luz «en vivo»: el punto se prende y se apaga con una transición suave y deja un halo que se expande.
    '.live{position:relative;display:inline-flex;flex-shrink:0;width:10px;height:10px}',
    '.live::before,.live::after{content:"";position:absolute;inset:0;border-radius:50%;background:#9fb3c4}',
    '.live::after{opacity:0}',
    '.live--on::before{background:#2ee66b;box-shadow:0 0 8px rgba(46,230,107,.85);animation:blink 1.8s ease-in-out infinite}',
    '.live--on::after{background:#2ee66b;animation:halo 1.8s ease-out infinite}',
    '@keyframes blink{0%,100%{opacity:1;box-shadow:0 0 8px rgba(46,230,107,.9)}50%{opacity:.28;box-shadow:0 0 0 rgba(46,230,107,0)}}',
    '@keyframes halo{0%{opacity:.55;transform:scale(1)}70%,100%{opacity:0;transform:scale(2.6)}}',

    '.launcher{position:fixed;right:20px;bottom:calc(20px + env(safe-area-inset-bottom,0px));z-index:2147482999;display:inline-flex;align-items:center;gap:10px;height:52px;padding:0 20px 0 16px;border:1px solid #fff;color:#fff;font-family:' + FONT_STRONG + ';font-size:15px;letter-spacing:.4px;background:#336a91;box-shadow:0 8px 22px rgba(23,91,139,.35);transition:background-color .2s ease,transform .2s ease}',
    '.launcher:hover{background:#175b8b;transform:translateY(-2px)}',
    '.launcher .live{margin-right:2px}',
    '.badge{position:absolute;top:-9px;right:-9px;min-width:22px;height:22px;padding:0 6px;border-radius:11px;color:#fff;font-family:' + FONT_STRONG + ';font-size:12px;line-height:20px;text-align:center;background:#e11d48;border:2px solid #fff}',

    '.panel{position:fixed;right:20px;bottom:calc(20px + env(safe-area-inset-bottom,0px));z-index:2147483000;display:flex;flex-direction:column;width:384px;height:min(640px,calc(100vh - 40px));height:min(640px,calc(100dvh - 40px));max-height:calc(100vh - 40px);overflow:hidden;border-radius:4px;background:#fff;box-shadow:0 22px 60px rgba(16,44,66,.34)}',
    '.panel--inline{inset:0;width:auto;height:auto;max-height:none;border-radius:0;box-shadow:none}',

    '.head{display:flex;align-items:center;gap:12px;padding:16px;color:#fff;background:#336a91;border-bottom:4px solid #175b8b}',
    '.mark{display:inline-flex;align-items:center;justify-content:center;width:44px;height:44px;flex-shrink:0;border:1px solid rgba(255,255,255,.7);font-family:' + FONT_TITLE + ';font-size:26px;line-height:1;color:#fff}',
    '.head__text{min-width:0;flex:1}',
    '.head__title{margin:0;font-family:' + FONT_TITLE + ';font-size:13.5px;font-weight:400;letter-spacing:2px;text-transform:uppercase;line-height:1.2}',
    '.head__status{display:flex;align-items:center;gap:8px;margin-top:6px;line-height:1.3}',
    '.head__state{font-family:' + FONT_STRONG + ';font-size:12.5px;letter-spacing:.3px}',
    '.head__sub{margin-top:2px;padding-left:18px;font-size:11.5px;line-height:1.3;opacity:.86}',
    '.head__btn{display:inline-flex;align-items:center;justify-content:center;width:34px;height:34px;flex-shrink:0;border:1px solid rgba(255,255,255,.55);color:#fff;transition:background-color .2s ease}',
    '.head__btn:hover{background:#175b8b}',
    '.head__btn[aria-pressed="true"]{opacity:.7}',

    '.banner{padding:10px 16px;font-size:12.5px;line-height:1.4;color:#7a4a05;background:#fff5e6;border-left:4px solid #f2a33a}.banner--muted{color:#175b8b;background:#eaf1f6;border-left-color:#336a91}',

    '.form{display:flex;flex:1;min-height:0;flex-direction:column;gap:13px;overflow-y:auto;padding:18px 16px;background:#fff}',
    '.form__intro{margin:0;color:#1f2d3a;font-size:15px;line-height:1.45}',
    '.field{display:flex;flex-direction:column;gap:6px}',
    '.field__label{color:#336a91;font-family:' + FONT_STRONG + ';font-size:11px;letter-spacing:1.2px;text-transform:uppercase}',
    '.field__input{width:100%;min-height:44px;padding:10px 12px;border:1px solid #c9d6e0;border-radius:2px;font-size:15px;background:#fff;transition:border-color .2s ease,box-shadow .2s ease}',
    '.field__input:focus{border-color:#336a91;outline:none;box-shadow:0 0 0 3px rgba(51,106,145,.18)}',
    'textarea.field__input{min-height:92px;resize:vertical}',
    '.field__hint{margin:0;color:#5e6b76;font-size:11.5px}',
    '.trap{position:absolute!important;left:-9999px!important;width:1px;height:1px;overflow:hidden}',
    '.error{margin:0;padding:9px 12px;color:#fff;font-family:' + FONT_STRONG + ';font-size:12.5px;background:#c8323c}',
    '.primary{display:inline-flex;align-items:center;justify-content:center;gap:8px;width:100%;min-height:50px;border:1px solid #336a91;color:#fff;font-family:' + FONT_STRONG + ';font-size:14px;letter-spacing:1.6px;text-transform:uppercase;background:#336a91;transition:background-color .2s ease}',
    '.primary:hover:not(:disabled){background:#175b8b}.primary:disabled{opacity:.55;cursor:default}',
    '.legal{margin:0;color:#7d8a95;font-size:11px}',

    '.thread{display:flex;flex:1;min-height:0;flex-direction:column}',
    '.msgs{display:flex;flex:1;min-height:0;flex-direction:column;gap:8px;overflow-y:auto;padding:16px 14px 10px;background:#f2f6f9;overscroll-behavior:contain}',
    '.day{align-self:center;margin:6px 0;padding:3px 10px;color:#5e6b76;font-family:' + FONT_STRONG + ';font-size:10.5px;letter-spacing:1px;text-transform:uppercase;background:#e3ebf1}',
    '.msg{display:flex;max-width:84%;flex-direction:column;gap:3px;align-self:flex-start}',
    '.msg--own{align-self:flex-end;align-items:flex-end}',
    '.msg__author{padding-left:2px;color:#336a91;font-family:' + FONT_TITLE + ';font-size:10px;letter-spacing:1.2px;text-transform:uppercase}',
    '.bubble{padding:9px 13px;border:1px solid #d6e0e8;border-radius:2px 12px 12px 12px;color:#1f2d3a;background:#fff;box-shadow:0 1px 2px rgba(16,44,66,.06)}',
    '.msg--own .bubble{border-color:#336a91;border-radius:12px 2px 12px 12px;color:#fff;background:#336a91}',
    '.msg--pending .bubble{opacity:.6}',
    '.bubble__text{margin:0;white-space:pre-wrap;overflow-wrap:anywhere;font-size:14.5px}',
    '.bubble__time{display:block;margin-top:3px;text-align:right;font-size:10.5px;opacity:.72;font-variant-numeric:tabular-nums}',
    '.retry{color:#c8323c;font-family:' + FONT_STRONG + ';font-size:11.5px;text-decoration:underline}',

    '.composer{display:flex;align-items:flex-end;gap:8px;padding:10px 12px calc(10px + env(safe-area-inset-bottom,0px));border-top:1px solid #d6e0e8;background:#fff}',
    '.composer__input{flex:1;min-width:0;min-height:46px;max-height:120px;resize:none;padding:12px;border:1px solid #c9d6e0;border-radius:2px;font-size:15px;line-height:1.3;background:#fff;transition:border-color .2s ease,box-shadow .2s ease}',
    '.composer__input:focus{border-color:#336a91;outline:none;box-shadow:0 0 0 3px rgba(51,106,145,.18)}',
    '.send{display:inline-flex;align-items:center;justify-content:center;width:46px;height:46px;flex-shrink:0;color:#fff;background:#336a91;transition:background-color .2s ease}',
    '.send:hover:not(:disabled){background:#175b8b}.send:disabled{opacity:.4;cursor:default}',

    '@media (max-width:480px){',
    '.panel:not(.panel--inline){inset:0;width:auto;height:auto;max-height:none;border-radius:0;padding-top:env(safe-area-inset-top,0px);background:#336a91}',
    '.panel:not(.panel--inline) .form,.panel:not(.panel--inline) .thread{background:#fff}',
    '.head{padding:14px 12px;gap:10px}.head__title{font-size:12.5px;letter-spacing:1.1px}.mark{width:40px;height:40px;font-size:23px}',
    '.launcher{right:14px;bottom:calc(14px + env(safe-area-inset-bottom,0px));height:48px;padding:0 14px 0 12px;gap:8px;font-size:14px;letter-spacing:.2px}',
    '}',
    '@media (prefers-reduced-motion:no-preference){',
    '.panel{animation:pop .2s ease-out}@keyframes pop{from{opacity:0;transform:translateY(10px)}to{opacity:1;transform:none}}',
    '.msg--new{animation:rise .22s ease-out}@keyframes rise{from{opacity:0;transform:translateY(6px)}to{opacity:1;transform:none}}',
    '.launcher--nudge{animation:nudge .6s ease-in-out 2}@keyframes nudge{0%,100%{transform:none}30%{transform:translateY(-5px)}60%{transform:translateY(1px)}}',
    '}',
    // Con movimiento reducido la luz queda fija en verde: sigue indicando que hay alguien en línea.
    '@media (prefers-reduced-motion:reduce){.live--on::before{animation:none;opacity:1}.live--on::after{animation:none;opacity:0}.launcher:hover{transform:none}}'
  ].join('');

  var host, shadow, launcher, launcherBadge, launcherDot, panel, titleEl, statusTextEl, statusSubEl, statusDot, muteButton, banner, formView, threadView, msgsEl, composerInput, sendButton;
  var renderedIds = {};

  function build() {
    host = h('div', { id: 'guarde-chat-host' });
    shadow = host.attachShadow ? host.attachShadow({ mode: 'open' }) : host;

    launcherBadge = h('span', { class: 'badge', hidden: true });
    launcherDot = h('span', { class: 'live', 'aria-hidden': 'true', hidden: true });
    launcher = h('button', {
      type: 'button', class: 'launcher', 'aria-haspopup': 'dialog', 'aria-expanded': 'false', onclick: function () { openPanel(); }
    }, [launcherDot, icon(ICON_CHAT, 22), h('span', { class: 'launcher__label', text: LAUNCHER_LABEL }), launcherBadge]);

    titleEl = h('h2', { class: 'head__title', text: 'Guarde Lo Que Quiera' });
    statusDot = h('span', { class: 'live', 'aria-hidden': 'true' });
    statusTextEl = h('span', { class: 'head__state', text: 'Conectando…' });
    statusSubEl = h('div', { class: 'head__sub', hidden: true });
    muteButton = h('button', { type: 'button', class: 'head__btn', onclick: toggleMute });
    var closeButton = h('button', { type: 'button', class: 'head__btn', 'aria-label': 'Cerrar el chat', onclick: function () { closePanel(); } },
      [icon(ICON_CLOSE, 14)]);
    var head = h('header', { class: 'head' }, [
      h('span', { class: 'mark', 'aria-hidden': 'true', text: 'G' }),
      h('div', { class: 'head__text' }, [titleEl, h('div', { class: 'head__status' }, [statusDot, statusTextEl]), statusSubEl]),
      muteButton,
      INLINE ? null : closeButton
    ]);
    renderMute();

    banner = h('div', { class: 'banner', role: 'status', hidden: true });
    formView = h('form', { class: 'form', novalidate: true, onsubmit: onFormSubmit, oninput: function () { setFormError(''); } });
    msgsEl = h('div', { class: 'msgs', 'aria-live': 'polite', 'aria-label': 'Mensajes', tabindex: '0', onscroll: function () {
      stick = msgsEl.scrollHeight - msgsEl.scrollTop - msgsEl.clientHeight < 60;
    } });
    composerInput = h('textarea', { class: 'composer__input', rows: '1', maxlength: String(MAX_MESSAGE), placeholder: 'Escribí tu mensaje…',
      'aria-label': 'Mensaje', oninput: onComposerInput, onkeydown: onComposerKey });
    sendButton = h('button', { type: 'submit', class: 'send', 'aria-label': 'Enviar mensaje', disabled: true }, [icon(ICON_SEND, 20)]);
    var composer = h('form', { class: 'composer', onsubmit: onComposerSubmit }, [composerInput, sendButton]);
    threadView = h('div', { class: 'thread', hidden: true }, [msgsEl, composer]);

    panel = h('section', {
      class: 'panel' + (INLINE ? ' panel--inline' : ''), role: 'dialog', 'aria-label': 'Chat en vivo de Guarde Lo Que Quiera', hidden: !state.open
    }, [head, banner, formView, threadView]);

    var root = h('div', { class: 'root' }, [launcher, panel]);
    if (INLINE) launcher.hidden = true;
    shadow.appendChild(h('style', { text: CSS + launcherOffsetCss() }));
    shadow.appendChild(root);
    document.body.appendChild(host);

    panel.addEventListener('keydown', function (event) {
      if (event.key === 'Escape' && !INLINE) { event.stopPropagation(); closePanel(); }
    });
    // El navegador sólo deja sonar audio después de un gesto de la persona: se habilita en el
    // primer toque o tecla dentro del chat, así el aviso de la respuesta suena aunque llegue después.
    root.addEventListener('pointerdown', unlockAudio);
    root.addEventListener('keydown', unlockAudio);
  }

  function renderMute() {
    var muted = isMuted();
    muteButton.textContent = '';
    muteButton.appendChild(icon(muted ? ICON_SOUND_OFF : ICON_SOUND_ON, 16));
    muteButton.setAttribute('aria-pressed', muted ? 'true' : 'false');
    muteButton.setAttribute('aria-label', muted ? 'Activar el sonido de aviso' : 'Silenciar el sonido de aviso');
    muteButton.title = muted ? 'Sonido desactivado' : 'Sonido activado';
  }

  function toggleMute() {
    if (isMuted()) storeRemove(MUTE_KEY); else storeSet(MUTE_KEY, '1');
    renderMute();
    if (!isMuted()) chime();
  }

  function renderAll() {
    renderStatus();
    renderBadge();
    var hasConversation = !!state.conv;
    formView.hidden = hasConversation;
    threadView.hidden = !hasConversation;
    if (hasConversation) renderMessages(); else renderForm();
  }

  function renderBadge() {
    launcherBadge.hidden = state.unread <= 0 || state.open;
    launcherBadge.textContent = state.unread > 9 ? '9+' : String(state.unread);
    launcher.setAttribute('aria-label', state.unread > 0 ? 'Abrir el chat: ' + state.unread + ' mensajes nuevos' : 'Abrir el chat en vivo');
  }

  function renderStatus() {
    var status = state.status;
    // El texto de introducción se actualiza sin redibujar el formulario: no se pierde lo escrito ni el foco.
    if (formIntro && !state.conv) formIntro.textContent = introText();
    if (!status) {
      statusDot.className = 'live';
      launcherDot.hidden = true;
      statusTextEl.textContent = 'Chat en vivo';
      statusSubEl.hidden = true;
      banner.hidden = true;
      return;
    }

    // En horario la luz verde titila: hay alguien atendiendo ahora.
    statusDot.className = 'live' + (status.isOpen ? ' live--on' : '');
    launcherDot.className = 'live live--on';
    launcherDot.hidden = !status.isOpen;
    statusTextEl.textContent = status.isOpen ? 'En línea ahora' : 'Fuera de horario';
    statusSubEl.textContent = status.isOpen
      ? 'Respondemos en menos de ' + status.responseTimeMinutes + ' minutos'
      : status.scheduleLabel || '';
    statusSubEl.hidden = !statusSubEl.textContent;

    var note = '';
    var muted = false;
    if (state.conv && state.conv.status === 'closed') {
      note = 'Cerramos esta conversación. Si escribís de nuevo, la retomamos.';
      muted = true;
    } else if (state.conv && !status.isOpen && status.nextOpenLabel) {
      // Sin conversación, la introducción del formulario ya lo explica: no se repite.
      note = 'Estamos fuera de horario. Tu mensaje queda guardado y te respondemos ' + status.nextOpenLabel + '.' +
        (state.conv && state.conv.hasEmail ? ' Te avisamos por email.' : '');
    }
    banner.hidden = !note;
    banner.textContent = note;
    banner.className = 'banner' + (muted ? ' banner--muted' : '');
  }

  var formRefs = null;
  var formIntro = null;
  var formError = null;
  var formSubmit = null;

  function setFormError(message) {
    if (!formError) return;
    formError.textContent = message;
    formError.hidden = !message;
    // El botón está debajo de los campos: el aviso tiene que quedar a la vista.
    if (message && formError.scrollIntoView) formError.scrollIntoView({ block: 'nearest' });
  }

  function setSubmitting(value) {
    if (!formSubmit) return;
    formSubmit.disabled = !!value;
    formSubmit.textContent = value ? 'Enviando…' : 'Iniciar chat';
  }

  function introText() {
    var status = state.status;
    if (!status) return 'Escribinos tu consulta.';
    if (status.isOpen) return 'Escribinos tu consulta y te respondemos en menos de ' + status.responseTimeMinutes + ' minutos.';
    return 'Ahora estamos fuera de horario. Dejanos tu consulta: queda guardada y te respondemos ' + (status.nextOpenLabel || 'en cuanto abramos') + '.';
  }

  function renderForm() {
    var contact = readJson(CONTACT_KEY) || {};
    var existing = formRefs;

    // Se conserva lo que la persona ya escribió cuando la vista se vuelve a dibujar.
    var keep = existing ? {
      name: existing.name.value, email: existing.email.value, phone: existing.phone.value, message: existing.message.value
    } : { name: contact.name || '', email: contact.email || '', phone: contact.phone || '', message: '' };

    var refs = {
      name: h('input', { class: 'field__input', type: 'text', name: 'name', autocomplete: 'name', maxlength: '100', required: true }),
      email: h('input', { class: 'field__input', type: 'email', name: 'email', autocomplete: 'email', maxlength: '254', inputmode: 'email' }),
      phone: h('input', { class: 'field__input', type: 'tel', name: 'phone', autocomplete: 'tel', maxlength: '40', inputmode: 'tel' }),
      message: h('textarea', { class: 'field__input', name: 'message', maxlength: String(MAX_MESSAGE), required: true, placeholder: 'Contanos en qué te podemos ayudar' }),
      website: h('input', { class: 'field__input', type: 'text', name: 'website', tabindex: '-1', autocomplete: 'off' })
    };
    refs.name.value = keep.name;
    refs.email.value = keep.email;
    refs.phone.value = keep.phone;
    refs.message.value = keep.message;

    function field(label, input, hint) {
      return h('label', { class: 'field' }, [h('span', { class: 'field__label', text: label }), input, hint ? h('p', { class: 'field__hint', text: hint }) : null]);
    }

    formView.textContent = '';
    formIntro = h('p', { class: 'form__intro', text: introText() });
    formView.appendChild(formIntro);
    formView.appendChild(field('Nombre', refs.name));
    formView.appendChild(field('Email', refs.email));
    formView.appendChild(field('Teléfono', refs.phone, 'Dejanos un email o un teléfono para poder responderte si cerrás la página.'));
    formView.appendChild(field('Tu consulta', refs.message));
    formView.appendChild(h('div', { class: 'trap', 'aria-hidden': 'true' }, [field('No completar este campo', refs.website)]));
    // El error y el botón se actualizan en sitio: redibujar el formulario le quitaría el foco a quien escribe.
    formError = h('p', { class: 'error', role: 'alert', hidden: true });
    formSubmit = h('button', { type: 'submit', class: 'primary' }, ['Iniciar chat']);
    formView.appendChild(formError);
    formView.appendChild(formSubmit);
    setSubmitting(state.submitting);
    formView.appendChild(h('p', { class: 'legal', text: 'Guardamos tu consulta y tus datos de contacto solo para responderte.' }));
    formRefs = refs;
  }

  function renderMessages() {
    msgsEl.textContent = '';
    var lastDay = '';
    // Sólo se anima lo que aparece por primera vez; el historial que se carga al abrir queda quieto.
    var firstRender = !Object.keys(renderedIds).length;
    state.messages.forEach(function (message) {
      var key = String(message.id);
      var animate = !firstRender && !renderedIds[key] && (message.pending || message.senderType === 'staff');
      renderedIds[key] = true;
      var label = dayLabel(message.createdAt);
      if (label !== lastDay) {
        msgsEl.appendChild(h('div', { class: 'day', text: label }));
        lastDay = label;
      }

      var own = message.senderType === 'visitor';
      var bubble = h('div', { class: 'bubble' }, [
        h('p', { class: 'bubble__text', text: message.body }),
        h('time', { class: 'bubble__time', text: message.failed ? message.error || 'No se envió' : message.pending ? 'Enviando…' : formatTime(message.createdAt) })
      ]);
      var wrapper = h('article', { class: 'msg' + (own ? ' msg--own' : '') + (message.pending ? ' msg--pending' : '') + (animate ? ' msg--new' : '') }, [
        own ? null : h('span', { class: 'msg__author', text: 'Guarde Lo Que Quiera' }),
        bubble,
        message.failed ? h('button', { type: 'button', class: 'retry', text: 'Tocá para reintentar', onclick: function () { deliver(message); renderMessages(); } }) : null
      ]);
      msgsEl.appendChild(wrapper);
    });

    updateSendState();
    if (stick) msgsEl.scrollTop = msgsEl.scrollHeight;
  }

  // ------------------------------------------------------------------ eventos

  function onFormSubmit(event) {
    event.preventDefault();
    if (state.submitting || !formRefs) return;

    var form = {
      name: formRefs.name.value.trim(),
      email: formRefs.email.value.trim(),
      phone: formRefs.phone.value.trim(),
      message: formRefs.message.value.trim(),
      website: formRefs.website.value
    };

    var problem = '';
    if (form.name.length < 2) problem = 'Contanos tu nombre para poder responderte.';
    else if (!form.email && !form.phone) problem = 'Dejanos un email o un teléfono para poder responderte.';
    else if (form.email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) problem = 'El email no parece válido.';
    else if (!form.message) problem = 'Escribí tu consulta.';

    if (problem) {
      setFormError(problem);
      return;
    }
    start(form);
  }

  function onComposerInput() {
    composerInput.style.height = 'auto';
    composerInput.style.height = Math.min(composerInput.scrollHeight, 120) + 'px';
    updateSendState();
  }

  function onComposerKey(event) {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      onComposerSubmit(event);
    }
  }

  function onComposerSubmit(event) {
    event.preventDefault();
    var body = composerInput.value.trim();
    if (!body || !state.conv) return;
    composerInput.value = '';
    composerInput.style.height = 'auto';
    send(body);
  }

  function updateSendState() {
    sendButton.disabled = composerInput.value.trim().length === 0;
  }

  function focusComposer() {
    setTimeout(function () {
      if (state.conv) composerInput.focus();
      else if (formRefs) (formRefs.name.value ? formRefs.message : formRefs.name).focus();
    }, 30);
  }

  // ------------------------------------------------------------------- panel

  function lockScroll(lock) {
    if (INLINE || window.innerWidth > 480) return;
    var root = document.documentElement;
    if (lock) { previousOverflow = root.style.overflow; root.style.overflow = 'hidden'; }
    else if (previousOverflow !== null) { root.style.overflow = previousOverflow; previousOverflow = null; }
  }

  function openPanel() {
    if (!host) return;
    state.open = true;
    state.unread = 0;
    panel.hidden = false;
    launcher.hidden = true;
    launcher.setAttribute('aria-expanded', 'true');
    lockScroll(true);
    stick = true;
    renderAll();
    loadStatus();
    clearInterval(statusTimer);
    statusTimer = setInterval(loadStatus, STATUS_REFRESH_MS);
    if (state.conv) { connectRealtime(); startSync(); sync().then(markRead); }
    focusComposer();
  }

  function closePanel() {
    if (INLINE) return;
    state.open = false;
    panel.hidden = true;
    launcher.hidden = false;
    launcher.setAttribute('aria-expanded', 'false');
    lockScroll(false);
    clearInterval(statusTimer);
    if (state.conv) startSync();
    renderBadge();
    launcher.focus({ preventScroll: true });
  }

  function init() {
    build();
    renderAll();
    loadStatus();
    resume();
    if (INLINE) {
      statusTimer = setInterval(loadStatus, STATUS_REFRESH_MS);
      focusComposer();
    }

    document.addEventListener('click', function (event) {
      var target = event.target && event.target.closest ? event.target.closest('[data-guarde-chat-open], a[href="#guarde-chat"]') : null;
      if (target) { event.preventDefault(); openPanel(); }
    });
    document.addEventListener('visibilitychange', function () {
      if (document.visibilityState === 'visible') restoreTitle();
      if (document.visibilityState === 'visible' && state.conv) {
        connectRealtime();
        sync().then(function () { if (state.open) markRead(); });
      }
    });
    window.addEventListener('hashchange', function () { if (location.hash === '#guarde-chat') openPanel(); });
    if (location.hash === '#guarde-chat') openPanel();
  }

  window.GuardeChat = {
    open: function () { if (host) openPanel(); else document.addEventListener('DOMContentLoaded', openPanel); },
    close: closePanel
  };

  if (document.body) init();
  else document.addEventListener('DOMContentLoaded', init);
})();
