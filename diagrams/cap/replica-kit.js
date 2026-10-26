// Shared layout for the CAP theorem and PACELC diagrams: two replicas of the same
// value `x`, each with its own client. Where cap-kit.js shows the theorems at work
// in a microservice system, these show the theorems themselves.
//
// Each replica's current value sits in a yellow box beneath it. A new value is a
// new box drawn over the old one, so the replicas visibly drift apart and converge.
// The CQRS notation matches cap-kit.js: `?` is a query, `=` its result, `!` a command.
// Coordinates use a 1680 x 620 canvas.

import { Diagram } from 'animated-diagrams/model';

const STYLESHEET = new URL('./cap.css', import.meta.url).href;

const BOXES = {
  'client-1': { label: 'Client 1', x: 40, y: 250, width: 220, height: 120, variant: 'client' },
  r1: { label: 'Replica 1', x: 420, y: 240, width: 320, height: 140, variant: 'service' },
  r2: { label: 'Replica 2', x: 980, y: 240, width: 320, height: 140, variant: 'service' },
  'client-2': { label: 'Client 2', x: 1420, y: 250, width: 220, height: 120, variant: 'client' },
};

const PARTITION_X = 860;
const LABEL_ABOVE = 17; // 17px labels sit just above their line
const laneY = (lane) => 290 + 40 * lane;

// The left and right edges each message runs between, keyed by its endpoints.
const SPANS = {
  'client-1 r1': [260, 420],
  'r1 r2': [740, 980],
  'r2 client-2': [1300, 1420],
};

// A replica's value box. Ids are unique per value, so later boxes cover earlier ones.
export const value = (replica, x, step, caption) => ({
  id: `${replica}-x${x}`,
  label: `x = ${x}`,
  x: BOXES[replica].x + 60,
  y: 430,
  width: 200,
  height: 90,
  variant: 'data',
  step,
  caption,
});

// `{ id, from, to, label, lane, async, cut, kind, caption, step }`. `cut` stops the
// arrow at the partition; `kind` is 'fail' (red) or 'stale' (orange).
export function message({ id, from, to, label, lane = 0, async = false, cut = false, kind = '', caption, step }) {
  const key = SPANS[`${from} ${to}`] ? `${from} ${to}` : `${to} ${from}`;
  let [left, right] = SPANS[key];
  const forward = key.startsWith(from);
  if (cut) [left, right] = forward ? [left, PARTITION_X - 20] : [PARTITION_X + 20, right];
  const y = laneY(lane);
  const [x1, x2] = forward ? [left, right] : [right, left];
  return {
    id, from, to, label,
    points: [{ x: x1, y }, { x: x2, y }],
    labelAt: { x: (left + right) / 2, y: y - LABEL_ABOVE },
    dashed: async,
    variant: kind,
    caption,
    step,
  };
}

// The jagged red cut between the replicas.
export function partition(step, caption) {
  const points = [];
  for (let i = 0; i <= 18; i++) points.push({ x: PARTITION_X + (i % 2 ? 14 : -14), y: 200 + i * 20 });
  return {
    id: 'partition', from: 'r1', to: 'r2', label: 'PARTITION',
    points, labelAt: { x: PARTITION_X, y: 585 }, variant: 'partition', step, caption,
  };
}

// Speech bubbles above Client 1 (`side: 'left'`) or Client 2 (`side: 'right'`).
export const callout = ({ id, side, lines, step, caption }) => ({
  id, lines, step, caption,
  ...(side === 'left'
    ? { x: 40, y: 30, width: 320, height: 130, tail: { x: 150, y: 248 } }
    : { x: 1300, y: 30, width: 340, height: 130, tail: { x: 1530, y: 248 } }),
});

// `nodes`: extra nodes (value boxes) after the four boxes, which appear at `castStep`.
export function replicaDiagram({ id, title, description, castStep = 1, castCaption, nodes, edges, callouts, resets }) {
  return new Diagram({
    id,
    title,
    description,
    stylesheet: STYLESHEET,
    width: 1680,
    height: 620,
    watermark: { text: 'NimblePros.com', x: 1300, y: 600, fontSize: 32 },
    nodes: [
      ...Object.entries(BOXES).map(([nodeId, box], i) => ({
        id: nodeId, ...box, radius: 16, step: castStep, caption: i === 0 ? castCaption : undefined,
      })),
      ...nodes.map((n) => ({ radius: 12, ...n })),
    ],
    edges,
    callouts,
    resets,
  });
}
