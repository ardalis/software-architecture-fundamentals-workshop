// Commands and queues: A writes commands to local persistent queues that B and C
// read over the network. When C is cut off, A stays available and C catches up later.

import { capDiagram } from '../cap-kit.js';

export default capDiagram({
  id: 'cap-queued-commands',
  title: 'Commands and Queues: Available, Eventually Consistent',
  description:
    'Service A handles a client command by writing commands to local persistent queues for Services B and C, ' +
    'and answers OK right away. When a partition cuts C off from its queue, A and the client never notice; ' +
    "C's commands wait in the queue and are processed once the network is restored.",
  queued: true,

  cast: [
    { ids: ['client'], step: 1,
      caption: 'A client sends commands to Service A. A ! marks a command, which only returns a status like OK.' },
    { ids: ['a', 'queue-b', 'queue-c', 'b', 'c'], step: 2,
      caption: 'This time, A writes commands to its own persistent queues. B and C read them over the network.' },
  ],

  happy: [
    { id: 'command', from: 'client', to: 'a', label: '! Submit', step: 3, caption: 'First, the happy path.' },
    { id: 'enqueue-b', from: 'a', to: 'queue-b', label: '! Update B', step: 4,
      caption: 'A writes a command to each queue...' },
    { id: 'enqueue-c', from: 'a', to: 'queue-c', label: '! Update C', step: 4 },
    { id: 'ok', from: 'a', to: 'client', label: 'OK', lane: 1, step: 5,
      caption: '...and answers right away. Here, OK means "received", not "done".' },
    { id: 'deliver-b', from: 'queue-b', to: 'b', label: '! Update B', async: true, step: 6,
      caption: 'B and C work through their queues on their own schedule.' },
    { id: 'deliver-c', from: 'queue-c', to: 'c', label: '! Update C', async: true, step: 6 },
  ],

  reset: { caption: 'Now a router fails between C and its queue.' },

  partition: { service: 'c', caption: 'A network partition: C can no longer read its queue.' },

  messages: [
    { id: 'command', from: 'client', to: 'a', label: '! Submit', step: 4 },
    { id: 'enqueue-b', from: 'a', to: 'queue-b', label: '! Update B', step: 5 },
    { id: 'enqueue-c', from: 'a', to: 'queue-c', label: '! Update C', step: 5 },
    { id: 'ok', from: 'a', to: 'client', label: 'OK', lane: 1, step: 6,
      caption: "A doesn't notice the partition, and neither does the client: it still gets OK." },
    { id: 'deliver-b', from: 'queue-b', to: 'b', label: '! Update B', async: true, step: 7,
      caption: 'B processes its command...' },
    { id: 'deliver-c', from: 'queue-c', to: 'c', label: '! Update C', async: true, cut: true, kind: 'fail', step: 8,
      caption: "...but C can't reach its queue, so C's command waits." },
    { id: 'deliver-c-later', from: 'queue-c', to: 'c', label: '! Update C', lane: 1, async: true, step: 11,
      caption: '...C works through its backlog, and the system is consistent again.' },
  ],

  callout: {
    lines: ['STILL', 'AVAILABLE!'],
    step: 9,
    caption: "The system is temporarily inconsistent: A's command looks done, but C's part is still pending.",
  },

  restore: { step: 10, edges: ['deliver-c'], caption: 'When C reconnects...' },

  captions: [
    { id: 'trade-offs', step: 12,
      caption: 'Queues add latency even when the network is up. But queue length tells you when to scale out B or C.' },
    { id: 'wrap-up', step: 13,
      caption: 'Design the user experience for this, like an online store: checkout returns at once, and an email confirms the order, or a problem, later.' },
  ],
});
