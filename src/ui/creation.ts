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
import { button, element, options, redrawGuard, section, type Host, type Section } from './dom.js';

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
      pool: entry.number('pool'),
    })),
    portraits: f.list('portraits', (entry) => ({
      id: entry.text('id'),
      name: entry.text('name'),
      race: entry.text('race'),
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

/**
 * One member of the party as a person reads it, from the values the flow published and no others. The race is
 * left to the chosen portrait and to the accepted list: it is the portrait that decided it.
 */
function describe(member: CreationMemberView): string {
  const named = member.name === '' ? 'unnamed' : member.name;
  return `${member.index + 1}. ${named} · ${member.class === '' ? 'no class' : member.class} · ${member.step}`;
}

/** Mounts the creation screen. */
export function mountCreation(host: Host): Section<CreationReading> {
  const { panel, claim } = host;
  const creation = section('crawler-creation');
  const stepHead = element('p', 'crawler-step-head');
  const memberList = element('div', 'crawler-row');
  const portraitRow = element('div', 'crawler-row');
  const classRow = element('div', 'crawler-row');
  const skillRow = element('div', 'crawler-row');
  const attributeRow = element('div', 'crawler-row');
  const nameRow = element('div', 'crawler-name');
  const nameInput = element('input');
  nameInput.type = 'text';
  nameInput.placeholder = 'Name this character';
  const nameButton = button('Set name');
  nameButton.addEventListener('click', () => claim(ACTIONS.setName, { name: nameInput.value }));
  nameRow.append(nameInput, nameButton);
  const flowRow = element('div', 'crawler-actions');
  const advanceButton = button('Confirm step');
  const acceptButton = button('Accept party');
  for (const control of [advanceButton, acceptButton]) {
    control.addEventListener('click', () => {
      if (control.dataset.action !== undefined && control.dataset.action !== '') claim(control.dataset.action);
    });
  }

  flowRow.append(advanceButton, acceptButton);
  const refusal = element('p', 'crawler-refusal');
  refusal.hidden = true;
  const acceptedList = element('ul', 'crawler-accepted');
  creation.append(stepHead, memberList, portraitRow, classRow, skillRow, attributeRow, nameRow, flowRow, refusal, acceptedList);
  const changed = redrawGuard();

  /** The members as they stand, each a button that moves creation onto it. */
  const members = (view: CreationView): HTMLElement => {
    const row = element('div');
    const heading = element('span', 'crawler-row-label');
    heading.textContent = `Party — member ${view.member + 1} of ${view.members}`;
    const list = element('div', 'crawler-options');
    for (const member of view.roster) {
      const choice = button(describe(member));
      choice.dataset.member = String(member.index);
      choice.dataset.step = member.step;
      choice.dataset.selected = String(member.index === view.member);
      choice.addEventListener('click', () => claim(ACTIONS.selectMember, { member: member.index }));
      list.append(choice);
    }

    row.append(heading, list);
    return row;
  };

  /** The attributes with the two moves creation allows, each carrying what the race permits. */
  const attributes = (view: CreationView): HTMLElement => {
    const row = element('div');
    const heading = element('span', 'crawler-row-label');
    heading.textContent = `Attributes — ${view.pool} point${view.pool === 1 ? '' : 's'} left`;
    row.append(heading);
    for (const attribute of view.attributes) {
      const line = element('div', 'crawler-attribute');
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
      line.append(value, lower, raise);
      row.append(line);
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
      for (const row of [memberList, portraitRow, classRow, skillRow, attributeRow]) row.replaceChildren();
      nameRow.hidden = true;
      flowRow.hidden = true;
      return;
    }

    acceptedList.replaceChildren();
    nameRow.hidden = false;
    flowRow.hidden = false;
    stepHead.textContent = view.roster.every((member) => member.step === 'complete')
      ? 'Every member is finished; accept the party, or reopen one to change it.'
      : `Creating member ${view.member + 1} of ${view.members} · ${view.step}`;
    memberList.replaceChildren(members(view));
    portraitRow.replaceChildren(
      options(
        claim,
        'Portrait — decides the race',
        view.portraits.map((portrait) => ({
          id: portrait.id,
          text: portrait.name,
          selected: portrait.selected,
          action: ACTIONS.selectPortrait,
          payload: () => ({ portrait: portrait.id }),
        })),
      ),
    );
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
