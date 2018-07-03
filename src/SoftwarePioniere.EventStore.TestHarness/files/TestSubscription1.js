fromStream('$all')
.when({
  $init: function () {
      return {
          'gesamtzahl': 1,
          'offene': 1,
          'geschlossene': 1
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