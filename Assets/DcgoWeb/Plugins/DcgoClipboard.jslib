mergeInto(LibraryManager.library, {
  DcgoClipboard_Read: function (goNamePtr, methodPtr) {
    var goName = UTF8ToString(goNamePtr);
    var method = UTF8ToString(methodPtr);
    function send(text) {
      try { SendMessage(goName, method, text || ''); } catch (e) { console.warn('[DcgoClipboard]', e); }
    }
    function fallback() {
      var t = '';
      try { t = window.prompt('Cole o deck code aqui (Ctrl+V):') || ''; } catch (e) {}
      send(t);
    }
    try {
      if (navigator.clipboard && navigator.clipboard.readText) {
        navigator.clipboard.readText().then(function (t) {
          if (t) send(t); else fallback();
        }, function () { fallback(); });
      } else fallback();
    } catch (e) { fallback(); }
  }
});
