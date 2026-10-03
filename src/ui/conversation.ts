/**
 * The conversation screen: who the party is speaking with, what they said, what can be brought up, and what the
 * state withholds with its reason. It comes before the counter's screen because it is what a player reaches a
 * counter through: a person is talked to first, and stepping up to what they keep is one of their offers.
 *
 * Every word here came from the product — the greeting, each topic, each reason, and what was said — and the panel
 * spells none of them itself.
 */

import type { ControlView } from './overview.js';
import type { Fields } from './reader.js';
import { ACTIONS } from './actions.js';
import { button, element, redrawGuard, report, result, section, type Host, type Section } from './dom.js';

/** One person present in a conversation. */
export interface ConversationPersonView {
  readonly id: string;
  readonly name: string;
  readonly portrait: string;
  /** The URL the Engine serves the person's portrait at, empty when there is none to show. */
  readonly portraitImage: string;
  /** Whether this is the person already speaking, whom there is nothing to turn to. */
  readonly speaking: boolean;
}

/** One topic a speaker has: on offer, or withheld with the reason the state gives. */
export interface ConversationTopicView {
  readonly id: string;
  readonly label: string;
  readonly available: boolean;
  readonly reason: string;
}

/** One thing said while the conversation has been open. */
export interface ConversationLineView {
  readonly speaker: string;
  readonly text: string;
  readonly residue: string;
}

/** One member the product says could try to lift what the person spoken with carries. */
export interface ConversationThiefView {
  /** The member's place in the party, which a theft command names. */
  readonly index: number;
  readonly name: string;
}

/** The conversation block: who is here, what was said, which topics are on offer, and which are withheld. */
export interface ConversationView {
  readonly available: boolean;
  readonly open: boolean;
  readonly subject: string;
  readonly speaker: string;
  readonly greeting: string;
  readonly people: readonly ConversationPersonView[];
  readonly topics: readonly ConversationTopicView[];
  readonly withheld: readonly ConversationTopicView[];
  readonly said: readonly ConversationLineView[];
  readonly action: string;
  readonly outcome: string;
  readonly code: string;
  readonly message: string;
  readonly residue: string;
  /** The mechanism a taken topic handed the party to, empty when it handed it nowhere. */
  readonly handoff: string;
  /** The topic the last choice named, empty before one. */
  readonly topic: string;
  /** The members who could try to lift what the person carries; empty when nobody could. */
  readonly thieves: readonly ConversationThiefView[];
}

function readTopic(entry: Fields): ConversationTopicView {
  return { id: entry.text('id'), label: entry.text('label'), available: entry.flag('available'), reason: entry.text('reason') };
}

export function readConversation(f: Fields): ConversationView {
  return {
    available: f.flag('available'),
    open: f.flag('open'),
    subject: f.text('subject'),
    speaker: f.text('speaker'),
    greeting: f.text('greeting'),
    people: f.list('people', (entry) => ({
      id: entry.text('id'),
      name: entry.text('name'),
      portrait: entry.text('portrait'),
      portraitImage: entry.text('portraitImage'),
      speaking: entry.flag('speaking'),
    })),
    topics: f.list('topics', readTopic),
    withheld: f.list('withheld', readTopic),
    said: f.list('said', (entry) => ({ speaker: entry.text('speaker'), text: entry.text('text'), residue: entry.text('residue') })),
    action: f.text('action'),
    outcome: f.text('outcome', 'none'),
    code: f.text('code'),
    message: f.text('message'),
    residue: f.text('residue'),
    handoff: f.text('handoff'),
    topic: f.text('topic'),
    thieves: f.list('thieves', (entry) => ({ index: entry.number('index'), name: entry.text('name') })),
  };
}

/** What the conversation screen draws from: the conversation, and the control that leaves it. */
export interface ConversationReading {
  readonly conversation: ConversationView;
  readonly leave: ControlView;
}

/** Mounts the conversation screen. */
export function mountConversation(host: Host): Section<ConversationReading> {
  const { panel, claim } = host;
  const conversation = section('crawler-conversation');
  const conversationHead = element('p', 'crawler-step-head');
  const greeting = element('p', 'crawler-conversation-greeting');
  const people = element('div', 'crawler-row');
  const topics = element('div', 'crawler-options');
  const withheld = element('ul', 'crawler-withheld');
  const said = element('ul', 'crawler-said');
  const thieves = element('div', 'crawler-row');
  const actions = element('div', 'crawler-actions');
  const leave = button('Take your leave');
  leave.addEventListener('click', () => {
    if (leave.dataset.action !== undefined && leave.dataset.action !== '') claim(leave.dataset.action);
  });
  actions.append(leave);
  const outcome = result('crawler-conversation-result');
  const residue = result('crawler-conversation-residue');
  conversation.append(conversationHead, greeting, people, topics, withheld, said, thieves, actions, outcome, residue);
  const changed = redrawGuard();

  const render = ({ conversation: view, leave: control }: ConversationReading): void => {
    // A session with no mechanism, one that is speaking with nobody, and one whose last choice was refused are
    // three different facts: a panel that hid the section for the first would leave a product without dialogue
    // looking like a party that had simply not spoken to anybody.
    panel.dataset.conversation = !view.available ? 'none' : view.open ? 'open' : view.outcome === 'none' ? 'available' : 'closed';
    panel.dataset.conversationAction = view.action;
    panel.dataset.conversationOutcome = view.outcome;
    conversation.hidden = !view.available || (!view.open && view.outcome === 'none');
    conversation.dataset.subject = view.subject;
    conversation.dataset.handoff = view.handoff;
    conversation.dataset.topic = view.topic;
    conversationHead.textContent =
      view.speaker === ''
        ? ''
        : view.people.length > 1
          ? `Speaking with ${view.speaker} (${view.people.length} here)`
          : `Speaking with ${view.speaker}`;
    greeting.textContent = view.greeting;
    report(outcome, view.outcome, view.code, view.message);
    residue.hidden = view.residue === '';
    residue.textContent = view.residue;
    leave.dataset.id = control.action;
    leave.dataset.action = control.action;
    leave.disabled = !control.enabled;
    if (!changed(view)) return;

    if (!view.open) {
      for (const list of [people, topics, withheld, said, thieves]) list.replaceChildren();
      return;
    }

    // Who is here: one button per person, which turns the conversation to them. The person speaking is shown as
    // the one already chosen, because a button that does nothing must not look like one that does.
    people.replaceChildren(
      ...view.people.map((person) => {
        const choice = button(person.name);
        choice.dataset.id = person.id;
        choice.dataset.person = person.id;
        choice.disabled = person.speaking;
        choice.addEventListener('click', () => claim(ACTIONS.conversationPerson, { target: person.id }));
        return choice;
      }),
    );

    // What may be brought up: a topic the state withholds is shown disabled, because the product would refuse it by
    // name and a choice that cannot be made must not look like one that can.
    topics.replaceChildren(
      ...view.topics.map((topic) => {
        const choice = button(topic.label);
        choice.dataset.id = topic.id;
        choice.disabled = !topic.available;
        choice.addEventListener('click', () => claim(ACTIONS.conversationTopic, { target: topic.id }));
        return choice;
      }),
    );

    // What the state withholds, with the reason each is withheld: the same vocabulary a locked door uses, so a
    // player learns what the person is waiting for instead of seeing a shorter list.
    withheld.replaceChildren(
      ...view.withheld.map((topic) => {
        const item = element('li');
        item.dataset.id = topic.id;
        item.textContent = `${topic.label} — ${topic.reason}`;
        return item;
      }),
    );

    // Who could try to lift what the person carries is the product's list: one button per member it named, which
    // sends that member's hand. What came of it is the counter's report, because a theft is the service mechanism's.
    thieves.replaceChildren(
      ...view.thieves.map((thief) => {
        const choice = button(`${thief.name}: steal`);
        choice.dataset.id = `steal-${thief.index}`;
        choice.dataset.member = String(thief.index);
        choice.addEventListener('click', () => claim(ACTIONS.conversationSteal, { member: thief.index }));
        return choice;
      }),
    );

    // What has been said while the conversation has been open, oldest first, so a line taken is still readable
    // after it leaves the list of things to bring up.
    said.replaceChildren(
      ...view.said.map((line) => {
        const item = element('li');
        item.dataset.speaker = line.speaker;
        item.textContent = line.residue === '' ? line.text : `${line.text} ${line.residue}`;
        return item;
      }),
    );
  };

  return { element: conversation, render };
}
