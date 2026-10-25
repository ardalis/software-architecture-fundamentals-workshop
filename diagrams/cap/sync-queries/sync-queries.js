// Synchronous queries: A calls B and C directly. When B is cut off, A fails
// rather than return data that might be stale: consistency over availability.

import { capDiagram } from '../cap-kit.js';

export default capDiagram({
  id: 'cap-sync-queries',
  title: 'Synchronous Queries: Consistent, Not Available',
  description:
    'Service A answers a client query by querying Services B and C synchronously and combining the results. ' +
    "When a partition cuts B off, A's query to B times out, so A returns an error instead of possibly stale data.",

  cast: [
    { ids: ['client'], step: 1, caption: 'A client sends queries to Service A. A ? marks a query, and = marks its result.' },
    { ids: ['a', 'b', 'c'], step: 2, caption: 'To answer, A needs data owned by Services B and C.' },
  ],

  happy: [
    { id: 'query', from: 'client', to: 'a', label: '? Get Summary', step: 3, caption: 'First, the happy path.' },
    { id: 'query-b', from: 'a', to: 'b', label: '? Get B Data', step: 4,
      caption: 'A queries B and C directly, and waits for each answer...' },
    { id: 'result-b', from: 'b', to: 'a', label: '= B Data', lane: 1, step: 5 },
    { id: 'query-c', from: 'a', to: 'c', label: '? Get C Data', step: 6 },
    { id: 'result-c', from: 'c', to: 'a', label: '= C Data', lane: 1, step: 7 },
    { id: 'result', from: 'a', to: 'client', label: '= Summary', lane: 1, step: 8,
      caption: '...then combines them. With no partition, the system is available and consistent, at the cost of some network latency.' },
  ],

  reset: { caption: 'Now a router fails between A and B.' },

  partition: { service: 'b', caption: 'A network partition: A and B can no longer talk to each other.' },

  messages: [
    { id: 'query', from: 'client', to: 'a', label: '? Get Summary', step: 4 },
    { id: 'query-b', from: 'a', to: 'b', label: '? Get B Data', cut: true, kind: 'fail', step: 5,
      caption: "A's query to B never arrives, and eventually times out." },
    { id: 'error', from: 'a', to: 'client', label: 'Error', lane: 1, kind: 'fail', step: 6,
      caption: 'So A fails the whole request. A is no longer available...' },
  ],

  callout: {
    lines: ['CONSISTENT,', 'NOT AVAILABLE'],
    step: 7,
    caption: '...but it never returns data that might be out of date. Synchronous calls to dependencies choose consistency.',
  },

  captions: [
    { id: 'wrap-up', step: 8,
      caption: 'Cutting off C instead has the same effect: every dependency A calls directly can take it down.' },
  ],
});
