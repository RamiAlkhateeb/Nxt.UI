// Nxt.UI runtime helpers shared by every app in the family.
// Loaded as a classic script (<script src="_content/Nxt.UI/nxt.js"></script>) in <head>,
// so the stored theme is applied before first paint.
(function () {
  const THEME_KEY = "nxt.theme";
  const VERSION_KEY = "nxt.appVersion";
  const media = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;

  function safeGet(key) {
    try { return localStorage.getItem(key); } catch { return null; }
  }
  function safeSet(key, value) {
    try { localStorage.setItem(key, value); } catch { /* private mode */ }
  }

  function resolveTheme(mode) {
    if (mode === "light" || mode === "dark") return mode;
    return media && media.matches ? "dark" : "light";
  }

  function applyTheme(mode) {
    mode = mode || safeGet(THEME_KEY) || "system";
    const resolved = resolveTheme(mode);
    const root = document.documentElement;
    root.dataset.theme = resolved;
    root.dataset.bsTheme = resolved;   // Bootstrap 5.3 components follow along where an app still uses them
    root.dataset.themeMode = mode;
    root.style.colorScheme = resolved;
    // Keep the browser chrome (status bar, tab colour) in step with the page.
    const page = getComputedStyle(root).getPropertyValue("--nxt-page").trim();
    if (page) {
      document.querySelectorAll('meta[name="theme-color"]').forEach(m => m.setAttribute("content", page));
    }
  }

  if (media) {
    media.addEventListener("change", () => {
      if ((safeGet(THEME_KEY) || "system") === "system") applyTheme("system");
    });
  }

  async function fetchVersion() {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 2500);
    try {
      const response = await fetch(`version.json?t=${Date.now()}`, { cache: "no-store", signal: controller.signal });
      if (!response.ok) return null;
      const payload = await response.json();
      return payload && typeof payload.version === "string" && payload.version ? payload.version : null;
    } catch {
      return null;
    } finally {
      clearTimeout(timeout);
    }
  }

  async function updateServiceWorker() {
    if (!("serviceWorker" in navigator)) return;
    try {
      const registrations = await navigator.serviceWorker.getRegistrations();
      await Promise.all(registrations.map(r => r.update().catch(() => {})));
      registrations.forEach(r => r.waiting && r.waiting.postMessage({ type: "SKIP_WAITING" }));
    } catch { /* best effort */ }
  }

  let speech = null;
  const timers = new Map();
  const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;

  window.nxt = {
    applyTheme,

    scrollToId(id) {
      const el = document.getElementById(id);
      if (el) el.scrollIntoView({ behavior: "smooth", block: "end" });
    },

    getVersion: fetchVersion,

    /** Returns the new version string when this browser last ran an older one, otherwise null. */
    async checkVersion() {
      const version = await fetchVersion();
      if (!version) return null;
      const stored = safeGet(VERSION_KEY);
      if (!stored) { safeSet(VERSION_KEY, version); return null; }
      return stored === version ? null : version;
    },

    async applyUpdate(version) {
      if (version) safeSet(VERSION_KEY, version);
      await updateServiceWorker();
      window.location.reload();
    },

    /** Web Share API with clipboard fallback. Returns "shared" | "copied" | "cancelled" | "failed". */
    async share(title, text, url) {
      try {
        if (navigator.share) { await navigator.share({ title, text, url }); return "shared"; }
      } catch (e) {
        if (e && e.name === "AbortError") return "cancelled";
      }
      try { await navigator.clipboard.writeText(url || text); return "copied"; } catch { return "failed"; }
    },

    async copy(text) {
      try { await navigator.clipboard.writeText(text); return true; } catch { return false; }
    },

    isIos() {
      return /iphone|ipad|ipod/i.test(navigator.userAgent) ||
        (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
    },

    isStandalone() {
      return window.matchMedia("(display-mode: standalone)").matches || navigator.standalone === true;
    },

    /** True only the first time it is called for this key on this device. */
    claimOnce(key) {
      if (safeGet(key)) return false;
      safeSet(key, "1");
      return true;
    },

    /** The browser's preferred language tag, e.g. "ar-SA". */
    browserLanguage() {
      return navigator.language || "";
    },

    /** Saves text as a file. On iOS the share sheet is used when it can take files (e.g. .ics → Calendar). */
    async download(filename, mime, text) {
      const blob = new Blob([text], { type: mime });
      try {
        const file = new File([blob], filename, { type: mime });
        if (window.nxt.isIos() && navigator.canShare && navigator.canShare({ files: [file] })) {
          await navigator.share({ files: [file] });
          return "shared";
        }
      } catch (e) {
        if (e && e.name === "AbortError") return "cancelled";
      }
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = filename;
      document.body.appendChild(a);
      a.click();
      a.remove();
      setTimeout(() => URL.revokeObjectURL(url), 10000);
      return "downloaded";
    },

    /**
     * Local notifications. They only fire while the page is alive (open, or recently backgrounded):
     * there is no way to schedule one for a closed web app without a push server.
     */
    notify: {
      supported: () => "Notification" in window,
      permission: () => ("Notification" in window ? Notification.permission : "unsupported"),
      async request() {
        if (!("Notification" in window)) return "unsupported";
        try { return await Notification.requestPermission(); } catch { return Notification.permission; }
      },
      async show(title, body, tag, url) {
        if (!("Notification" in window) || Notification.permission !== "granted") return false;
        const options = { body, tag: tag || undefined, renotify: !!tag, data: { url: url || "./" }, icon: "icon-192.png", badge: "favicon.png" };
        try {
          const reg = "serviceWorker" in navigator ? await navigator.serviceWorker.getRegistration() : null;
          if (reg) { await reg.showNotification(title, options); return true; }
          new Notification(title, options);
          return true;
        } catch { return false; }
      },
      /** Calls dotNetRef.OnNotificationDue(id) and shows the notification at atMs (epoch ms). Replaces a timer with the same id. */
      schedule(id, atMs, title, body, tag, url, dotNetRef) {
        this.cancel(id);
        const fire = () => {
          timers.delete(id);
          this.show(title, body, tag, url);
          if (dotNetRef) dotNetRef.invokeMethodAsync("OnNotificationDue", id).catch(() => {});
        };
        const delay = atMs - Date.now();
        if (delay <= 0) { fire(); return; }
        // Browsers clamp long timeouts and throttle background tabs, so check again every minute.
        const tick = () => {
          const left = atMs - Date.now();
          if (left <= 0) fire();
          else timers.set(id, setTimeout(tick, Math.min(left, 60000)));
        };
        timers.set(id, setTimeout(tick, Math.min(delay, 60000)));
      },
      cancel(id) {
        const t = timers.get(id);
        if (t) { clearTimeout(t); timers.delete(id); }
      },
      cancelAll() {
        timers.forEach(t => clearTimeout(t));
        timers.clear();
      }
    },

    /** A short two-tone chime (Web Audio, no file). */
    chime() {
      try {
        const Ctx = window.AudioContext || window.webkitAudioContext;
        const ctx = new Ctx();
        [[880, 0], [660, 0.25]].forEach(([freq, at]) => {
          const osc = ctx.createOscillator();
          const gain = ctx.createGain();
          osc.frequency.value = freq;
          gain.gain.setValueAtTime(0.0001, ctx.currentTime + at);
          gain.gain.exponentialRampToValueAtTime(0.3, ctx.currentTime + at + 0.02);
          gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + at + 0.4);
          osc.connect(gain).connect(ctx.destination);
          osc.start(ctx.currentTime + at);
          osc.stop(ctx.currentTime + at + 0.45);
        });
        setTimeout(() => ctx.close(), 1200);
      } catch { /* no audio */ }
    },

    /** Subscribes dotNetRef.OnVisible() to the page becoming visible again. Returns nothing. */
    onVisible(dotNetRef) {
      document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") dotNetRef.invokeMethodAsync("OnVisible").catch(() => {});
      });
    },

    speech: {
      isSupported: () => !!Recognition,
      start(dotNetRef, lang) {
        if (!Recognition) return false;
        this.stop();
        speech = new Recognition();
        speech.lang = lang || navigator.language || "en-US";
        speech.continuous = true;
        speech.interimResults = true;
        speech.onresult = (event) => {
          let transcript = "";
          for (let i = 0; i < event.results.length; i++) transcript += event.results[i][0].transcript;
          dotNetRef.invokeMethodAsync("OnSpeechResult", transcript);
        };
        speech.onerror = (event) => dotNetRef.invokeMethodAsync("OnSpeechError", event.error || "unknown");
        speech.onend = () => { speech = null; dotNetRef.invokeMethodAsync("OnSpeechEnded"); };
        try { speech.start(); return true; } catch { speech = null; return false; }
      },
      stop() {
        if (speech) { try { speech.stop(); } catch { /* already stopped */ } }
      }
    }
  };

  applyTheme();
})();
