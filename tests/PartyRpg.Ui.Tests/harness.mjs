/**
 * The companion suite's stand-in for the Engine: a browser document, and a mount context built to the shape the
 * Engine's application host delivers (`src/ui/context.ts` states it and where it was read from).
 *
 * It is deliberately no looser than the Engine: the context and its ports are frozen, `projection.subscribe`
 * delivers the current envelope — `null` before anything is published — synchronously before it returns, every
 * envelope carries the whole of what the Engine sends (artifact, runtime identity, sequence, stream, contract,
 * value), and every value is detached and deeply frozen, so a companion that wrote into what it was given would
 * throw here exactly as it would in a browser.
 */
import { readFile } from 'node:fs/promises';
import { JSDOM } from 'jsdom';

/** A checked-in fixture the host suite wrote from the product's own output. */
export async function fixture(name) {
  return JSON.parse(await readFile(new URL(`./fixtures/${name}`, import.meta.url), 'utf8'));
}

/** Freezes a value and everything inside it, as the Engine delivers every projection value. */
export function deepFreeze(value) {
  if (typeof value === 'object' && value !== null) {
    for (const entry of Object.values(value)) deepFreeze(entry);
    Object.freeze(value);
  }

  return value;
}

/** The stream and contract the product declares, which every envelope the harness delivers is written on. */
const STREAM = 'crawler.hud';
const CONTRACT = 'crawler.ui.snapshot.v1';

export function harness() {
  const dom = new JSDOM('<!doctype html><html><body><div id="root"></div></body></html>');
  // The companion is browser code: it reads the global document, exactly as it does in the shell.
  globalThis.window = dom.window;
  globalThis.document = dom.window.document;
  const { document } = dom.window;
  const claims = [];
  const listeners = new Set();
  let current = null;
  let sequence = 0;
  let unsubscribed = false;
  let focused = 0;
  let mode = 'gameplay';

  const ui = Object.freeze({
    active: () => true,
    allowsGameplayInput: () => mode === 'gameplay',
    focusGameplay: () => {
      focused += 1;
    },
    interactionMode: () => mode,
    setInteractionMode: (next) => {
      mode = next;
    },
  });
  const projection = Object.freeze({
    current: () => current,
    subscribe: (listener) => {
      if (typeof listener !== 'function') throw new TypeError('listener must be a function');
      listeners.add(listener);
      // The Engine delivers the current envelope before `subscribe` returns, and swallows what a listener throws.
      try {
        listener(current);
      } catch {
        // A listener's own failure is not the Engine's.
      }

      let done = false;
      return () => {
        if (done) return;
        done = true;
        unsubscribed = true;
        listeners.delete(listener);
      };
    },
  });
  const intents = Object.freeze({
    claim: (intent, value) => {
      // The Engine's claim is synchronous, returns nothing, and refuses a malformed intent or payload at once.
      if (typeof intent !== 'string' || !/^[a-z0-9](?:[a-z0-9]|[._-](?=[a-z0-9]))*$/.test(intent)) {
        throw new TypeError(`intent '${intent}' is not a declared intent name`);
      }

      if (value?.kind !== 'product-payload' || typeof value.contract !== 'string') {
        throw new TypeError('the companion claims only product payloads');
      }

      // The payload is snapshotted as plain JSON, which is all the Engine accepts.
      claims.push({ intent, value: JSON.parse(JSON.stringify(value)) });
    },
  });
  const input = Object.freeze({ subscribe: () => () => {} });
  const context = Object.freeze({ ui, projection, intents, input });

  const root = document.querySelector('#root');
  const timers = { setTimeout: 0, setInterval: 0, requestAnimationFrame: 0 };
  const originals = {
    setTimeout: globalThis.setTimeout,
    setInterval: globalThis.setInterval,
    requestAnimationFrame: globalThis.requestAnimationFrame,
  };
  globalThis.setTimeout = (...args) => {
    timers.setTimeout += 1;
    return originals.setTimeout(...args);
  };
  globalThis.setInterval = (...args) => {
    timers.setInterval += 1;
    return originals.setInterval(...args);
  };
  globalThis.requestAnimationFrame = () => {
    timers.requestAnimationFrame += 1;
    return 0;
  };

  const restore = () => {
    globalThis.setTimeout = originals.setTimeout;
    globalThis.setInterval = originals.setInterval;
    globalThis.requestAnimationFrame = originals.requestAnimationFrame;
    delete globalThis.document;
    delete globalThis.window;
  };

  /** Publishes one value as the Engine would: a whole, frozen envelope, or `null` when there is none. */
  const publish = (envelope) => {
    current = envelope;
    for (const listener of [...listeners]) {
      try {
        listener(envelope);
      } catch {
        // A listener's own failure is not the Engine's.
      }
    }
  };

  return {
    dom,
    document,
    root,
    context,
    claims,
    timers,
    restore,
    emit: (value, contract = CONTRACT) => {
      if (value === null) {
        publish(null);
        return;
      }

      sequence += 1;
      publish(
        deepFreeze({
          artifact: 'rusty.product.ui-projection',
          runtime: { instanceId: '1', generation: '1', controlRevision: '1' },
          sequence: String(sequence),
          stream: STREAM,
          contract,
          value: structuredClone(value),
        }),
      );
    },
    panel: () => root.querySelector('.crawler-session'),
    // The session's own action buttons are direct children of the diagnostic panel, where the frame keeps every
    // control the product publishes: the creation screen's buttons live inside their own section, and a case that
    // clicks 'the button' means the session's.
    button: () => root.querySelector('.crawler-diagnostics > button'),
    saveButton: () => root.querySelector('.crawler-diagnostics > button.crawler-save'),
    useButton: () => root.querySelector('.crawler-diagnostics > button.crawler-use'),
    /** The problems the panel named reading the last projection, as it printed them. */
    problems: () => [...(root.querySelectorAll('.crawler-problems li') ?? [])].map((item) => item.textContent),
    get unsubscribed() {
      return unsubscribed;
    },
    get focused() {
      return focused;
    },
  };
}
