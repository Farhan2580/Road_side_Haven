mergeInto(LibraryManager.library, {
  RoadsideTouchStatus: function(mode, modal, speed) {
    if (typeof window !== 'undefined' && window['roadsideTouchStatus']) window['roadsideTouchStatus'](mode, modal, speed);
  }
});
