// Horror Story Saga: asynchronous, atomic, choreographed.
// No coordinator and no waiting, yet every step must be undone when one fails.

import { choreographed, sagaDiagram } from '../saga-kit.js';

export default sagaDiagram(choreographed, {
  id: 'saga-horror-story',
  title: 'Horror Story Saga: Asynchronous, Atomic, Choreographed',
  description:
    'Checkout, Inventory, Payments and Ticketing pass asynchronous messages along a chain with no coordinator. ' +
    'When Ticketing fails, it must notify both Payments and Inventory, compensations race each other back, ' +
    'and Checkout must collect them all before telling the fan the purchase failed.',

  cast: [
    { ids: ['fan'], step: 1, caption: 'A fan buys tickets. Seats must be held, the card charged, and tickets issued.' },
    {
      ids: ['checkout', 'inventory', 'payments', 'ticketing'],
      step: 2,
      caption: 'There is no orchestrator. Each service reacts to the messages it receives.',
    },
  ],

  happy: [
    { id: 'buy', from: 'fan', to: 'checkout', label: 'Buy Tickets', step: 3,
      caption: 'First, the happy path.' },
    { id: 'placed', from: 'checkout', to: 'inventory', label: 'Order Placed', async: true, step: 4,
      caption: 'Services send asynchronous messages. No one coordinates, and no one waits.' },
    { id: 'held', from: 'inventory', to: 'payments', label: 'Seats Held', async: true, step: 5 },
    { id: 'paid', from: 'payments', to: 'ticketing', label: 'Payment Received', async: true, step: 6 },
    { id: 'issued', from: 'ticketing', to: 'checkout', label: 'Tickets Issued', async: true, step: 7,
      caption: 'Ticketing tells Checkout the tickets are ready...' },
    { id: 'confirmed', from: 'checkout', to: 'fan', label: 'Tickets Confirmed', lane: 1, step: 8,
      caption: '...and Checkout confirms the purchase with the waiting fan.' },
  ],

  reset: { caption: 'Now replay the same purchase, but this time Ticketing fails.' },

  messages: [
    { id: 'buy', from: 'fan', to: 'checkout', label: 'Buy Tickets', step: 3 },
    { id: 'placed', from: 'checkout', to: 'inventory', label: 'Order Placed', async: true, step: 4 },
    { id: 'held', from: 'inventory', to: 'payments', label: 'Seats Held', async: true, step: 5 },
    { id: 'paid', from: 'payments', to: 'ticketing', label: 'Payment Received', async: true, step: 6 },
    { id: 'failed', from: 'ticketing', to: 'payments', label: 'Issue Failed', async: true, kind: 'fail', step: 7,
      caption: 'Ticketing fails. The saga is atomic, so every completed step must be undone...' },
    { id: 'failed-inventory', from: 'ticketing', to: 'inventory', label: 'Issue Failed', async: true, kind: 'fail', step: 8,
      caption: '...but who tells Inventory? Ticketing must know about every service upstream.' },
    { id: 'refunded', from: 'payments', to: 'inventory', label: 'Card Refunded', async: true, kind: 'compensate', step: 9,
      caption: 'Messages race each other. Inventory may hear about the refund before it hears about the failure.' },
    { id: 'released', from: 'inventory', to: 'checkout', label: 'Seats Released', async: true, kind: 'compensate', step: 10,
      caption: 'Checkout must collect every compensation before it can tell the fan anything.' },
    { id: 'purchase-failed', from: 'checkout', to: 'fan', label: 'Purchase Failed', lane: 1, kind: 'fail', step: 11 },
  ],

  callout: {
    lines: ['HORROR', 'STORY!'],
    step: 12,
    caption: 'Horror Story Saga: asynchronous, atomic, choreographed. It pairs the loosest coordination with the strictest consistency.',
  },

  captions: [
    { id: 'wrap-up', step: 13,
      caption: 'If you end up here, relax atomicity (Anthology Saga) or add an orchestrator (Fantasy Fiction Saga).' },
  ],
});
