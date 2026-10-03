/**
 * The character book: one member at a time, as the game's character screen shows them — their scores, vitals and
 * resistances, their growth and the ranks their class leads to, their skills, what they wear beside the party's one
 * shared pack, and what the party has accomplished.
 *
 * The member shown is the party's selected member, the same one the adventure bar outlines; a face at the top of
 * the book selects another through the product. Which page is open is presentation state. Every row is a value the
 * product published, and every control claims the action the product names for it.
 */

import type { EquipmentView } from './equipment.js';
import type { MagicView } from './magic.js';
import type { PartyView } from './overview.js';
import type { ProgressionView } from './progression.js';
import type { PromotionView } from './promotion.js';
import type { Fields } from './reader.js';
import type { SkillsView } from './skills.js';
import { ACTIONS } from './actions.js';
import { button, element, plural, redrawGuard, report, result, section, type Host, type Section } from './dom.js';
import { mountInventory } from './inventory.js';

/** One row of a member's sheet, in the game's words. */
export interface CharacterRowView {
  readonly label: string;
  readonly value: string;
  readonly detail: string;
}

/** One member's sheet, as the game reads it. */
export interface CharacterMemberView {
  readonly index: number;
  readonly member: string;
  readonly name: string;
  readonly sections: readonly { readonly title: string; readonly rows: readonly CharacterRowView[] }[];
}

/** Every member's sheet. */
export interface CharacterView {
  readonly available: boolean;
  readonly members: readonly CharacterMemberView[];
}

export function readCharacter(f: Fields): CharacterView {
  return {
    available: f.flag('available'),
    members: f.list('members', (entry) => ({
      index: entry.number('index'),
      member: entry.text('member'),
      name: entry.text('name'),
      sections: entry.list('sections', (part) => ({
        title: part.text('title'),
        rows: part.list('rows', (row) => ({ label: row.text('label'), value: row.text('value'), detail: row.text('detail') })),
      })),
    })),
  };
}

/** What the character book draws from. */
export interface CharacterReading {
  readonly character: CharacterView;
  readonly party: PartyView;
  readonly equipment: EquipmentView;
  readonly magic: MagicView;
  readonly skills: SkillsView;
  readonly progression: ProgressionView;
  readonly promotion: PromotionView;
}

/** The book's pages, in the order the original's character screen tabs them. */
const PAGES = [
  ['stats', 'Stats'],
  ['skills', 'Skills'],
  ['inventory', 'Inventory'],
  ['awards', 'Awards'],
] as const;
type Page = (typeof PAGES)[number][0];

/** A titled box of label–value rows. */
function table(title: string, rows: readonly CharacterRowView[]): HTMLElement {
  const box = element('div', 'crawler-sheet-box');
  const heading = element('p', 'crawler-sheet-title');
  heading.textContent = title;
  const list = element('dl', 'crawler-sheet-rows');
  for (const row of rows) {
    const term = element('dt');
    term.textContent = row.label;
    const value = element('dd');
    value.textContent = row.value;
    if (row.detail !== '') value.title = row.detail;
    if (row.detail !== '') value.dataset.detail = row.detail;
    list.append(term, value);
  }

  box.append(heading, list);
  return box;
}

/**
 * Mounts the character book.
 * @param awards The section that lists what the party has accomplished, drawn by its own module and shown on the Awards page.
 */
export function mountCharacter(host: Host, awards: HTMLElement): Section<CharacterReading> {
  const { panel, claim } = host;
  const book = section('crawler-character');
  const faces = element('div', 'crawler-character-faces');
  const title = element('p', 'crawler-character-title');
  const tabs = element('div', 'crawler-character-tabs');
  const body = element('div', 'crawler-character-page');
  const inventory = mountInventory(host);
  const skillOutcome = result('crawler-character-skill-outcome');
  const trainOutcome = result('crawler-character-train-outcome');
  // The accomplishments section stays mounted, drawn by its own module on every projection, and is shown only on its page.
  const awardsPage = element('div', 'crawler-character-awards');
  awardsPage.append(awards);
  book.append(faces, title, tabs, body, awardsPage, skillOutcome, trainOutcome);
  const changed = redrawGuard();
  let page: Page = 'stats';
  let last: CharacterReading | null = null;

  const tabButtons = PAGES.map(([id, label]) => {
    const tab = button(label, 'crawler-character-tab');
    tab.dataset.page = id;
    tab.addEventListener('click', () => {
      page = id;
      if (last !== null) render(last);
    });
    return tab;
  });
  tabs.append(...tabButtons);

  /** The stats page: the sheet, the member's growth, and the ranks their class leads to. */
  const stats = (reading: CharacterReading, index: number): HTMLElement[] => {
    const parts: HTMLElement[] = [];
    const sheet = reading.character.members.find((entry) => entry.index === index);
    const grid = element('div', 'crawler-sheet');
    for (const part of sheet?.sections ?? []) grid.append(table(part.title, part.rows));
    parts.push(grid);

    const growth = reading.progression.members.find((entry) => entry.index === index);
    if (growth !== undefined) {
      const box = element('div', 'crawler-sheet-box crawler-growth');
      const heading = element('p', 'crawler-sheet-title');
      heading.textContent = 'Growth';
      const line = element('p');
      line.textContent = `Level ${growth.level} · ${growth.experience} experience · level ${growth.nextLevel} at ${growth.nextLevelExperience}`;
      box.append(heading, line);
      if (growth.canTrain) {
        const train = button(`Train to level ${growth.nextLevel} for ${growth.fee} gold`, 'crawler-character-train');
        train.addEventListener('click', () => claim(ACTIONS.serviceTrain, { member: index }));
        box.append(train);
      } else {
        const hint = element('p', 'crawler-growth-hint');
        hint.textContent =
          growth.fee > 0
            ? `This hall trains up to level ${growth.cap} for ${growth.fee} gold.`
            : 'Levels are gained by training at a hall.';
        box.append(hint);
      }

      parts.push(box);
    }

    const ranks = reading.promotion.members.find((entry) => entry.index === index);
    if (ranks !== undefined && ranks.promotions.length > 0) {
      const box = element('div', 'crawler-sheet-box crawler-ranks');
      const heading = element('p', 'crawler-sheet-title');
      heading.textContent = `${ranks.class} · rank ${ranks.rank}`;
      box.append(heading);
      for (const rank of ranks.promotions) {
        const line = element('p', 'crawler-rank');
        line.dataset.promotion = rank.promotion;
        const choice = rank.choice === '' ? '' : ` (${rank.choice})`;
        line.textContent = `${rank.toClass}${choice} — ${rank.requirements.map((need) => need.text).join(', ')}`;
        if (rank.words !== '') line.title = rank.words;
        box.append(line);
      }

      parts.push(box);
    }

    return parts;
  };

  /** The skills page: each skill's rung and ceiling, with the next point offered or the reason there is none. */
  const skills = (reading: CharacterReading, index: number): HTMLElement[] => {
    const member = reading.skills.members.find((entry) => entry.index === index);
    const points = reading.progression.members.find((entry) => entry.index === index)?.skillPoints;
    const head = element('p', 'crawler-row-label');
    head.textContent = points === undefined ? 'Skills' : `Skills · ${points} skill point${plural(points)} to spend`;
    const list = element('div', 'crawler-skill-list');
    for (const row of member?.skills ?? []) {
      const line = element('div', 'crawler-character-skill');
      line.dataset.skill = row.skill;
      line.dataset.block = row.block;
      line.dataset.tier = row.tier;
      const name = element('span', 'crawler-skill-name');
      name.textContent = row.skill;
      const rung = element('span', 'crawler-skill-rung');
      rung.textContent = row.level === 0 ? 'not learned' : `${row.tier} ${row.level}`;
      const ceiling = element('span', 'crawler-skill-ceiling');
      ceiling.textContent = `up to ${row.ceilingTier} ${row.ceilingLevel}`;
      line.append(name, rung, ceiling);
      if (row.refusal === '') {
        const raise = button(`Raise · ${row.cost} pt${plural(row.cost)}`, 'crawler-character-raise');
        raise.addEventListener('click', () => claim(ACTIONS.raiseSkill, { member: index, skill: row.skill }));
        line.append(raise);
      } else {
        const why = element('span', 'crawler-character-skill-refusal');
        why.textContent = row.refusal;
        line.append(why);
      }

      list.append(line);
    }

    return [head, list];
  };

  const render = (reading: CharacterReading): void => {
    last = reading;
    const roster = reading.party.roster;
    const shown = roster.find((entry) => entry.selected) ?? roster[0];
    const index = shown === undefined ? -1 : roster.indexOf(shown);
    book.hidden = roster.length === 0;
    panel.dataset.characterPage = page;
    awardsPage.hidden = page !== 'awards';
    report(skillOutcome, reading.skills.outcome, reading.skills.code, reading.skills.message);
    report(trainOutcome, reading.progression.outcome, reading.progression.code, reading.progression.message);
    if (page === 'inventory') inventory.render({ equipment: reading.equipment, magic: reading.magic, member: index });
    if (!changed({ reading, page })) return;

    faces.replaceChildren(
      ...roster.map((entry) => {
        const face = button('', 'crawler-character-face');
        face.dataset.member = entry.member;
        face.dataset.selected = entry === shown ? 'yes' : 'no';
        face.title = entry.name;
        if (entry.portraitImage !== '') {
          const picture = element('img');
          picture.src = entry.portraitImage;
          picture.alt = entry.name;
          face.append(picture);
        } else {
          face.textContent = entry.name.charAt(0);
        }

        face.addEventListener('click', () => claim(ACTIONS.partySelectMember, { member: entry.member }));
        return face;
      }),
    );
    title.textContent = shown === undefined ? '' : `${shown.name} · ${shown.class}${shown.conditions === '' ? '' : ` · ${shown.conditions}`}`;
    for (const tab of tabButtons) tab.dataset.open = tab.dataset.page === page ? 'yes' : 'no';

    if (index < 0) {
      body.replaceChildren();
      return;
    }

    body.dataset.page = page;
    if (page === 'stats') body.replaceChildren(...stats(reading, index));
    else if (page === 'skills') body.replaceChildren(...skills(reading, index));
    else if (page === 'inventory') body.replaceChildren(inventory.element);
    else body.replaceChildren();
  };

  return { element: book, render };
}
