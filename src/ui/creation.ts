/**
 * The creation screen: the members as they stand, the choices creation offers the member being made, the rule the
 * last choice broke, and the party once one has been accepted.
 *
 * Every list is rebuilt from the projection, so the screen holds nothing the product did not publish. A choice the
 * projection lists is offered whatever its state: the flow is what decides whether it is legal, and its refusal is
 * what the screen shows — hiding a choice here would hide the rule.
 */

import type { ControlView } from './overview.js';
import type { Fields } from './reader.js';
import { ACTIONS } from './actions.js';
import { button, element, options, plural, redrawGuard, section, type Host, type Section } from './dom.js';

/** One member of the party being created, as the flow published it. */
export interface CreationMemberView {
  readonly index: number;
  /** Where this member stands: `portrait`, `class`, `name`, `attributes`, `skills`, or `complete`. */
  readonly step: string;
  readonly name: string;
  /** The race the portrait decided, empty while no portrait has been chosen. */
  readonly race: string;
  /** The class chosen, empty while none has been. */
  readonly class: string;
  readonly portrait: string;
  /** The URL the Engine serves the chosen portrait's face at, empty when there is none to show. */
  readonly portraitImage: string;
  /** How many attribute points this member has still to spend. */
  readonly pool: number;
}

/** One member of the party a session accepted, as it stands in the party being played. */
export interface CreationPartyMemberView {
  readonly index: number;
  readonly name: string;
  readonly race: string;
  readonly class: string;
  readonly portrait: string;
}

/** One portrait creation offers. */
export interface CreationPortraitView {
  readonly id: string;
  readonly name: string;
  readonly race: string;
  /** The URL the Engine serves the portrait's face at, empty when there is none to show. */
  readonly image: string;
  readonly selected: boolean;
}

/** One class creation offers. */
export interface CreationClassView {
  readonly id: string;
  readonly name: string;
  readonly selected: boolean;
}

/** One skill of the member being created, and where it stands: `fixed`, `chosen`, or `available`. */
export interface CreationSkillView {
  readonly id: string;
  readonly name: string;
  readonly state: string;
}

/** One attribute of the member being created, with the range its race allows and what the pool may do to it. */
export interface CreationAttributeView {
  readonly id: string;
  readonly name: string;
  readonly value: number;
  readonly minimum: number;
  readonly maximum: number;
  readonly canRaise: boolean;
  readonly canLower: boolean;
}

/**
 * The creation screen: where the flow stands, what the member being created has chosen, what it may still choose,
 * the rule the last illegal choice broke, and — once a party has been accepted — the members of the party played.
 */
export interface CreationView {
  readonly active: boolean;
  readonly accepted: boolean;
  readonly hasDefault: boolean;
  readonly member: number;
  readonly members: number;
  readonly step: string;
  readonly pool: number;
  readonly refusalCode: string;
  readonly refusalMessage: string;
  readonly roster: readonly CreationMemberView[];
  readonly portraits: readonly CreationPortraitView[];
  readonly classes: readonly CreationClassView[];
  readonly skills: readonly CreationSkillView[];
  readonly attributes: readonly CreationAttributeView[];
  readonly party: readonly CreationPartyMemberView[];
}

export function readCreation(f: Fields): CreationView {
  return {
    active: f.flag('active'),
    accepted: f.flag('accepted'),
    hasDefault: f.flag('hasDefault'),
    member: f.number('member'),
    members: f.number('members'),
    step: f.text('step'),
    pool: f.number('pool'),
    refusalCode: f.text('refusalCode'),
    refusalMessage: f.text('refusalMessage'),
    roster: f.list('roster', (entry) => ({
      index: entry.number('index'),
      step: entry.text('step'),
      name: entry.text('name'),
      race: entry.text('race'),
      class: entry.text('class'),
      portrait: entry.text('portrait'),
      portraitImage: entry.text('portraitImage'),
      pool: entry.number('pool'),
    })),
    portraits: f.list('portraits', (entry) => ({
      id: entry.text('id'),
      name: entry.text('name'),
      race: entry.text('race'),
      image: entry.text('image'),
      selected: entry.flag('selected'),
    })),
    classes: f.list('classes', (entry) => ({ id: entry.text('id'), name: entry.text('name'), selected: entry.flag('selected') })),
    skills: f.list('skills', (entry) => ({ id: entry.text('id'), name: entry.text('name'), state: entry.text('state') })),
    attributes: f.list('attributes', (entry) => ({
      id: entry.text('id'),
      name: entry.text('name'),
      value: entry.number('value'),
      minimum: entry.number('minimum'),
      maximum: entry.number('maximum'),
      canRaise: entry.flag('canRaise'),
      canLower: entry.flag('canLower'),
    })),
    party: f.list('party', (entry) => ({
      index: entry.number('index'),
      name: entry.text('name'),
      race: entry.text('race'),
      class: entry.text('class'),
      portrait: entry.text('portrait'),
    })),
  };
}

/** What the creation screen draws from: the flow, whether the party was resumed, and creation's two controls. */
export interface CreationReading {
  readonly creation: CreationView;
  readonly resumed: boolean;
  readonly advance: ControlView;
  readonly accept: ControlView;
}

/** A face: the portrait's image when the Engine serves one, otherwise the initial that names it. */
function face(image: string, initial: string): HTMLElement {
  if (image !== '') {
    const picture = element('img', 'crawler-creation-face');
    picture.src = image;
    picture.alt = '';
    return picture;
  }

  // A face offered by name alone needs no initial beside that name; a member card without one shows its initial.
  const letter = element('span', 'crawler-creation-face crawler-creation-initial');
  letter.textContent = initial.charAt(0).toUpperCase();
  return letter;
}

/** A line of text in the given class. */
function line(className: string, text: string): HTMLElement {
  const made = element('span', className);
  made.textContent = text;
  return made;
}

/**
 * Mounts the creation screen: the party's members across the top, each a card that moves creation onto it; the
 * faces, classes and skills the member being made may take on the left; its name and attributes on the right; and
 * the flow's controls beneath, with the rule the last choice broke where a player cannot miss it.
 */
export function mountCreation(host: Host): Section<CreationReading> {
  const { panel, claim } = host;
  const creation = section('crawler-creation');
  const stepHead = element('p', 'crawler-step-head');
  const memberRow = element('div', 'crawler-creation-members');
  const choices = element('div', 'crawler-creation-choices');
  const portraitRow = element('div', 'crawler-row crawler-creation-portraits');
  const classRow = element('div', 'crawler-row');
  const skillRow = element('div', 'crawler-row');
  choices.append(portraitRow, classRow, skillRow);
  const sheet = element('div', 'crawler-creation-sheet');
  const nameRow = element('form', 'crawler-name');
  const nameLabel = line('crawler-row-label', 'Name');
  const nameInput = element('input');
  nameInput.type = 'text';
  nameInput.placeholder = 'Name this character';
  const nameButton = button('Set name');
  nameButton.type = 'submit';
  // Enter in the box and the button are one control: the name is sent as typed and the flow judges it.
  nameRow.addEventListener('submit', (event) => {
    event.preventDefault();
    claim(ACTIONS.setName, { name: nameInput.value });
  });
  nameRow.append(nameLabel, nameInput, nameButton);
  const attributeRow = element('div', 'crawler-row');
  sheet.append(nameRow, attributeRow);
  const layout = element('div', 'crawler-creation-layout');
  layout.append(choices, sheet);
  const flowRow = element('div', 'crawler-actions');
  const resetButton = button('Reset member');
  resetButton.addEventListener('click', () => claim(ACTIONS.resetMember));
  const defaultButton = button('Restore default party');
  defaultButton.addEventListener('click', () => claim(ACTIONS.applyDefault));
  const advanceButton = button('Confirm step');
  const acceptButton = button('Accept party');
  for (const control of [advanceButton, acceptButton]) {
    control.addEventListener('click', () => {
      if (control.dataset.action !== undefined && control.dataset.action !== '') claim(control.dataset.action);
    });
  }

  flowRow.append(resetButton, defaultButton, advanceButton, acceptButton);
  const refusal = element('p', 'crawler-refusal');
  refusal.hidden = true;
  const acceptedList = element('ul', 'crawler-accepted');
  creation.append(stepHead, memberRow, refusal, layout, flowRow, acceptedList);
  const changed = redrawGuard();
  // The name box is written from the projection only when the member it names, or that member's published name,
  // changes: a draft being typed is the player's until it is sent.
  let named = '';

  /** The members as they stand, each a card that moves creation onto it. */
  const members = (view: CreationView): HTMLElement[] =>
    view.roster.map((member) => {
      const card = button('', 'crawler-creation-member');
      card.dataset.member = String(member.index);
      card.dataset.step = member.step;
      card.dataset.selected = String(member.index === view.member);
      const kind = [member.race, member.class].filter((part) => part !== '').join(' · ');
      card.append(
        face(member.portraitImage, member.name),
        line('crawler-creation-member-name', member.name === '' ? 'Unnamed' : member.name),
        line('crawler-creation-member-kind', kind === '' ? 'No portrait or class yet' : kind),
        line('crawler-creation-member-step', member.step === 'complete' ? 'Ready' : member.step),
        line('crawler-creation-member-pool', `${member.pool} point${plural(member.pool)} left`),
      );
      card.addEventListener('click', () => claim(ACTIONS.selectMember, { member: member.index }));
      return card;
    });

  /** The faces creation offers; the one chosen decides the member's race. */
  const portraits = (view: CreationView): HTMLElement => {
    const row = element('div');
    const list = element('div', 'crawler-options');
    for (const portrait of view.portraits) {
      const choice = button('');
      choice.dataset.id = portrait.id;
      choice.dataset.selected = String(portrait.selected);
      choice.title = `${portrait.name} — ${portrait.race}`;
      choice.append(face(portrait.image, ''), line('crawler-creation-face-name', portrait.name));
      choice.addEventListener('click', () => claim(ACTIONS.selectPortrait, { portrait: portrait.id }));
      list.append(choice);
    }

    row.append(line('crawler-row-label', 'Face — decides the race'), list);
    return row;
  };

  /** The attributes with the two moves creation allows, each carrying what the race permits. */
  const attributes = (view: CreationView): HTMLElement => {
    const row = element('div');
    row.append(line('crawler-row-label', `Attributes — ${view.pool} point${plural(view.pool)} left`));
    for (const attribute of view.attributes) {
      const entry = element('div', 'crawler-attribute');
      const value = element('span');
      value.textContent = `${attribute.name} ${attribute.value} (${attribute.minimum}–${attribute.maximum})`;
      const lower = button('−');
      lower.dataset.attribute = attribute.id;
      lower.dataset.canLower = String(attribute.canLower);
      lower.addEventListener('click', () => claim(ACTIONS.lowerAttribute, { attribute: attribute.id }));
      const raise = button('+');
      raise.dataset.attribute = attribute.id;
      raise.dataset.canRaise = String(attribute.canRaise);
      raise.addEventListener('click', () => claim(ACTIONS.raiseAttribute, { attribute: attribute.id }));
      entry.append(value, lower, raise);
      row.append(entry);
    }

    return row;
  };

  const render = (reading: CreationReading): void => {
    const { creation: view, resumed } = reading;
    // A resumed session plays a party it did not create here, and says so rather than claiming a creation that
    // never happened: the roster below it is the same either way.
    panel.dataset.creation = view.active ? 'active' : view.accepted ? (resumed ? 'resumed' : 'accepted') : 'none';
    creation.hidden = !view.active && !view.accepted;
    // The refusal and the two flow controls are written every time because they are what a player must not miss.
    refusal.hidden = view.refusalMessage === '';
    refusal.dataset.code = view.refusalCode;
    refusal.textContent = view.refusalMessage;
    advanceButton.disabled = !reading.advance.enabled;
    advanceButton.dataset.action = reading.advance.action;
    acceptButton.disabled = !reading.accept.enabled;
    acceptButton.dataset.action = reading.accept.action;
    defaultButton.hidden = !view.hasDefault;
    if (!changed(reading)) return;

    if (!view.active) {
      // The party a session accepted is shown from the party itself, so the screen's list is the party being
      // played rather than the draft that described it.
      acceptedList.replaceChildren(
        ...view.party.map((member) => {
          const item = element('li');
          item.textContent = `${member.index + 1}. ${member.name} · ${member.race} · ${member.class} · ${member.portrait}`;
          return item;
        }),
      );
      stepHead.textContent = view.accepted ? (resumed ? 'Party resumed' : 'Party accepted') : '';
      for (const row of [memberRow, portraitRow, classRow, skillRow, attributeRow]) row.replaceChildren();
      named = '';
      nameRow.hidden = true;
      flowRow.hidden = true;
      layout.hidden = true;
      return;
    }

    acceptedList.replaceChildren();
    nameRow.hidden = false;
    flowRow.hidden = false;
    layout.hidden = false;
    stepHead.textContent = view.roster.every((member) => member.step === 'complete')
      ? 'Every member is finished; accept the party, or reopen one to change it.'
      : `Creating member ${view.member + 1} of ${view.members} · ${view.step}`;
    memberRow.replaceChildren(...members(view));
    const current = view.roster.find((member) => member.index === view.member);
    const naming = `${view.member}:${current?.name ?? ''}`;
    if (naming !== named) {
      named = naming;
      nameInput.value = current?.name ?? '';
    }

    portraitRow.replaceChildren(portraits(view));
    classRow.replaceChildren(
      options(
        claim,
        'Class — decides the skills',
        view.classes.map((option) => ({
          id: option.id,
          text: option.name,
          selected: option.selected,
          action: ACTIONS.selectClass,
          payload: () => ({ class: option.id }),
        })),
      ),
    );
    skillRow.replaceChildren(
      options(
        claim,
        'Skills — the fixed ones, the chosen ones, and the rest',
        view.skills.map((skill) => ({
          id: skill.id,
          text: skill.state === 'fixed' ? `${skill.name} (fixed)` : skill.name,
          selected: skill.state === 'chosen',
          available: skill.state === 'available',
          // A fixed skill names no choice: it is the class's, and the flow would refuse removing it.
          action: skill.state === 'chosen' ? ACTIONS.removeSkill : skill.state === 'available' ? ACTIONS.chooseSkill : undefined,
          payload: () => ({ skill: skill.id }),
        })),
      ),
    );
    attributeRow.replaceChildren(attributes(view));
  };

  return { element: creation, render };
}
