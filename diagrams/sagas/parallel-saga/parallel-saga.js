// Parallel Saga: asynchronous, eventually consistent, orchestrated.
// The orchestrator answers the fan at once, sends messages in parallel, and retries failures.

import { orchestrated, sagaDiagram } from '../saga-kit.js';

export default sagaDiagram(orchestrated, {
  id: 'saga-parallel',
  title: 'Parallel Saga: Asynchronous, Eventual, Orchestrated',
  description:
    'A Checkout Orchestrator accepts the order right away, then sends asynchronous messages to Inventory, ' +
    'Payments and Ticketing in parallel. When Ticketing fails, the orchestrator retries instead of rolling back, ' +
    'and emails the tickets to the fan once they are issued.',

  cast: [
    { ids: ['fan'], step: 1, caption: 'A fan buys tickets. Seats must be held, the card charged, and tickets issued.' },
    {
      ids: ['orchestrator', 'inventory', 'payments', 'ticketing'],
      step: 2,
      caption: 'A Checkout Orchestrator coordinates the three services.',
    },
  ],

  happy: [
    { id: 'buy', from: 'fan', to: 'orchestrator', label: 'Buy Tickets', step: 3,
      caption: 'First, the happy path.' },
    { id: 'received', from: 'orchestrator', to: 'fan', label: 'Order Received', lane: 1, step: 4,
      caption: 'The orchestrator accepts the order and answers the fan right away.' },
    { id: 'hold', from: 'orchestrator', to: 'inventory', label: 'Hold Seats', async: true, step: 5,
      caption: 'Then it sends messages to all three services in parallel.' },
    { id: 'charge', from: 'orchestrator', to: 'payments', label: 'Charge Card', async: true, step: 5 },
    { id: 'issue', from: 'orchestrator', to: 'ticketing', label: 'Issue Tickets', async: true, step: 5 },
    { id: 'held', from: 'inventory', to: 'orchestrator', label: 'Seats Held', lane: 1, async: true, step: 6,
      caption: 'Each service works at its own pace and reports back.' },
    { id: 'charged', from: 'payments', to: 'orchestrator', label: 'Charged', lane: 1, async: true, step: 6 },
    { id: 'issued', from: 'ticketing', to: 'orchestrator', label: 'Tickets Issued', lane: 1, async: true, step: 6 },
    { id: 'emailed', from: 'orchestrator', to: 'fan', label: 'Tickets Emailed', lane: 2, async: true, step: 7,
      caption: 'When all three are done, the fan gets the tickets by email.' },
  ],

  reset: { caption: 'Now replay the same purchase, but this time Ticketing fails.' },

  messages: [
    { id: 'buy', from: 'fan', to: 'orchestrator', label: 'Buy Tickets', step: 3 },
    { id: 'received', from: 'orchestrator', to: 'fan', label: 'Order Received', lane: 1, step: 4 },
    { id: 'hold', from: 'orchestrator', to: 'inventory', label: 'Hold Seats', async: true, step: 5 },
    { id: 'charge', from: 'orchestrator', to: 'payments', label: 'Charge Card', async: true, step: 5 },
    { id: 'issue', from: 'orchestrator', to: 'ticketing', label: 'Issue Tickets', async: true, step: 5 },
    { id: 'held', from: 'inventory', to: 'orchestrator', label: 'Seats Held', lane: 1, async: true, step: 6,
      caption: 'Inventory and Payments report back...' },
    { id: 'charged', from: 'payments', to: 'orchestrator', label: 'Charged', lane: 1, async: true, step: 6 },
    { id: 'failed', from: 'ticketing', to: 'orchestrator', label: 'Failed', lane: 1, async: true, kind: 'fail', step: 7,
      caption: '...but Ticketing fails.' },
    { id: 'retry', from: 'orchestrator', to: 'ticketing', label: 'Retry Issue Tickets', lane: 2, async: true, step: 8,
      caption: 'Nothing is rolled back. The orchestrator owns the error, so it retries.' },
    { id: 'issued', from: 'ticketing', to: 'orchestrator', label: 'Tickets Issued', lane: 3, async: true, step: 9,
      caption: 'Ticketing recovers, and the saga is complete.' },
    { id: 'emailed', from: 'orchestrator', to: 'fan', label: 'Tickets Emailed', lane: 2, async: true, step: 10,
      caption: 'The fan gets the tickets by email. Until then, they saw the order as pending.' },
  ],

  callout: {
    lines: ['FULL SPEED', 'AHEAD!'],
    step: 11,
    caption: 'Parallel Saga: asynchronous, eventually consistent, orchestrated. You get high throughput and still have one owner for errors.',
  },

  captions: [
    { id: 'wrap-up', step: 12,
      caption: 'The costs: the orchestrator must track saga state, and users see in-between states like "pending".' },
  ],
});
