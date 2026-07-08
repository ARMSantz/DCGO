// Plugin WebGL: expõe ao C# os dados de login que a página web injeta em
// window.DCGO_AUTH (ver web/js/app.js). Coloque em Assets/DcgoWeb/Plugins/.
mergeInto(LibraryManager.library, {
  DcgoWeb_GetToken: function () {
    var s = (window.DCGO_AUTH && window.DCGO_AUTH.token) || '';
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },
  DcgoWeb_GetUsername: function () {
    var s = (window.DCGO_AUTH && window.DCGO_AUTH.username) || '';
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },
  DcgoWeb_GetApiBase: function () {
    var s = (window.DCGO_AUTH && window.DCGO_AUTH.apiBase) || '';
    var size = lengthBytesUTF8(s) + 1;
    var buf = _malloc(size);
    stringToUTF8(s, buf, size);
    return buf;
  },
  // Logout: apaga o token do localStorage (compartilhado com a pagina de login,
  // mesma origem) e leva a janela de topo de volta para a tela de login ('/').
  // Funciona tanto no modo iframe (window.top = pagina pai) quanto acessando
  // /game/ direto (window.top = o proprio jogo) — ambos mesma origem.
  DcgoWeb_Logout: function () {
    try { localStorage.removeItem('dcgo_token'); } catch (e) {}
    try {
      var t = window.top || window;
      t.location.href = t.location.origin + '/';
    } catch (e) {
      try { window.location.href = '/'; } catch (e2) {}
    }
  },
});
