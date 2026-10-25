// Anthology Saga: asynchronous, eventually consistent, choreographed.
// Services react to each other's events; nobody coordinates and nobody waits.

import { choreographed, sagaDiagram } from '../saga-kit.js';

export default sagaDiagram(choreographed, {
  id: 'saga-anthology',
  title: 'Anthology Saga: Asynchronous, Eventual, Choreographed',
  description:
    'Checkout accepts the order right away. Checkout, Inventory, Payments and Ticketing then react to ' +
    "each other's asynchronous events with no coordinator, and Ticketing tells Checkout when the tickets are issued " +
    'so they can be emailed to the fan. On replay, Ticketing fails; its message is redelivered once it recovers, ' +
    'and nothing upstream is undone.',

  cast: [
    { ids: ['fan'], step: 1, caption: 'A fan buys tickets. Seats must be held, the card charged, and tickets issued.' },
    {
      ids: ['checkout', 'inventory', 'payments', 'ticketing'],
      step: 2,
      caption: 'There is no orchestrator. Each service reacts to events and publishes its own.',
    },
  ],

  happy: [
    { id: 'buy', from: 'fan', to: 'checkout', label: 'Buy Tickets', step: 3,
      caption: 'First, the happy path.' },
    { id: 'received', from: 'checkout', to: 'fan', label: 'Order Received', lane: 1, step: 4,
      caption: 'Checkout accepts the order and answers the fan right away.' },
    { id: 'placed', from: 'checkout', to: 'inventory', label: 'Order Placed', async: true, step: 5,
      caption: 'Events flow from service to service. No one coordinates, and no one waits.' },
    { id: 'held', from: 'inventory', to: 'payments', label: 'Seats Held', async: true, step: 6 },
    { id: 'paid', from: 'payments', to: 'ticketing', label: 'Payment Received', async: true, step: 7 },
    { id: 'issued', from: 'ticketing', to: 'checkout', label: 'Tickets Issued', async: true, step: 8,
      caption: 'Ticketing announces the tickets when they are ready...' },
    { id: 'emailed', from: 'checkout', to: 'fan', label: 'Tickets Emailed', lane: 2, async: true, step: 9,
      caption: '...and Checkout emails them to the fan.' },
  ],

  reset: { caption: 'Now replay the same purchase, but this time Ticketing fails.' },

  messages: [
    { id: 'buy', from: 'fan', to: 'checkout', label: 'Buy Tickets', step: 3 },
    { id: 'received', from: 'checkout', to: 'fan', label: 'Order Received', lane: 1, step: 4 },
    { id: 'placed', from: 'checkout', to: 'inventory', label: 'Order Placed', async: true, step: 5 },
    { id: 'held', from: 'inventory', to: 'payments', label: 'Seats Held', async: true, step: 6 },
    { id: 'paid', from: 'payments', to: 'ticketing', label: 'Payment Received', async: true, kind: 'fail', step: 7,
      caption: 'Ticketing fails while handling this message. Nothing upstream is undone.' },
    { id: 'redelivered', from: 'payments', to: 'ticketing', label: 'Redelivered', lane: 1, async: true, step: 8,
      caption: 'The message stays in its queue and is redelivered once Ticketing recovers.' },
    { id: 'issued', from: 'ticketing', to: 'checkout', label: 'Tickets Issued', async: true, step: 9 },
    { id: 'emailed', from: 'checkout', to: 'fan', label: 'Tickets Emailed', lane: 2, async: true, step: 10,
      caption: 'The fan already knew the order was in. The tickets just arrive a little later.' },
  ],

  callout: {
    lines: ['LOOSE AND', 'FAST'],
    step: 11,
    caption: 'Anthology Saga: asynchronous, eventually consistent, choreographed. It has the lowest coupling and the highest scalability of the eight.',
  },

  captions: [
    { id: 'wrap-up', step: 12,
      caption: 'The cost: no one owns the workflow, so complex flows and their errors are hard to follow and debug.' },
  ],
});
