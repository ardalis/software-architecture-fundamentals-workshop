// Synchronous commands: A sends commands to B and C directly. When B is cut off,
// A fails and C has already made its change: neither available nor consistent.

import { capDiagram } from '../cap-kit.js';

export default capDiagram({
  id: 'cap-sync-commands',
  title: 'Synchronous Commands: Neither Available nor Consistent',
  description:
    'Service A handles a client command by sending commands to Services B and C synchronously. ' +
    "When a partition cuts B off, C's command has already succeeded but B's never arrives, " +
    'so A reports failure and the system is left inconsistent.',

  cast: [
    { ids: ['client'], step: 1,
      caption: 'A client sends commands to Service A. A ! marks a command, which only returns a status like OK.' },
    { ids: ['a', 'b', 'c'], step: 2, caption: 'To carry out a command, A must also change data in B and C.' },
  ],

  happy: [
    { id: 'command', from: 'client', to: 'a', label: '! Submit', step: 3, caption: 'First, the happy path.' },
    { id: 'command-c', from: 'a', to: 'c', label: '! Update C', step: 4,
      caption: 'A sends a command to each service and waits for its status.' },
    { id: 'ok-c', from: 'c', to: 'a', label: 'OK', lane: 1, step: 5 },
    { id: 'command-b', from: 'a', to: 'b', label: '! Update B', step: 6 },
    { id: 'ok-b', from: 'b', to: 'a', label: 'OK', lane: 1, step: 7 },
    { id: 'ok', from: 'a', to: 'client', label: 'OK', lane: 1, step: 8,
      caption: 'Everything is up, so every command succeeds.' },
  ],

  reset: { caption: 'Now a router fails between A and B.' },

  partition: { service: 'b', caption: 'A network partition: A and B can no longer talk to each other.' },

  messages: [
    { id: 'command', from: 'client', to: 'a', label: '! Submit', step: 4 },
    { id: 'command-c', from: 'a', to: 'c', label: '! Update C', step: 5,
      caption: "C's command goes through..." },
    { id: 'ok-c', from: 'c', to: 'a', label: 'OK', lane: 1, step: 6 },
    { id: 'command-b', from: 'a', to: 'b', label: '! Update B', cut: true, kind: 'fail', step: 7,
      caption: "...but B's never arrives." },
    { id: 'not-ok', from: 'a', to: 'client', label: 'Not OK', lane: 1, kind: 'fail', step: 8,
      caption: 'A reports a failure, so A is not available.' },
  ],

  callout: {
    lines: ['UNAVAILABLE', 'AND INCONSISTENT!'],
    step: 9,
    caption: 'Worse, C already made its change. The system is now inconsistent, and undoing that may be expensive.',
  },

  captions: [
    { id: 'wrap-up', step: 10,
      caption: 'A distributed transaction coordinator can prevent this, but it adds a point of failure and hurts performance. Use one only when you must.' },
  ],
});
