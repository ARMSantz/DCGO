mergeInto(LibraryManager.library, {
  DcgoSyncFS: function () {
    try {
      if (typeof FS !== 'undefined' && FS.syncfs) {
        FS.syncfs(false, function (err) { if (err) console.warn('[DcgoPersist] syncfs', err); });
      }
    } catch (e) { console.warn('[DcgoPersist] ex', e); }
  }
});
