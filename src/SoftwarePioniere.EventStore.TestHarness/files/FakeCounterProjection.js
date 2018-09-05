fromCategory('SoftwarePionierTests_Fake')
    .partitionBy(function (evnt) {
        return evnt.body.AggregateId.replace(/-/g, '');
    })
    .when({
        $init: function () {
            return {
                ids: []
            }
        },
        FakeEvent: function (state, evnt) {
            state.ids.push(evnt.body.Id);
        },
        FakeEvent2: function (state, evnt) {
            state.ids = state.ids.filter((x) => (x !== evnt.body.Id));
        }
    }).outputState()