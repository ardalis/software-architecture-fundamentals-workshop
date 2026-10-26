// The CAP theorem: two replicas stay in sync until a partition cuts them apart.
// Then a read from the cut-off replica must either fail (consistent, not available)
// or return the old value (available, not consistent).

import { callout, message, partition, replicaDiagram, value } from '../replica-kit.js';

export default replicaDiagram({
  id: 'cap-theorem',
  title: 'CAP: During a Partition, Choose Consistency or Availability',
  description:
    'Two replicas copy each write to each other. When a network partition cuts them apart, a write reaches only ' +
    'Replica 1, and a read from Replica 2 must either fail or return the stale value.',
  castCaption: 'Two replicas hold the same value, x = 1. Each has its own client.',

  nodes: [
    value('r1', 1, 1),
    value('r2', 1, 1),
    value('r1', 2, 2),
    value('r2', 2, 3),
    value('r1', 3, 8),
  ],

  edges: [
    message({ id: 'write-2', from: 'client-1', to: 'r1', label: '! x = 2', step: 2,
      caption: 'Client 1 sets x to 2 on Replica 1.' }),
    message({ id: 'copy-2', from: 'r1', to: 'r2', label: 'Copy x = 2', step: 3,
      caption: 'Replica 1 copies the change to Replica 2.' }),
    message({ id: 'read-1', from: 'client-2', to: 'r2', label: '? x', step: 4,
      caption: 'Client 2 reads x from Replica 2...' }),
    message({ id: 'result-2', from: 'r2', to: 'client-2', label: '= 2', lane: 1, step: 5,
      caption: '...and gets the latest value. Consistent and available.' }),

    partition(7, 'A network partition: the replicas can no longer talk to each other.'),
    message({ id: 'write-3', from: 'client-1', to: 'r1', label: '! x = 3', step: 8,
      caption: 'Client 1 sets x to 3 on Replica 1.' }),
    message({ id: 'copy-3', from: 'r1', to: 'r2', label: 'Copy x = 3', cut: true, kind: 'fail', step: 9,
      caption: "The copy can't reach Replica 2." }),
    message({ id: 'read-2', from: 'client-2', to: 'r2', label: '? x', step: 10,
      caption: 'Client 2 reads x from Replica 2. What should Replica 2 do?' }),
    message({ id: 'cp-answer', from: 'r2', to: 'client-2', label: 'Error', lane: 1, kind: 'fail', step: 11 }),
    message({ id: 'ap-answer', from: 'r2', to: 'client-2', label: '= 2', lane: 1, kind: 'stale', step: 13 }),
  ],

  callouts: [
    callout({ id: 'cp-callout', side: 'right', lines: ['CONSISTENT,', 'NOT AVAILABLE'], step: 11,
      caption: "It can refuse, because it can't confirm it has the latest value. Consistent, but not available (CP)." }),
    callout({ id: 'ap-callout', side: 'right', lines: ['AVAILABLE,', 'NOT CONSISTENT'], step: 13,
      caption: 'It answers with the value it has, which is stale. Available, but not consistent (AP).' }),
  ],

  resets: [
    { id: 'reset', step: 6, caption: 'Now the network between the replicas fails.' },
    { id: 'or', step: 12, only: ['cp-answer', 'cp-callout'], caption: 'Or...' },
  ],
});
