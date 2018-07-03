fromStream('$all')
.when({
  $init: function () {
      return {
          'gesamtzahl': 0,
          'offene': 0,
          'geschlossene': 0
      };
  },
  'MeldungAngelegtEvent': function (state, ev) {
    state.gesamtzahl += 1;
    state.offene += 1;

    emit('MeldungsStatusUebersicht', 'StatusUpdated', state);
  },
  'MeldungGeschlossenEvent': function (state, ev) {
    state.geschlossene += 1;
    state.offene -= 1;

    emit('MeldungsStatusUebersicht', 'StatusUpdated', state);
  }
});