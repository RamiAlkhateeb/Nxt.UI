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
