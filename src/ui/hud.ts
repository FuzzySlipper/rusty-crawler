/**
 * The adventure frame's persistent parts: the party's four portraits along the bottom with their pools, conditions and
 * readiness; the purse, larder and clock; the ordinary adventure controls and the buttons that open the books; down the
 * right, the place, the automap and what is running on the party; and along the top, what the party faces and the
 * answer to its latest act.
 *
 * Every value is the product's. A portrait's bars are drawn at the percentage the product published, a member's
 * readiness is the fight's own reading of that member, and a control is offered exactly when the product says it would
 * be taken. Which book is open is the frame's (`frame.ts`); this part only marks it. The companions travelling
 * with the party are the details section's own, placed in the column below the automap.
 */

import { ACTIONS, type Claim } from './actions.js';
import type { FighterView } from './combat.js';
import { button, element, type Host } from './dom.js';
import { feedbackHeading } from './feedback.js';
import { mountMap } from './map.js';
import type { ControlView, RosterMemberView } from './overview.js';
import type { SnapshotView } from './snapshot.js';

/** The books and screens a player opens from the frame, and the world the frame shows when none is open. */
export type ScreenName = 'world' | 'character' | 'spellbook' | 'journal' | 'map' | 'rest';

/** A book the frame offers: its screen, its title, and the key that opens it. */
export interface BookEntry {
  readonly screen: Exclude<ScreenName, 'world'>;
  readonly title: string;
  /** The key, as `KeyboardEvent.key` spells it in lower case; empty for a book opened only by its button. */
  readonly key: string;
}

/** The books, in the order the frame offers them. The keys are ones the host binds to nothing in play. */
export const BOOKS: readonly BookEntry[] = [
  { screen: 'character', title: 'Character', key: 'i' },
  { screen: 'spellbook', title: 'Spellbook', key: 'l' },
  { screen: 'journal', title: 'Journal', key: 'j' },
  { screen: 'map', title: 'Map', key: 'v' },
  { screen: 'rest', title: 'Rest', key: '' },
];

/** The key that shows or hides the diagnostic panel. */
export const DIAGNOSTICS_KEY = '`';

/** What the frame asks of the screen owner. */
export interface Navigation {
  open(screen: ScreenName): void;
  toggleDiagnostics(): void;
}

/** The mounted frame: the bottom bar, the right column, and how it draws one projection. */
export interface Hud {
  readonly bar: HTMLElement;
  readonly side: HTMLElement;
  readonly message: HTMLElement;
  /** The world reticle and the canonical focus context drawn beside it. */
  readonly reticle: HTMLElement;
  render(snapshot: SnapshotView): void;
  /** Marks which screen shows, which is presentation only and changes no projection. */
  mark(screen: string): void;
}

/** A pool bar drawn at the percentage the product published. */
function bar(kind: 'health' | 'spell'): { element: HTMLElement; fill: HTMLElement } {
  const outer = element('div', `crawler-bar crawler-bar-${kind}`);
  const fill = element('div', 'crawler-bar-fill');
  outer.append(fill);
  return { element: outer, fill };
}

/** One member's portrait card. */
function portraitCard(claim: Claim): { element: HTMLButtonElement; render(member: RosterMemberView, fighter: FighterView | undefined): void } {
  const card = element('button', 'crawler-member');
  card.type = 'button';
  const face = element('img', 'crawler-member-face');
  face.alt = '';
  face.draggable = false;
  const initial = element('span', 'crawler-member-initial');
  const name = element('span', 'crawler-member-name');
  const health = bar('health');
  const spell = bar('spell');
  const state = element('span', 'crawler-member-state');
  card.append(face, initial, name, health.element, spell.element, state);
  let member = '';
  card.addEventListener('click', () => {
    if (member !== '') claim(ACTIONS.partySelectMember, { member });
  });
  return {
    element: card,
    render(view, fighter) {
      member = view.member;
      card.dataset.member = view.member;
      card.dataset.action = ACTIONS.partySelectMember;
      card.dataset.selected = view.selected ? 'yes' : 'no';
      card.title = `${view.name} · ${view.class} — ${view.hitPoints}/${view.hitPointsMax} hit points, ${view.spellPoints}/${view.spellPointsMax} spell points`;
      if (view.portraitImage !== '') {
        if (face.getAttribute('src') !== view.portraitImage) face.src = view.portraitImage;
        face.hidden = false;
        initial.hidden = true;
      } else {
        face.removeAttribute('src');
        face.hidden = true;
        initial.hidden = false;
        initial.textContent = view.name.slice(0, 1);
      }

      name.textContent = view.name;
      health.fill.style.width = `${view.hitPointsPercent}%`;
      health.element.title = `${view.hitPoints}/${view.hitPointsMax} hit points`;
      spell.fill.style.width = `${view.spellPointsPercent}%`;
      spell.element.title = `${view.spellPoints}/${view.spellPointsMax} spell points`;
      spell.element.hidden = view.spellPointsMax === 0;

      // What acts on the member first, then what the fight says of them; a member nobody is fighting beside is ready.
      const down = fighter?.down === true;
      const recovering = fighter !== undefined && !fighter.ready && !down;
      card.dataset.state = down ? 'down' : recovering ? 'recovering' : 'ready';
      state.textContent = view.conditions !== ''
        ? view.conditions
        : recovering && fighter !== undefined
          ? `recovering ${fighter.recoverySeconds.toFixed(1)}s`
          : '';
    },
  };
}

/** A control the product offers, drawn as a button with its key, disabled exactly when the product would not take it. */
function controlButton(claim: Claim, text: string, className: string): { element: HTMLButtonElement; render(view: ControlView, label?: string): void } {
  const made = button(text, className);
  let action = '';
  made.addEventListener('click', () => {
    if (action !== '') claim(action);
  });
  return {
    element: made,
    render(view, label = text) {
      action = view.action;
      made.dataset.action = view.action;
      made.disabled = !view.enabled;
      made.textContent = view.key === '' ? label : `${label} (${view.key})`;
    },
  };
}

/** Mounts the frame's persistent parts. */
export function mountHud(host: Host, navigation: Navigation): Hud {
  const { claim } = host;

  // The bottom bar: four portraits, the purse and clock, the adventure controls, and the books.
  const bar = element('div', 'crawler-hud');
  const roster = element('div', 'crawler-roster');
  const cards = Array.from({ length: 4 }, () => portraitCard(claim));
  roster.append(...cards.map((card) => card.element));

  const purse = element('div', 'crawler-purse');
  const coins = element('span', 'crawler-coins');
  const food = element('span', 'crawler-food');
  const date = element('span', 'crawler-date');
  const time = element('span', 'crawler-time');
  purse.append(coins, food, date, time);

  const actions = element('div', 'crawler-hud-actions');
  const use = controlButton(claim, 'Use', 'crawler-act-use');
  const attack = controlButton(claim, 'Attack', 'crawler-act-attack');
  const next = controlButton(claim, 'Next', 'crawler-act-next');
  const pace = controlButton(claim, 'Turn-based', 'crawler-act-pace');
  const save = controlButton(claim, 'Save', 'crawler-act-save');
  actions.append(use.element, attack.element, next.element, pace.element, save.element);

  const books = element('div', 'crawler-books');
  const bookButtons = BOOKS.map((book) => {
    const made = button(book.key === '' ? book.title : `${book.title} (${book.key.toUpperCase()})`, 'crawler-hud-book');
    made.dataset.screen = book.screen;
    made.addEventListener('click', () => navigation.open(book.screen));
    return made;
  });
  const diagnostics = button('…', 'crawler-hud-book crawler-hud-diagnostics');
  diagnostics.title = `Diagnostics (${DIAGNOSTICS_KEY})`;
  diagnostics.addEventListener('click', () => navigation.toggleDiagnostics());
  books.append(...bookButtons, diagnostics);
  bar.append(roster, purse, actions, books);

  // The right column: where the party is, the automap, and what is running on the party.
  const side = element('div', 'crawler-hud-side');
  const place = element('p', 'crawler-hud-place');
  const minimap = mountMap(host);
  minimap.element.classList.add('crawler-minimap');
  const effects = element('ul', 'crawler-hud-effects');
  side.append(place, minimap.element, effects);

  // The line along the top: what the party faces, and the answer to its latest act.
  const message = element('div', 'crawler-hud-message');
  const facing = element('p', 'crawler-facing');
  const said = element('p', 'crawler-hud-said');
  message.append(facing, said);

  // The reticle context is presentation only: its state is copied from the Engine-backed interaction projection. It
  // never stores a target or decides whether an action is legal; its adjacent next-target button is a product control,
  // so turning, cycling and using continue through the same canonical world owner.
  const reticle = element('div', 'crawler-reticle');
  const reticleMark = element('span', 'crawler-reticle-mark');
  reticleMark.textContent = '✣';
  const reticleTarget = element('span', 'crawler-reticle-target');
  const reticleRange = element('span', 'crawler-reticle-range');
  // This is an ordinary world action beside the current context, not a second focus owner: the product decides
  // whether cycling is possible and the claim goes through the same UI action channel as every other control.
  const nextTarget = controlButton(claim, 'Next target', 'crawler-reticle-action');
  reticle.append(reticleMark, reticleTarget, reticleRange, nextTarget.element);
  let showing = 'world';
  let worldProjectionActive = false;

  const focusReason = (reason: string, available: boolean): string => {
    if (!available) return 'interaction unavailable';
    switch (reason) {
      case 'no-candidate': return 'no target in sight';
      case 'outside-query': return 'outside view';
      case 'out-of-reach': return 'out of reach';
      case 'occluded': return 'not in sight';
      case 'visibility-unknown': return 'sight unknown';
      case 'unavailable': return 'unavailable';
      case 'locked': return 'locked';
      case 'invalid-target':
      case 'stale-target': return 'target changed';
      case 'ready': return 'ready';
      default: return reason === '' ? 'no target in sight' : reason;
    }
  };

  return {
    bar,
    side,
    message,
    reticle,
    mark(screen) {
      showing = screen;
      for (const made of bookButtons) made.dataset.open = made.dataset.screen === screen ? 'yes' : 'no';
      // Frame navigation is presentation state. Hide the world-only reticle immediately when a book opens, so
      // the old world context cannot sit over the screen between the click and the next product projection.
      reticle.hidden = !worldProjectionActive || showing !== 'world';
    },
    render(snapshot) {
      const { party, combat, clock, controls, interaction, world, magic } = snapshot;
      const worldActive = party.present && !snapshot.menu.visible && snapshot.session.mode !== 'creating'
        && !snapshot.conversation.open && !snapshot.service.open;
      worldProjectionActive = worldActive;
      bar.hidden = !party.present;
      side.hidden = !party.present;
      message.hidden = !party.present;
      reticle.hidden = !worldActive || showing !== 'world';

      cards.forEach((card, index) => {
        const member = party.roster[index];
        card.element.hidden = member === undefined;
        if (member !== undefined) card.render(member, combat.members.find((fighter) => fighter.member === member.member));
      });

      coins.textContent = `${party.coins} gold`;
      food.textContent = `${party.provisions} ${party.unit}`;
      date.textContent = clock.present ? clock.date : '';
      time.textContent = clock.present ? `${clock.time} · ${clock.daylight}` : '';

      use.render(controls.use);
      attack.render(controls.attack);
      next.render(controls.nextMember);
      nextTarget.render(controls.nextTarget);
      // The pacing control names the pacing a press switches to, as the fight beside the world does.
      pace.render(controls.turnBased, combat.pacing === 'turnbased' ? 'Real-time' : 'Turn-based');
      save.render(controls.save);

      place.textContent = world.name;
      minimap.render(snapshot.map);
      effects.replaceChildren(
        ...magic.running.map((effect) => {
          const item = element('li', 'crawler-effect');
          item.textContent = effect.endsAt === '' ? effect.effect : `${effect.effect} until ${effect.endsAt}`;
          return item;
        }),
        ...magic.memberRunning.map((effect) => {
          const item = element('li', 'crawler-effect');
          item.textContent = `${effect.name}: ${effect.effect}`;
          return item;
        }),
      );
      effects.hidden = effects.childElementCount === 0;

      // What the party faces, and why the reticle holds or refuses it.
      const disposition = interaction.disposition === '' ? '' : ` · ${interaction.disposition}`;
      facing.textContent = interaction.label === ''
        ? interaction.available ? `No target · ${focusReason(interaction.reason, true)}` : 'No interaction target'
        : interaction.verb === ''
          ? `${interaction.label}${disposition}`
          : `${interaction.label}${disposition} — ${interaction.verb} · ${interaction.distance.toFixed(0)}`;
      facing.dataset.reason = interaction.reason;
      facing.hidden = !worldActive;

      reticle.dataset.state = interaction.reason;
      reticleTarget.textContent = interaction.label === ''
        ? interaction.available ? `No target · ${focusReason(interaction.reason, true)}` : 'No interaction target'
        : `${interaction.label}${disposition}`;
      reticleRange.textContent = interaction.label === '' || interaction.distance <= 0
        ? focusReason(interaction.reason, interaction.available)
        : `${focusReason(interaction.reason, interaction.available)} · ${interaction.distance.toFixed(0)} away`;
      // The answer to the party's latest act, under the act it answers: the product numbers each answer, so this line
      // is always the latest one and never an older owner's result standing in for it.
      const { feedback } = snapshot;
      said.dataset.serial = String(feedback.serial);
      said.dataset.source = feedback.source;
      said.dataset.outcome = feedback.outcome;
      said.textContent = feedback.serial === 0 ? '' : `${feedbackHeading(feedback)}: ${feedback.message}`;
      said.hidden = feedback.serial === 0;
    },
  };
}
