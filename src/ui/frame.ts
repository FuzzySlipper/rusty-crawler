/**
 * The adventure frame's screens: which one shows over the world, how a player opens and closes a book, and the
 * diagnostic panel kept beside the game rather than in its way.
 *
 * A contextual screen — party creation, a conversation, a counter — is the product's: it shows exactly while the
 * product says the party is making itself, speaking or standing at a counter, and only that screen's own controls
 * leave it. A book is the player's: a button or its key opens it, the same again or Escape closes it, and the world
 * is what shows when none is open. Which book is open is presentation only. It is kept here and nowhere else, it is
 * not saved, and it changes no projection and asks the product for nothing. The Engine's input stays in its gameplay
 * mode throughout, so the host's own keys — rest, leave, attack — work over any screen, and every change of screen
 * hands the keyboard back to the game view, so a book opened by its button does not keep the keyboard on the button.
 */

import type { ProductUiContext } from './context.js';
import { button, element } from './dom.js';
import { BOOKS, DIAGNOSTICS_KEY, type ScreenName } from './hud.js';
import type { SnapshotView } from './snapshot.js';

/** Every screen the frame can show: the player's books and the product's contextual screens. */
export type FrameScreen = ScreenName | 'creation' | 'conversation' | 'service';

/** One screen: its container, the body its sections are placed in, and whether a player closes it. */
interface Pane {
  readonly element: HTMLElement;
  readonly body: HTMLElement;
}

/** The mounted frame's screens. */
export interface Frame {
  readonly element: HTMLElement;
  readonly diagnostics: HTMLElement;
  /** The body a screen's sections are placed in. */
  body(screen: Exclude<FrameScreen, 'world'>): HTMLElement;
  /** Opens a book, or closes it when it is the one open. */
  open(screen: ScreenName): void;
  toggleDiagnostics(): void;
  /** Shows what the product says is in front of the party, and the open book when nothing is. */
  render(snapshot: SnapshotView): void;
  /** Forgets what the product held open, for a moment the product publishes nothing. */
  clear(): void;
  dispose(): void;
}

const TITLES: Record<Exclude<FrameScreen, 'world'>, string> = {
  character: 'Character',
  spellbook: 'Spellbook',
  journal: 'Journal',
  map: 'Map',
  rest: 'Rest',
  creation: 'Make your party',
  conversation: 'Conversation',
  service: 'Counter',
};

/** Whether a key press belongs to a field a player is typing in, which the frame never takes keys from. */
function typing(event: KeyboardEvent): boolean {
  const target = event.target as Partial<HTMLElement> | null;
  return target?.isContentEditable === true || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target?.tagName ?? '');
}

/**
 * Mounts the frame's screens into `panel`. `onChange` is told which screen shows, so the bar can mark the open book.
 */
export function mountFrame(panel: HTMLElement, context: ProductUiContext, onChange: (screen: FrameScreen) => void): Frame {
  const screens = element('div', 'crawler-screens');
  const panes = new Map<Exclude<FrameScreen, 'world'>, Pane>();
  let chosen: ScreenName = 'world';
  let contextual: FrameScreen | null = null;
  let showing: FrameScreen = 'world';
  let setupShown = false;

  for (const [name, title] of Object.entries(TITLES) as [Exclude<FrameScreen, 'world'>, string][]) {
    const pane = element('section', 'crawler-screen');
    pane.dataset.screen = name;
    pane.hidden = true;
    const top = element('header', 'crawler-screen-head');
    const heading = element('h2');
    heading.textContent = title;
    top.append(heading);
    if (BOOKS.some((book) => book.screen === name)) {
      const close = button('Close (Esc)', 'crawler-screen-close');
      close.addEventListener('click', () => open('world'));
      top.append(close);
    }

    const body = element('div', 'crawler-screen-body');
    pane.append(top, body);
    screens.append(pane);
    panes.set(name, { element: pane, body });
  }

  const diagnostics = element('aside', 'crawler-diagnostics');
  diagnostics.hidden = true;

  const show = (): void => {
    const next: FrameScreen = contextual ?? chosen;
    for (const [name, pane] of panes) pane.element.hidden = name !== next;
    panel.dataset.screen = next;
    if (next !== showing) {
      showing = next;
      context.ui.focusGameplay();
    }

    onChange(next);
  };

  const open = (screen: ScreenName): void => {
    chosen = chosen === screen ? 'world' : screen;
    show();
  };

  const toggleDiagnostics = (): void => {
    diagnostics.hidden = !diagnostics.hidden;
    panel.dataset.diagnostics = diagnostics.hidden ? 'hidden' : 'shown';
  };

  const keydown = (event: KeyboardEvent): void => {
    if (event.repeat || event.ctrlKey || event.metaKey || event.altKey || typing(event)) return;
    const key = event.key.toLowerCase();
    if (key === DIAGNOSTICS_KEY) {
      toggleDiagnostics();
      return;
    }

    // While the product holds a contextual screen, its own controls are what leave it.
    if (contextual !== null) return;
    if (key === 'escape' && chosen !== 'world') {
      open('world');
      return;
    }

    const book = BOOKS.find((entry) => entry.key !== '' && entry.key === key);
    if (book !== undefined) open(book.screen);
  };
  document.addEventListener('keydown', keydown);
  panel.dataset.diagnostics = 'hidden';

  return {
    element: screens,
    diagnostics,
    body: (screen) => panes.get(screen)!.body,
    open,
    toggleDiagnostics,
    render(snapshot) {
      contextual = snapshot.session.mode === 'creating'
        ? 'creation'
        : snapshot.conversation.open
          ? 'conversation'
          : snapshot.service.open
            ? 'service'
            : null;
      // What the product puts in front of the party closes the book behind it: leaving returns to the world.
      if (contextual !== null) chosen = 'world';
      // A product that cannot play its content says how to set it up, which is the one thing to show.
      if (snapshot.composition.setup !== null && !setupShown) {
        setupShown = true;
        if (diagnostics.hidden) toggleDiagnostics();
      }
      show();
    },
    clear() {
      contextual = null;
      show();
    },
    dispose() {
      document.removeEventListener('keydown', keydown);
    },
  };
}
