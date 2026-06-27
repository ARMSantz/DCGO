// Ponte WebGL: expõe ao C# o login. Lê primeiro dos parâmetros da URL do iframe
// (?dcgo_token=...&dcgo_user=...&dcgo_api=...), com fallback para window.DCGO_AUTH
// (caso o jogo rode na mesma página, sem iframe).
mergeInto(LibraryManager.library, {
  DcgoWeb_GetToken: function () {
    var s = '';
    try { s = new URLSearchParams(window.location.search).get('dcgo_token') || ''; } catch (e) {}
    if (!s) { try { s = (window.DCGO_AUTH && window.DCGO_AUTH.token) || ''; } catch (e) {} }
    var size = lengthBytesUTF8(s) + 1; var buf = _malloc(size); stringToUTF8(s, buf, size); return buf;
  },
  DcgoWeb_GetUsername: function () {
    var s = '';
    try { s = new URLSearchParams(window.location.search).get('dcgo_user') || ''; } catch (e) {}
    if (!s) { try { s = (window.DCGO_AUTH && window.DCGO_AUTH.username) || ''; } catch (e) {} }
    var size = lengthBytesUTF8(s) + 1; var buf = _malloc(size); stringToUTF8(s, buf, size); return buf;
  },
  DcgoWeb_GetApiBase: function () {
    var s = '';
    try { s = new URLSearchParams(window.location.search).get('dcgo_api') || ''; } catch (e) {}
    if (!s) { try { s = (window.DCGO_AUTH && window.DCGO_AUTH.apiBase) || ''; } catch (e) {} }
    if (!s) { try { s = window.location.origin; } catch (e) {} }
    var size = lengthBytesUTF8(s) + 1; var buf = _malloc(size); stringToUTF8(s, buf, size); return buf;
  },
});
