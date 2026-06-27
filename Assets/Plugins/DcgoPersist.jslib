mergeInto(LibraryManager.library, {
  DcgoSyncFS: function () {
    try {
      if (typeof FS === 'undefined' || !FS.syncfs) return;
      // Nao sobrepoe: se ja ha um syncfs em andamento, pula (evita corromper o IDBFS).
      if (window.__dcgoSyncing) return;
      window.__dcgoSyncing = true;
      FS.syncfs(false, function (err) {
        window.__dcgoSyncing = false;
        if (err) console.warn('[DcgoPersist] syncfs', err);
      });
    } catch (e) {
      try { window.__dcgoSyncing = false; } catch (_) {}
      console.warn('[DcgoPersist] ex', e);
    }
  }
});
