/**
 * The small DOM vocabulary every section is built with, and the one guard that decides when a section's controls
 * are rebuilt.
 */

import type { Claim } from './actions.js';

/** What a section is mounted with: the panel whose attributes it writes, and the way it asks for an action. */
export interface Host {
  readonly panel: HTMLElement;
  readonly claim: Claim;
}

/** A mounted section: the element it lives in, and how it draws one reading of its block. */
export interface Section<T> {
  readonly element: HTMLElement;
  render(view: T): void;
}

/** The namespace every shape of the automap is created in, because a drawing is not HTML. */
export const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';

/** An element of the given tag with the given class, and nothing else. */
export function element<K extends keyof HTMLElementTagNameMap>(tag: K, className = ''): HTMLElementTagNameMap[K] {
  const made = document.createElement(tag);
  if (className !== '') made.className = className;
  return made;
}

/** A section of the panel, hidden until a render shows it. */
export function section(className: string): HTMLElement {
  const made = element('section', className);
  made.hidden = true;
  return made;
}

/** The heading line a section opens with. */
export function head(text = ''): HTMLParagraphElement {
  const made = element('p', 'crawler-step-head');
  made.textContent = text;
  return made;
}

/** A button of the given class and words. */
export function button(text: string, className = ''): HTMLButtonElement {
  const made = element('button', className);
  made.type = 'button';
  made.textContent = text;
  return made;
}

/** A line that reports what the last command did, hidden until there is something to report. */
export function result(className: string): HTMLParagraphElement {
  const made = element('p', className);
  made.hidden = true;
  return made;
}

/** Writes a command's answer into its result line: the outcome and code as attributes, the sentence as text. */
export function report(line: HTMLElement, outcome: string, code: string, message: string): void {
  line.hidden = message === '';
  line.dataset.outcome = outcome;
  line.dataset.code = code;
  line.textContent = message;
}

/** `s` when a count reads as more than one, so a sentence agrees with the number the product published. */
export function plural(count: number, many = 's', one = ''): string {
  return count === 1 ? one : many;
}

/** A labelled row of choices, each a button that asks for what it names or is disabled when it names nothing. */
export function options(
  claim: Claim,
  label: string,
  entries: readonly {
    readonly id: string;
    readonly text: string;
    readonly selected?: boolean;
    readonly available?: boolean;
    readonly enabled?: boolean;
    readonly action?: string;
    readonly payload?: () => Readonly<Record<string, string | number>>;
  }[],
): HTMLElement {
  const row = element('div');
  const heading = element('span', 'crawler-row-label');
  heading.textContent = label;
  const list = element('div', 'crawler-options');
  for (const entry of entries) {
    const choice = button(entry.text);
    choice.dataset.id = entry.id;
    if (entry.selected !== undefined) choice.dataset.selected = String(entry.selected);
    if (entry.available !== undefined) choice.dataset.available = String(entry.available);
    // A row the product says it would refuse is shown disabled with its fact on it, and a row that names no
    // command is shown as a fact rather than a control: a button that cannot work must not look like one that can.
    if (entry.action === undefined || entry.enabled === false) choice.disabled = true;
    if (entry.action !== undefined) {
      const action = entry.action;
      // The payload is read when the button is pressed rather than when it is drawn: a lesson goes to whoever
      // the picker shows at that moment, not to whoever it showed when the row was last drawn.
      choice.addEventListener('click', () => claim(action, entry.payload?.() ?? {}));
    }

    list.append(choice);
  }

  row.append(heading, list);
  return row;
}

/**
 * Says whether a section's controls must be drawn again for this reading of its block.
 *
 * This is the one place the companion remembers anything between projections, and it remembers only what it last
 * drew. It must stay: the product publishes once per admitted update and only when the value changed, but a
 * running session's value changes every update — its admitted simulation time and step count move each step — so
 * a block that did not change still arrives sixty times a second. A section rebuilt on each of those would replace
 * the button under the pointer between its press and its release, and reset a picker a player has open. The guard
 * compares the whole published block, so what is drawn is always the last value the product sent and never a
 * reading the panel kept of its own; sections without controls redraw every time and use no guard.
 */
export function redrawGuard(): (view: unknown) => boolean {
  let drawn: string | null = null;
  return (view) => {
    const signature = JSON.stringify(view);
    if (signature === drawn) return false;
    drawn = signature;
    return true;
  };
}

/** A picker offering the given rows, each an option whose value is what a command sends back. */
export function picker(className: string, rows: readonly { readonly value: string; readonly text: string }[]): HTMLSelectElement {
  const made = element('select', className);
  for (const row of rows) {
    const choice = element('option');
    choice.value = row.value;
    choice.textContent = row.text;
    made.append(choice);
  }

  return made;
}

export type { Claim };
