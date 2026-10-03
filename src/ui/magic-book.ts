/**
 * The spellbook (`L`): one member's book at a time, a page per school they hold, every spell of the school on it —
 * learned or not — and the spell a player picked with what it costs, what it is aimed at, and why it cannot be cast
 * when it cannot. Casting and the quick slot are the product's actions; the Mixing page holds the party's alchemy.
 *
 * Whether a spell is learned, its price and what stands in the way are the casting workflow's own answers, published
 * per member; the aim offered is the side or the places the product said the spell names. Which page is open and
 * which spell is picked are presentation state.
 */

import type { AlchemyView } from './alchemy.js';
import type { MagicView, SpellPageEntryView, SpellPageView, SpellRowView } from './magic.js';
import type { PartyView } from './overview.js';
import { ACTIONS } from './actions.js';
import { button, element, heldPicker, redrawGuard, report, result, section, type Host, type Section } from './dom.js';
import { memberFaces, shownMember } from './faces.js';
import { aimRows } from './magic.js';

/** What the spellbook draws from. */
export interface MagicBookReading {
  readonly magic: MagicView;
  readonly party: PartyView;
  readonly alchemy: AlchemyView;
}

/** The page the mixing section is shown on. */
const MIXING = '\u0000mixing';

/**
 * Mounts the spellbook.
 * @param mixing The party's alchemy section, drawn by its own module and shown on the Mixing page.
 */
export function mountMagicBook(host: Host, mixing: HTMLElement): Section<MagicBookReading> {
  const { panel, claim } = host;
  const book = section('crawler-magic-book');
  const faces = element('div', 'crawler-character-faces');
  const title = element('p', 'crawler-character-title');
  const outcome = result('crawler-magic-book-outcome');
  const tabs = element('div', 'crawler-character-tabs');
  const layout = element('div', 'crawler-magic-book-layout');
  const list = element('div', 'crawler-magic-book-spells');
  const detail = element('div', 'crawler-magic-book-detail');
  layout.append(list, detail);
  const mixingPage = element('div', 'crawler-magic-book-mixing');
  mixingPage.append(mixing);
  book.append(faces, title, outcome, tabs, layout, mixingPage);
  const changed = redrawGuard();
  // Which school is open and which spell is picked, remembered per member so moving between members keeps each one's place.
  const opened = new Map<string, string>();
  const picked = new Map<string, string>();
  // The target chosen for each member's spell, kept across redraws so a cast names the actor the player chose.
  const aimed = new Map<string, string>();
  let last: MagicBookReading | null = null;
  const redraw = (): void => {
    if (last !== null) render(last);
  };

  /** The picked spell's detail: what it is, what it costs, what stands in the way, and the controls the product offers. */
  const describe = (reading: MagicBookReading, index: number, entry: SpellPageEntryView, row: SpellRowView | undefined, quick: string): HTMLElement[] => {
    const parts: HTMLElement[] = [];
    const name = element('p', 'crawler-inspect-name');
    name.textContent = entry.name;
    const facts = element('ul', 'crawler-inspect-facts');
    const lines = [`${entry.tier} rung`, `${entry.cost} spell points`];
    if (row !== undefined) lines.push(`aimed at: ${row.targeting}`, `effect: ${row.effect}`);
    facts.append(
      ...lines.map((text) => {
        const item = element('li');
        item.textContent = text;
        return item;
      }),
    );
    parts.push(name, facts);
    const standing = element('p', entry.refusal === '' ? 'crawler-spell-ready' : 'crawler-spell-refusal');
    standing.dataset.code = entry.refusalCode;
    standing.textContent = entry.refusal === '' ? 'Ready to cast.' : entry.refusal;
    parts.push(standing);
    if (row === undefined) return parts;

    // A spell aimed at an actor offers that side's rows; one aimed at a place offers the places it names; one aimed at
    // nobody is cast with no target. Whether the aim is good is judged when it is cast.
    const actions = element('div', 'crawler-inspect-actions');
    const aims = aimRows(reading.magic, row);
    const choice = aims.length > 0 ? heldPicker('crawler-magic-book-target', aims, aimed, `${index}:${row.spell}`) : null;
    if (choice !== null) actions.append(choice);
    const cast = button(`Cast ${row.name}`, 'crawler-magic-book-cast');
    cast.disabled = !row.canCast;
    cast.addEventListener('click', () => claim(ACTIONS.cast, { member: index, spell: row.spell, target: choice === null ? '' : choice.value }));
    const quickControl = button(quick === row.spell ? 'Clear quick spell' : 'Make quick spell', 'crawler-magic-book-quick');
    quickControl.addEventListener('click', () => claim(ACTIONS.quickSpell, { member: index, spell: quick === row.spell ? '' : row.spell }));
    actions.append(cast, quickControl);
    parts.push(actions);
    return parts;
  };

  /** One school's page: every spell it teaches, learned ones first in the catalog's order as published. */
  const page = (school: SpellPageView, key: string, chosen: string): HTMLElement[] => {
    const head = element('p', 'crawler-row-label');
    head.textContent = `${school.school} · ${school.held}`;
    const rows = school.spells.map((entry) => {
      const line = button('', 'crawler-magic-book-spell');
      line.dataset.spell = entry.spell;
      line.dataset.known = String(entry.known);
      line.dataset.picked = String(entry.spell === chosen);
      if (entry.refusal !== '') line.dataset.refused = entry.refusalCode;
      const name = element('span', 'crawler-magic-book-spell-name');
      name.textContent = entry.name;
      const meta = element('span', 'crawler-magic-book-spell-meta');
      meta.textContent = entry.known ? `${entry.tier} · ${entry.cost}` : 'not learned';
      line.append(name, meta);
      line.addEventListener('click', () => {
        picked.set(key, entry.spell);
        redraw();
      });
      return line;
    });
    return [head, ...rows];
  };

  const render = (reading: MagicBookReading): void => {
    last = reading;
    const { magic, party } = reading;
    const { member: shown, index } = shownMember(party.roster);
    const own = magic.members.find((entry) => entry.index === index);
    book.hidden = !magic.available || party.roster.length === 0;
    report(outcome, magic.outcome, magic.code, magic.message);
    const key = shown?.member ?? '';
    const schools = own?.pages ?? [];
    // A member's first school opens by default; a member with none opens on a page that says so.
    let open = opened.get(key) ?? schools[0]?.school ?? '';
    if (open !== MIXING && !schools.some((school) => school.school === open)) open = schools[0]?.school ?? '';
    panel.dataset.spellbookPage = open === MIXING ? 'mixing' : open;
    mixingPage.hidden = open !== MIXING;
    layout.hidden = open === MIXING;
    if (!changed({ reading: { magic, roster: party.roster }, open, picked: picked.get(key) ?? '' })) return;

    faces.replaceChildren(...memberFaces(claim, party.roster, shown));
    title.textContent =
      own === undefined
        ? (shown?.name ?? '')
        : `${own.name} · ${own.spellPoints} / ${own.spellPointsMax} spell points · quick spell: ${own.quickSpellName === '' ? 'none' : own.quickSpellName}`;

    tabs.replaceChildren(
      ...[...schools.map((school) => [school.school, school.school] as const), [MIXING, 'Mixing'] as const].map(([id, label]) => {
        const tab = button(label, 'crawler-character-tab');
        tab.dataset.page = id === MIXING ? 'mixing' : id;
        tab.dataset.open = id === open ? 'yes' : 'no';
        tab.addEventListener('click', () => {
          opened.set(key, id);
          redraw();
        });
        return tab;
      }),
    );

    const school = schools.find((entry) => entry.school === open);
    if (own === undefined || school === undefined) {
      const none = element('p', 'crawler-inspect-hint');
      none.textContent = `${shown?.name ?? 'Nobody'} holds no school of magic.`;
      list.replaceChildren(none);
      detail.replaceChildren();
      return;
    }

    const chosen = picked.get(key) ?? '';
    list.replaceChildren(...page(school, key, chosen));
    const entry = school.spells.find((spell) => spell.spell === chosen);
    if (entry === undefined) {
      const hint = element('p', 'crawler-inspect-hint');
      hint.textContent = 'Pick a spell to see what it costs and cast it.';
      detail.replaceChildren(hint);
      return;
    }

    detail.replaceChildren(...describe(reading, index, entry, own.spells.find((row) => row.spell === entry.spell), own.quickSpell));
  };

  return { element: book, render };
}
