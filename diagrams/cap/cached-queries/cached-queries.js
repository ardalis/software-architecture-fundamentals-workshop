// Cached queries: B and C publish change events that keep a cache in A up to date.
// When B is cut off, A stays available but serves stale data until the network returns.

import { capDiagram } from '../cap-kit.js';

export default capDiagram({
  id: 'cap-cached-queries',
  title: 'Cached Queries: Available, Eventually Consistent',
  description:
    'Services B and C publish events when their data changes, and Service A keeps those changes in a local cache, ' +
    'answering client queries from it. When a partition cuts B off, A still answers, but with stale data. ' +
    "Once the network is restored, B's event arrives and the cache catches up.",

  cast: [
    { ids: ['client'], step: 1, caption: 'A client sends queries to Service A. A ? marks a query, and = marks its result.' },
    { ids: ['a', 'b', 'c', 'cache'], step: 2,
      caption: 'This time, A keeps a local cache of the data it needs from B and C.' },
  ],

  happy: [
    { id: 'event-b', from: 'b', to: 'a', label: 'B Data Changed', async: true, step: 3,
      caption: 'First, the happy path. When their data changes, B and C publish events...' },
    { id: 'event-c', from: 'c', to: 'a', label: 'C Data Changed', async: true, step: 4,
      caption: "...and A updates its cache." },
    { id: 'query', from: 'client', to: 'a', label: '? Get Summary', step: 5,
      caption: 'Queries are answered from the cache, with no calls to B or C.' },
    { id: 'result', from: 'a', to: 'client', label: '= Summary', lane: 1, step: 6,
      caption: "Even with no partition, the cache can briefly lag behind B and C. That is PACELC's \"else\" trade-off between latency and consistency." },
  ],

  reset: { caption: 'Now a router fails between A and B.' },

  partition: { service: 'b', caption: 'A network partition: A and B can no longer talk to each other.' },

  messages: [
    { id: 'event-b', from: 'b', to: 'a', label: 'B Data Changed', async: true, cut: true, kind: 'fail', step: 4,
      caption: "B's data changes, but its event can't reach A." },
    { id: 'query', from: 'client', to: 'a', label: '? Get Summary', step: 5 },
    { id: 'result', from: 'a', to: 'client', label: '= Stale Summary', lane: 1, kind: 'stale', step: 6,
      caption: 'A still answers from its cache. A is available, but its answer is out of date.' },
    { id: 'event-b-delivered', from: 'b', to: 'a', label: 'B Data Changed', lane: 1, async: true, step: 9,
      caption: "...B's event finally arrives, and the cache catches up. That is eventual consistency." },
  ],

  callout: {
    lines: ['AVAILABLE,', 'NOT CONSISTENT'],
    step: 7,
    caption: 'Caching chooses availability over consistency during a partition.',
  },

  restore: { step: 8, edges: ['event-b'], caption: 'When the network is restored...' },

  captions: [
    { id: 'wrap-up', step: 10,
      caption: 'Caching works for queries. Commands need a different approach.' },
  ],
});
