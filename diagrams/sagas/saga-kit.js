// Shared layouts for the eight saga pattern diagrams from *Software Architecture:
// The Hard Parts*. Every saga tells the same TicketTide story: a fan buys tickets,
// so Inventory must hold seats, Payments must charge the card, and Ticketing must
// issue the tickets.
//
// A saga file lists who appears (`cast`) and the `messages` between them; the
// layout works out where each arrow goes from its endpoints and `lane`.
// Solid arrows are synchronous calls, dashed arrows (`async: true`) are messages.
// `kind: 'fail'` draws a failure in red; `kind: 'compensate'` draws an undo in orange.

import { Diagram } from 'animated-diagrams/model';

const STYLESHEET = new URL('./sagas.css', import.meta.url).href;
const WATERMARK = 'NimblePros.com';

// Orchestrated: the fan on the left, a tall Checkout Orchestrator in the middle,
// and one row per service on the right. Every message is a straight horizontal line.
// Coordinates use a 1660 x 840 canvas.
const service = (label, y) => ({ label, x: 1300, y, width: 320, height: 180, variant: 'service' });

export const orchestrated = {
  width: 1660,
  height: 840,
  watermark: { text: WATERMARK, x: 1010, y: 805, fontSize: 32 },
  callout: { x: 40, y: 110, width: 240, height: 120, tail: { x: 130, y: 345 } },
  boxes: {
    fan: { label: 'Fan', x: 40, y: 350, width: 200, height: 120, variant: 'fan' },
    orchestrator: { label: 'Checkout\nOrchestrator', x: 440, y: 60, width: 280, height: 700, variant: 'orchestrator' },
    inventory: service('Inventory', 60),
    payments: service('Payments', 320),
    ticketing: service('Ticketing', 580),
  },

  // Fan lanes are 30px apart; each service row has four lanes, 40px apart.
  route(from, to, lane) {
    if (from === 'fan' || to === 'fan') {
      const y = 380 + 30 * lane;
      const [a, b] = from === 'fan' ? [240, 440] : [440, 240];
      return { points: [{ x: a, y }, { x: b, y }], labelAt: { x: 340, y } };
    }
    const target = from === 'orchestrator' ? to : from;
    const y = this.boxes[target].y + 30 + 40 * lane;
    const [a, b] = from === 'orchestrator' ? [720, 1300] : [1300, 720];
    return { points: [{ x: a, y }, { x: b, y }], labelAt: { x: 1010, y } };
  },
};

// Choreographed: no orchestrator. The fan talks to Checkout, and the services
// form a staircase, each one handing off to the next. Coordinates use a 1920 x 840 canvas.
const CHAIN = ['checkout', 'inventory', 'payments', 'ticketing'];
const STEP_W = 260;
const STEP_H = 120;
const colX = (i) => 480 + 380 * i;
const rowY = (i) => 80 + 200 * i;
const step = (i, label) => ({ label, x: colX(i), y: rowY(i), width: STEP_W, height: STEP_H, variant: 'service' });

export const choreographed = {
  width: 1920,
  height: 840,
  watermark: { text: WATERMARK, x: 270, y: 600, fontSize: 40 },
  callout: { x: 1480, y: 230, width: 300, height: 120, tail: { x: 1420, y: 475 } },
  boxes: {
    fan: { label: 'Fan', x: 40, y: 80, width: 200, height: STEP_H, variant: 'fan' },
    checkout: step(0, 'Checkout'),
    inventory: step(1, 'Inventory'),
    payments: step(2, 'Payments'),
    ticketing: step(3, 'Ticketing'),
  },

  // Next service: out of the right side, across, then down onto its top.
  // Previous service: out of the left side, across, then up into its bottom.
  // Further back: a lane along the bottom of the sender (`lane` is ignored).
  // Nested lanes turn in order (lower start, earlier turn), so no lines cross.
  route(from, to, lane) {
    if (from === 'fan' || to === 'fan') {
      const y = 108 + 32 * lane;
      const [a, b] = from === 'fan' ? [240, 480] : [480, 240];
      return { points: [{ x: a, y }, { x: b, y }], labelAt: { x: 360, y } };
    }
    const i = CHAIN.indexOf(from);
    const j = CHAIN.indexOf(to);
    if (j === i + 1) {
      const y = rowY(i) + 30 + 32 * lane;
      const turn = colX(j) + 170 - 60 * lane;
      return {
        points: [{ x: colX(i) + STEP_W, y }, { x: turn, y }, { x: turn, y: rowY(j) }],
        labelAt: { x: colX(i) + STEP_W + 130, y },
      };
    }
    if (j === i - 1) {
      const y = rowY(i) + 30 + 32 * lane;
      const turn = colX(j) + 170 - 60 * lane;
      return {
        points: [{ x: colX(i), y }, { x: turn, y }, { x: turn, y: rowY(j) + STEP_H }],
        labelAt: { x: colX(i) - 110, y },
      };
    }
    if (j < i) {
      const y = rowY(i) + 100;
      const turn = colX(j) + 50;
      return {
        points: [{ x: colX(i), y }, { x: turn, y }, { x: turn, y: rowY(j) + STEP_H }],
        labelAt: { x: colX(i) - 300, y },
      };
    }
    throw new Error(`No route from "${from}" to "${to}"`);
  },
};

// `cast`: [{ ids, step, caption }] — boxes that appear together.
// `happy`: messages for the happy path, played first (ids get a `happy-` prefix).
// `reset`: { caption } — clears the happy path's arrows before the failure case.
// `messages`: [{ id, from, to, label, lane, async, kind, caption, step }] — the failure case.
// `callout`: { lines, caption, step } — drawn at the layout's callout spot.
// `captions`: caption-only steps.
// Failure-case steps are numbered as if the happy path weren't there (the first
// message after the cast is step 3); they're shifted to play after the reset.
export function sagaDiagram(layout, {
  id, title, description, cast, happy = [], reset, messages, callout, captions = [],
}) {
  const nodes = cast.flatMap(({ ids, step, caption }) =>
    ids.map((nodeId, i) => ({
      id: nodeId,
      ...layout.boxes[nodeId],
      radius: 16,
      step,
      caption: i === 0 ? caption : undefined,
    })),
  );

  const resetStep = happy.length ? Math.max(...happy.map((m) => m.step)) + 1 : null;
  const shift = resetStep === null ? 0 : resetStep - 2;
  const later = (el) => ({ ...el, step: el.step + shift });

  const toEdge = ({ id: edgeId, from, to, label, lane = 0, async = false, kind = '', caption, step }) => ({
    id: edgeId,
    from,
    to,
    label,
    ...layout.route(from, to, lane),
    dashed: async,
    variant: kind,
    caption,
    step,
  });

  const edges = [
    ...happy.map((m) => toEdge({ ...m, id: `happy-${m.id}` })),
    ...messages.map((m) => toEdge(later(m))),
  ];

  return new Diagram({
    id,
    title,
    description: happy.length
      ? `The purchase succeeds first; then it is replayed with a failure. ${description}`
      : description,
    stylesheet: STYLESHEET,
    width: layout.width,
    height: layout.height,
    watermark: layout.watermark,
    nodes,
    edges,
    callouts: callout ? [{ id: 'callout', ...layout.callout, ...later(callout) }] : [],
    captions: captions.map(later),
    resets: resetStep === null ? [] : [{ id: 'reset', caption: reset?.caption, step: resetStep }],
  });
}
