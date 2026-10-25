// Shared layout for the CAP / PACELC diagrams, based on the post
// "CAP Theorem, PACELC, and Microservices" (ardalis.com/cap-pacelc-and-microservices/).
//
// A client calls Service A, which depends on Services B and C. The post's CQRS
// notation is kept: `?` is a query, `=` its result, `!` a command (which only
// returns a status like OK).
//
// Each diagram plays the happy path, resets, then cuts the network: a red zigzag
// partition line is drawn between A's side and B or C. A message with `cut: true`
// stops at that line. `restore` fades the partition (and any listed edges) so the
// story can show the network coming back.
//
// Solid arrows are synchronous calls, dashed arrows (`async: true`) are messages
// or events. `kind: 'fail'` is red; `kind: 'stale'` (a stale answer) is orange.
// Coordinates use a 1660 x 840 canvas.

import { Diagram } from 'animated-diagrams/model';

const STYLESHEET = new URL('./cap.css', import.meta.url).href;
const WATERMARK = 'NimblePros.com';

const ROW = { b: 60, c: 520 }; // top of each service's row; rows are 240 tall
const ROW_H = 240;
const A_RIGHT = 720;
const SERVICE_X = 1300;
const QUEUE = { x: 860, width: 200 };

const service = (label, row) => ({ label, x: SERVICE_X, y: row, width: 320, height: ROW_H, variant: 'service' });
const queue = (label, row) => ({
  label, x: QUEUE.x, y: row + 60, width: QUEUE.width, height: 120, variant: 'queue',
});

export const BOXES = {
  client: { label: 'Client', x: 40, y: 350, width: 200, height: 120, variant: 'client' },
  a: { label: 'Service A', x: 440, y: 60, width: 280, height: 700, variant: 'service' },
  b: service('Service B', ROW.b),
  c: service('Service C', ROW.c),
  cache: { label: 'Cache', x: 470, y: 620, width: 220, height: 100, variant: 'cache' },
  'queue-b': queue('Queue for B', ROW.b),
  'queue-c': queue('Queue for C', ROW.c),
};

const rowOf = (id) => (id.endsWith('b') ? ROW.b : ROW.c);
const line = (x1, x2, y, labelX) => ({ points: [{ x: x1, y }, { x: x2, y }], labelAt: { x: labelX, y } });

// The partition's x position: between A and the service, or between a queue and its service.
const partitionX = (queued) => (queued ? 1200 : 1000);

// Works out an arrow from its endpoints. Lanes are 30px apart at the client,
// 40px apart in a service row. `cut` stops the arrow at the partition line.
function route({ from, to, lane = 0, cut = false }, queued) {
  const px = partitionX(queued);
  if (from === 'client' || to === 'client') {
    const y = 380 + 30 * lane;
    return from === 'client' ? line(240, 440, y, 340) : line(440, 240, y, 340);
  }
  if (from === 'a' && to.startsWith('queue-')) {
    return line(A_RIGHT, QUEUE.x, rowOf(to) + 100 + 40 * lane, 790);
  }
  if (from.startsWith('queue-')) {
    const y = rowOf(from) + 100 + 40 * lane;
    return cut
      ? line(QUEUE.x + QUEUE.width, px - 20, y, (QUEUE.x + QUEUE.width + px - 20) / 2)
      : line(QUEUE.x + QUEUE.width, SERVICE_X, y, 1130);
  }
  const y = rowOf(from === 'a' ? to : from) + 40 + 40 * lane;
  if (from === 'a') return cut ? line(A_RIGHT, px - 20, y, (A_RIGHT + px - 20) / 2) : line(A_RIGHT, SERVICE_X, y, 930);
  return cut ? line(SERVICE_X, px + 20, y, (SERVICE_X + px + 20) / 2) : line(SERVICE_X, A_RIGHT, y, 930);
}

// A jagged vertical line across a service's row: the "unplugged cable".
function partitionEdge({ id = 'partition', service: target, queued, caption, step }) {
  const x = partitionX(queued);
  const top = rowOf(target) - 10;
  const points = [];
  for (let i = 0; i <= 11; i++) points.push({ x: x + (i % 2 ? 14 : -14), y: top + i * 20 });
  return {
    id,
    from: queued ? `queue-${target}` : 'a',
    to: target,
    label: 'PARTITION',
    points,
    labelAt: { x, y: top + 245 },
    variant: 'partition',
    caption,
    step,
  };
}

// `cast`: [{ ids, step, caption }] — boxes that appear together.
// `happy`: the happy path's messages (ids get a `happy-` prefix), played first.
// `reset`: { caption } — clears the happy path's arrows.
// `partition`: { service: 'b' | 'c', caption } — the failure case's first step.
// `messages`: [{ id, from, to, label, lane, async, cut, kind, caption, step }] — the failure case.
// `restore`: { step, caption, edges } — fades the partition, the callout and `edges`: the network is back.
// `callout`, `captions`: as in the other diagrams.
// Failure-case steps are numbered as if the happy path weren't there: the
// partition is step 3. They're shifted to play after the reset.
export function capDiagram({
  id, title, description, queued = false, cast, happy, reset, partition, messages,
  restore, callout, captions = [],
}) {
  const nodes = cast.flatMap(({ ids, step, caption }) =>
    ids.map((nodeId, i) => ({ id: nodeId, ...BOXES[nodeId], radius: 16, step, caption: i === 0 ? caption : undefined })),
  );

  const resetStep = Math.max(...happy.map((m) => m.step)) + 1;
  const shift = resetStep - 2;
  const later = (el) => ({ ...el, step: el.step + shift });

  const toEdge = (m) => ({
    id: m.id,
    from: m.from,
    to: m.to,
    label: m.label,
    ...route(m, queued),
    dashed: m.async ?? false,
    variant: m.kind ?? '',
    caption: m.caption,
    step: m.step,
  });

  const edges = [
    ...happy.map((m) => toEdge({ ...m, id: `happy-${m.id}` })),
    later(partitionEdge({ ...partition, queued, step: 3 })),
    ...messages.map((m) => toEdge(later(m))),
  ];

  const resets = [{ id: 'reset', caption: reset.caption, step: resetStep }];
  if (restore) {
    resets.push({
      id: 'restore',
      caption: restore.caption,
      step: restore.step + shift,
      only: ['partition', ...(callout ? ['callout'] : []), ...(restore.edges ?? [])],
    });
  }

  return new Diagram({
    id,
    title,
    description: `The happy path plays first; then the network is partitioned and it plays again. ${description}`,
    stylesheet: STYLESHEET,
    width: 1660,
    height: 840,
    watermark: { text: WATERMARK, x: 1010, y: 815, fontSize: 32 },
    nodes,
    edges,
    callouts: callout
      ? [{ id: 'callout', x: 30, y: 100, width: 300, height: 130, tail: { x: 130, y: 345 }, ...later(callout) }]
      : [],
    captions: captions.map(later),
    resets,
  });
}
