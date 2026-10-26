// PACELC's "Else": with no partition, a write either waits for every replica to
// confirm (consistent, but slower) or returns at once and copies later (fast, but a
// reader can briefly see the old value).

import { callout, message, replicaDiagram, value } from '../replica-kit.js';

export default replicaDiagram({
  id: 'cap-pacelc',
  title: 'PACELC: Else, Choose Latency or Consistency',
  description:
    'With the network healthy, Replica 1 first waits for Replica 2 to confirm each write before answering, ' +
    'then answers at once and copies the write later, so a read from Replica 2 briefly returns the old value.',
  castCaption: 'No partition this time. Two replicas hold the same value, x = 1.',

  nodes: [
    value('r1', 1, 1),
    value('r2', 1, 1),
    value('r1', 2, 2),
    value('r2', 2, 3),
    value('r1', 3, 7),
    value('r2', 3, 11),
  ],

  edges: [
    message({ id: 'write-2', from: 'client-1', to: 'r1', label: '! x = 2', step: 2,
      caption: 'Client 1 sets x to 2 on Replica 1.' }),
    message({ id: 'copy-2', from: 'r1', to: 'r2', label: 'Copy x = 2', step: 3,
      caption: 'Replica 1 copies the change to Replica 2...' }),
    message({ id: 'ack-2', from: 'r2', to: 'r1', label: 'OK', lane: 1, step: 4,
      caption: '...and waits for Replica 2 to confirm.' }),
    message({ id: 'ok-2', from: 'r1', to: 'client-1', label: 'OK', lane: 1, step: 5 }),

    message({ id: 'write-3', from: 'client-1', to: 'r1', label: '! x = 3', step: 7,
      caption: 'Client 1 sets x to 3 on Replica 1.' }),
    message({ id: 'ok-3', from: 'r1', to: 'client-1', label: 'OK', lane: 1, step: 8,
      caption: 'Replica 1 answers right away. Client 1 waits only for one replica.' }),
    message({ id: 'read', from: 'client-2', to: 'r2', label: '? x', step: 9,
      caption: 'Client 2 reads x from Replica 2 before the copy arrives...' }),
    message({ id: 'stale', from: 'r2', to: 'client-2', label: '= 2', lane: 1, kind: 'stale', step: 10 }),
    message({ id: 'copy-3', from: 'r1', to: 'r2', label: 'Copy x = 3', async: true, step: 11,
      caption: 'The copy arrives in the background, and the replicas converge: eventual consistency.' }),
  ],

  callouts: [
    callout({ id: 'slow', side: 'left', lines: ['CONSISTENT,', 'BUT SLOWER'], step: 5,
      caption: 'Every reader now sees x = 2, but Client 1 waited for a round trip between replicas. Consistency costs latency.' }),
    callout({ id: 'stale-callout', side: 'right', lines: ['FAST,', 'BUT STALE'], step: 10,
      caption: '...and gets the old value. Lower latency costs consistency.' }),
  ],

  resets: [
    { id: 'reset', step: 6, caption: 'Now Replica 1 answers before it copies.' },
  ],
});
